// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// This ref struct implements the grammar for complex conditionals.
///
/// The usage is:
///    ParseResult result = Parser.Parse(expression, options, elementLocation);
///
/// The expression tree can then be evaluated and re-evaluated as needed.
/// </summary>
internal ref struct Parser
{
    private readonly string _expression;
    private readonly ParserOptions _options;
    private readonly ElementLocation _elementLocation;

    private Token _current;
    private int _position;

    private string? _errorResource;
    private object?[]? _errorArgs;
    private int _errorPosition;

    // Older versions of MSBuild evaluated mixed 'and'/'or' conditions left-to-right
    // instead of giving 'and' higher precedence. When this was fixed, a compatibility
    // warning (MSB4130) was added to flag expressions that might now evaluate differently.
    // The warning fires for expressions with both 'and' and 'or' at the same level
    // without explicit parentheses.
    private readonly ILoggingService? _loggingServices;
    private readonly BuildEventContext? _logBuildEventContext;
    private bool _warnedForExpression;

    private static string EndOfInput => field ??= ResourceUtilities.GetResourceString("EndOfInputTokenName");

    private Parser(string expression, ParserOptions options, ElementLocation elementLocation, LoggingContext? loggingContext)
    {
        // We currently have no support (and no scenarios) for disallowing property references in Conditions.
        Assumed.True((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

        _expression = expression;
        _options = options;
        _elementLocation = elementLocation;
        _loggingServices = loggingContext?.LoggingService;
        _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
    }

    //
    // Main entry point for parser.
    // You pass in the expression you want to parse, and you get a
    // ParseResult containing either the parsed expression tree or error information.
    //
    public static ParseResult Parse(string expression, ParserOptions options, ElementLocation elementLocation, LoggingContext? loggingContext = null)
    {
        var parser = new Parser(expression, options, elementLocation, loggingContext);
        return parser.ParseCore();
    }

    private ParseResult ParseCore()
    {
        if (!Advance())
        {
            return ErrorResult();
        }

        if (!TryParseExpr(out ExpressionNode? node))
        {
            Assumed.NotNull(_errorResource);
            return ErrorResult();
        }

        if (!IsNext(TokenKind.EndOfInput))
        {
            UnexpectedTokenInCondition();
            return ErrorResult();
        }

        return ParseResult.Success(node);
    }

    private ParseResult ErrorResult()
    {
        Assumed.NotNull(_errorResource);
        Assumed.NotNull(_errorArgs);
        Assumed.Positive(_errorPosition);

        return ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);
    }

    private bool UnexpectedTokenInConditionAndReturn<T>(out T result)
    {
        result = default!;
        return UnexpectedTokenInConditionAndReturn();
    }

    private bool UnexpectedTokenInConditionAndReturn()
    {
        UnexpectedTokenInCondition();
        return false;
    }

    private void UnexpectedTokenInCondition()
    {
        if (HasError)
        {
            return;
        }

        _errorResource = "UnexpectedTokenInCondition";
        _errorArgs = [_expression, _current.ToString(), _errorPosition];
    }

    private bool SetErrorInfo(int position, string resource, string? extraArg = null)
    {
        // Error positions are 1-based for user-facing display.
        int errorPosition = position + 1;
        _errorPosition = errorPosition;
        _errorResource = resource;
        _errorArgs = extraArg is not null
            ? [_expression, errorPosition, extraArg]
            : [_expression, errorPosition];

        return false;
    }

    [MemberNotNullWhen(true, nameof(_errorResource))]
    [MemberNotNullWhen(true, nameof(_errorArgs))]
    private bool HasError => _errorResource is not null;

    private bool TryParseExpr([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseBooleanTerm(out ExpressionNode? node))
        {
            result = null;
            return false;
        }

        if (!IsNext(TokenKind.EndOfInput))
        {
            if (!TryParseExprPrime(node, out node))
            {
                result = null;
                return false;
            }
        }

        // Check for potential change in behavior
        if (_loggingServices != null && !_warnedForExpression && node.PotentialAndOrConflict())
        {
            // We only want to warn once even if there multiple () sub expressions
            _warnedForExpression = true;

            // Log a warning regarding the fact the expression may have been evaluated
            // incorrectly in earlier version of MSBuild
            _loggingServices.LogWarning(_logBuildEventContext, subcategoryResourceName: null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", _expression);
        }

        result = node;
        return true;
    }

    private bool TryParseExprPrime(ExpressionNode lhs, [NotNullWhen(true)] out ExpressionNode? result)
    {
        if (Same(TokenKind.EndOfInput))
        {
            result = lhs;
            return true;
        }

        if (Same(TokenKind.Or))
        {
            if (!TryParseBooleanTerm(out ExpressionNode? rhs))
            {
                result = null;
                return false;
            }

            var orNode = new OrExpressionNode(lhs, rhs);
            return TryParseExprPrime(orNode, out result);
        }

        // ExprPrime always shows up at the rightmost side of the grammar rhs,
        // the EndOfInput case takes care of things
        result = lhs;
        return true;
    }

    private bool TryParseBooleanTerm([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseRelationalExpr(out ExpressionNode? node))
        {
            result = null;
            return false;
        }

        if (!IsNext(TokenKind.EndOfInput) && !TryParseBooleanTermPrime(node, out node))
        {
            result = null;
            return false;
        }

        result = node;
        return true;
    }

    private bool TryParseBooleanTermPrime(ExpressionNode lhs, [NotNullWhen(true)] out ExpressionNode? result)
    {
        if (IsNext(TokenKind.EndOfInput))
        {
            result = lhs;
            return true;
        }

        if (Same(TokenKind.And))
        {
            if (!TryParseRelationalExpr(out ExpressionNode? rhs))
            {
                result = null;
                return false;
            }

            var andNode = new AndExpressionNode(lhs, rhs);
            return TryParseBooleanTermPrime(andNode, out result);
        }

        // Should this be error case?
        result = lhs;
        return true;
    }

    private bool TryParseRelationalExpr([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseFactor(out ExpressionNode? lhs))
        {
            result = null;
            return false;
        }

        if (!TryParseRelationalOperation(out TokenKind operatorKind))
        {
            result = lhs;
            return true;
        }

        if (!TryParseFactor(out ExpressionNode? rhs))
        {
            result = null;
            return false;
        }

        result = operatorKind switch
        {
            TokenKind.LessThan => new LessThanExpressionNode(lhs, rhs),
            TokenKind.GreaterThan => new GreaterThanExpressionNode(lhs, rhs),
            TokenKind.LessThanOrEqualTo => new LessThanOrEqualExpressionNode(lhs, rhs),
            TokenKind.GreaterThanOrEqualTo => new GreaterThanOrEqualExpressionNode(lhs, rhs),
            TokenKind.EqualTo => new EqualExpressionNode(lhs, rhs),
            TokenKind.NotEqualTo => new NotEqualExpressionNode(lhs, rhs),
            _ => throw new InternalErrorException($"Unexpected operator kind: {operatorKind}")
        };
        return true;
    }

    private bool TryParseRelationalOperation(out TokenKind result)
    {
        if (Same(TokenKind.LessThan))
        {
            result = TokenKind.LessThan;
            return true;
        }

        if (Same(TokenKind.GreaterThan))
        {
            result = TokenKind.GreaterThan;
            return true;
        }

        if (Same(TokenKind.LessThanOrEqualTo))
        {
            result = TokenKind.LessThanOrEqualTo;
            return true;
        }

        if (Same(TokenKind.GreaterThanOrEqualTo))
        {
            result = TokenKind.GreaterThanOrEqualTo;
            return true;
        }

        if (Same(TokenKind.EqualTo))
        {
            result = TokenKind.EqualTo;
            return true;
        }

        if (Same(TokenKind.NotEqualTo))
        {
            result = TokenKind.NotEqualTo;
            return true;
        }

        result = default;
        return false;
    }

    private bool TryParseFactor([NotNullWhen(true)] out ExpressionNode? result)
    {
        // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
        if (TryParseArg(out result))
        {
            return true;
        }

        // If it's not one of those, check for other TokenTypes.
        Token current = _current;
        if (Same(TokenKind.FunctionName))
        {
            if (!Same(TokenKind.LeftParenthesis))
            {
                return UnexpectedTokenInConditionAndReturn(out result);
            }

            if (!TryParseArglist(out ImmutableArray<ExpressionNode> arglist))
            {
                result = null;
                return false;
            }

            if (!Same(TokenKind.RightParenthesis))
            {
                return UnexpectedTokenInConditionAndReturn(out result);
            }

            result = new FunctionCallExpressionNode(current.Text, arglist);
            return true;
        }

        if (Same(TokenKind.LeftParenthesis))
        {
            if (!TryParseExpr(out ExpressionNode? child))
            {
                result = null;
                return false;
            }

            if (Same(TokenKind.RightParenthesis))
            {
                result = child;
                return true;
            }

            return UnexpectedTokenInConditionAndReturn(out result);
        }

        if (Same(TokenKind.Not))
        {
            if (!TryParseFactor(out ExpressionNode? expr))
            {
                result = null;
                return false;
            }

            var notNode = new NotExpressionNode(expr);
            result = notNode;
            return true;
        }

        return UnexpectedTokenInConditionAndReturn(out result);
    }

    private bool TryParseArglist(out ImmutableArray<ExpressionNode> result)
    {
        if (IsNext(TokenKind.RightParenthesis))
        {
            result = [];
            return true;
        }

        using RefArrayBuilder<ExpressionNode> args = default;

        while (true)
        {
            if (!TryParseArg(out ExpressionNode? arg))
            {
                return UnexpectedTokenInConditionAndReturn(out result);
            }

            args.Add(arg);

            if (!Same(TokenKind.Comma))
            {
                result = args.ToImmutable();
                return true;
            }
        }
    }

    private bool TryParseArg([NotNullWhen(true)] out ExpressionNode? result)
    {
        Token current = _current;

        if (Same(TokenKind.String))
        {
            result = new StringExpressionNode(current.Text.ToString(), current.Expandable);
            return true;
        }

        if (Same(TokenKind.Number))
        {
            result = new NumericExpressionNode(current.Text);
            return true;
        }

        if (Same(TokenKind.Property))
        {
            result = new StringExpressionNode(current.Text.ToString(), expandable: true);
            return true;
        }

        if (Same(TokenKind.ItemMetadata))
        {
            result = new StringExpressionNode(current.Text.ToString(), expandable: true);
            return true;
        }

        if (Same(TokenKind.ItemList))
        {
            result = new StringExpressionNode(current.Text.ToString(), expandable: true);
            return true;
        }

        result = null;
        return false;
    }

    private bool IsNext(TokenKind kind)
        => _current.IsKind(kind);

    private bool AtEnd
        => _position >= _expression.Length;

    private bool At(char c)
        => !AtEnd && _expression[_position] == c;

    private bool At(string s)
    {
        if (_position + s.Length > _expression.Length)
        {
            return false;
        }

        for (int i = 0; i < s.Length; i++)
        {
            if (_expression[_position + i] != s[i])
            {
                return false;
            }
        }

        return true;
    }

    private void Assume(char c)
    {
        Assumed.True(At(c));
        _position++;
    }

    private bool TryConsume(char c)
    {
        if (!AtEnd && _expression[_position] == c)
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool NextIs(char c)
        => _position + 1 < _expression.Length && _expression[_position + 1] == c;

    /// <summary>
    /// Advance
    /// returns true on successful advance
    ///     and false on an erroneous token
    ///
    /// Doesn't return error until the bogus input is encountered.
    /// Advance() returns true even after EndOfInput is encountered.
    /// </summary>
    private bool Advance()
    {
        if (HasError)
        {
            return false;
        }

        if (_current.IsKind(TokenKind.EndOfInput))
        {
            return true;
        }

        SkipWhiteSpace();

        // Update error position after skipping whitespace
        _errorPosition = _position + 1;

        if (AtEnd)
        {
            _current = Token.EndOfInput;
            return true;
        }

        switch (_expression[_position])
        {
            case ',':
                _current = Token.Comma;
                _position++;
                break;

            case '(':
                _current = Token.LeftParenthesis;
                _position++;
                break;

            case ')':
                _current = Token.RightParenthesis;
                _position++;
                break;

            case '$':
                {
                    if (!TryScanProperty(out _current))
                    {
                        return false;
                    }
                }

                break;

            case '%':
                {
                    if (!TryScanItemMetadata(out _current))
                    {
                        return false;
                    }
                }

                break;

            case '@':
                {
                    if (!TryScanItemList(out _current))
                    {
                        return false;
                    }
                }

                break;

            case '!':
                if (NextIs('='))
                {
                    _current = Token.NotEqualTo;
                    _position += 2;
                }
                else
                {
                    _current = Token.Not;
                    _position++;
                }

                break;

            case '>':
                if (NextIs('='))
                {
                    _current = Token.GreaterThanOrEqualTo;
                    _position += 2;
                }
                else
                {
                    _current = Token.GreaterThan;
                    _position++;
                }

                break;

            case '<':
                if (NextIs('='))
                {
                    _current = Token.LessThanOrEqualTo;
                    _position += 2;
                }
                else
                {
                    _current = Token.LessThan;
                    _position++;
                }

                break;

            case '=':
                if (NextIs('='))
                {
                    _current = Token.EqualTo;
                    _position += 2;
                }
                else
                {
                    string unexpectedlyFound = (_position + 1) < _expression.Length
                        ? _expression[_position + 1].ToString()
                        : EndOfInput;

                    _position++;
                    return SetErrorInfo(_position, "IllFormedEqualsInCondition", unexpectedlyFound);
                }

                break;

            case '\'':
                {
                    if (!TryScanQuotedString(out _current))
                    {
                        return false;
                    }
                }

                break;

            default:
                {
                    int start = _position;

                    if (TryScanNumber(out _current) ||
                        TryScanIdentifier(out _current))
                    {
                        return true;
                    }

                    return SetErrorInfo(
                        start,
                        "UnexpectedCharacterInCondition",
                        _expression[start].ToString());
                }
        }

        return true;
    }

    /// <summary>
    /// Scans a property expression of the form $(propertyname).
    /// Expects _position at '$' on entry.
    /// </summary>
    private bool TryScanProperty(out Token token)
    {
        int start = _position;

        Assume('$');

        if (!TryConsume('('))
        {
            token = default;
            return SetErrorInfo(start, "IllFormedPropertyOpenParenthesisInCondition");
        }

        // Scan for the matching ')'. Property expressions can contain nested
        // parentheses from function calls, e.g. $([System.String]::Format('...')),
        // as well as nested property expressions like $(Foo$(Bar)).
        int nestLevel = 1;
        bool nonIdentifierCharacterFound = false;
        int? whitespacePosition = null;

        while (!AtEnd)
        {
            // Handle nested property expressions by recursing into TryScanProperty.
            if (At("$("))
            {
                nonIdentifierCharacterFound = true;

                if (!TryScanProperty(out _))
                {
                    token = default;
                    return false;
                }

                continue;
            }

            char c = _expression[_position];

            if (c == '(')
            {
                nestLevel++;
                nonIdentifierCharacterFound = true;
            }
            else if (c == ')')
            {
                nestLevel--;

                if (nestLevel == 0)
                {
                    if (whitespacePosition is int pos && !nonIdentifierCharacterFound)
                    {
                        token = default;
                        return SetErrorInfo(pos, "IllFormedPropertySpaceInCondition");
                    }

                    _position++;
                    token = Token.Property(_expression.AsMemory(start, _position - start));
                    return true;
                }
            }
            else if (char.IsWhiteSpace(c))
            {
                whitespacePosition ??= _position;
            }
            else if (!XmlUtilities.IsValidSubsequentElementNameCharacter(c))
            {
                nonIdentifierCharacterFound = true;
            }

            _position++;
        }

        token = default;
        return SetErrorInfo(start, "IllFormedPropertyCloseParenthesisInCondition");
    }

    /// <summary>
    /// Scans an item metadata expression of the form %(metadataname).
    /// Expects _position at '%' on entry.
    /// </summary>
    private bool TryScanItemMetadata(out Token token)
    {
        int start = _position;

        Assume('%');

        if (!TryConsume('('))
        {
            token = default;
            return SetErrorInfo(start, "IllFormedMetadataOpenParenthesisInCondition");
        }

        // Scan for the closing ')'. Metadata references are simply %(Name) or %(ItemType.Name).
        while (!AtEnd)
        {
            if (TryConsume(')'))
            {
                ReadOnlyMemory<char> memory = _expression.AsMemory(start, _position - start);

                if (!CheckForUnexpectedMetadata(start, memory))
                {
                    token = default;
                    return false;
                }

                token = Token.ItemMetadata(memory);
                return true;
            }

            if (At(' '))
            {
                token = default;
                return SetErrorInfo(_position, "IllFormedMetadataSpaceInCondition");
            }

            _position++;
        }

        token = default;
        return SetErrorInfo(start, "IllFormedMetadataCloseParenthesisInCondition");
    }

    /// <summary>
    /// Helper to verify that any AllowBuiltInMetadata or AllowCustomMetadata
    /// specifications are not respected.
    /// Returns true if it is ok, otherwise false.
    /// </summary>
    private bool CheckForUnexpectedMetadata(int start, ReadOnlyMemory<char> expression)
    {
        if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
        {
            return true;
        }

        ReadOnlySpan<char> name = expression.Span;

        if (name is ['%', '(', .. var span, ')'])
        {
            name = span;
        }

        int dotIndex = name.IndexOf('.');

        // Note: The '.' can't be the first or last character.
        if (dotIndex > 0 && dotIndex < name.Length - 1)
        {
            name = name[(dotIndex + 1)..];
        }

        bool isItemSpecModifier = ItemSpecModifiers.IsItemSpecModifier(name);

        if (((_options & ParserOptions.AllowBuiltInMetadata) == 0) && isItemSpecModifier)
        {
            return SetErrorInfo(start, "BuiltInMetadataNotAllowedInThisConditional", name.ToString());
        }

        if (((_options & ParserOptions.AllowCustomMetadata) == 0) && !isItemSpecModifier)
        {
            return SetErrorInfo(start, "CustomMetadataNotAllowedInThisConditional", name.ToString());
        }

        return true;
    }

    /// <summary>
    /// Scans past an item list expression starting at the current position.
    /// Expects At('@') on entry.
    /// On success, _position is advanced past the closing ')'.
    /// </summary>
    private bool TryScanItemList(out Token token)
    {
        int start = _position;

        Assume('@');

        if (!TryConsume('('))
        {
            token = default;
            return SetErrorInfo(start, "IllFormedItemListOpenParenthesisInCondition");
        }

        if ((_options & ParserOptions.AllowItemLists) == 0)
        {
            token = default;
            return SetErrorInfo(start, "ItemListNotAllowedInThisConditional");
        }

        bool inReplacement = false;
        int parenCount = 0;

        while (!AtEnd)
        {
            if (TryConsume('\''))
            {
                inReplacement = !inReplacement;
                continue;
            }

            if (inReplacement)
            {
                _position++;
                continue;
            }

            if (TryConsume('('))
            {
                parenCount++;
                continue;
            }

            if (TryConsume(')'))
            {
                if (parenCount == 0)
                {
                    token = Token.ItemList(_expression.AsMemory(start, _position - start));
                    return true;
                }

                parenCount--;
                continue;
            }

            _position++;
        }

        token = default;
        return SetErrorInfo(
            start,
            inReplacement ? "IllFormedItemListQuoteInCondition" : "IllFormedItemListCloseParenthesisInCondition");
    }

    /// <summary>
    /// Scans a quoted string that may contain property, item, or metadata expressions.
    /// Expects _position at opening quote on entry.
    /// On success, _position is advanced past the closing quote.
    /// </summary>
    private bool TryScanQuotedString(out Token token)
    {
        bool expandable = false;
        int start = _position;

        Assume('\'');

        while (!AtEnd)
        {
            if (TryConsume('\''))
            {
                token = Token.String(_expression.AsMemory(start + 1, _position - start - 2), expandable);
                return true;
            }

            if (At("%("))
            {
                if (!TryScanItemMetadata(out _))
                {
                    token = default;
                    return false;
                }

                expandable = true;
                continue;
            }

            if (At("@("))
            {
                if (!TryScanItemList(out _))
                {
                    token = default;
                    return false;
                }

                expandable = true;
                continue;
            }

            if (At("$("))
            {
                if (!TryScanProperty(out _))
                {
                    token = default;
                    return false;
                }

                expandable = true;
                continue;
            }

            if (At('%'))
            {
                // TODO: Verify that the next two characters are hex digits.
                expandable = true;
            }

            _position++;
        }

        token = default;
        return SetErrorInfo(start, "IllFormedQuotedStringInCondition");
    }

    // There is a bug here that spaces are not required around 'and' and 'or'. For example,
    // this works perfectly well:
    // Condition="%(a.Identity)!=''and%(a.m)=='1'"
    // Since people now depend on this behavior, we must not change it.
    private bool TryScanIdentifier(out Token token)
    {
        int start = _position;
        ReadOnlySpan<char> span = _expression.AsSpan(start);

        if (!CharacterUtilities.IsIdentifierStart(span[0]))
        {
            token = default;
            return false;
        }

        SkipIdentifierChars();

        span = span[..(_position - start)];

        if (span.Equals("and", StringComparison.OrdinalIgnoreCase))
        {
            token = Token.And;
            return true;
        }

        if (span.Equals("or", StringComparison.OrdinalIgnoreCase))
        {
            token = Token.Or;
            return true;
        }

        int end = _position;

        SkipWhiteSpace();

        token = At('(')
            ? Token.FunctionName(_expression.AsMemory(start, end - start))
            : Token.String(_expression.AsMemory(start, end - start));

        return true;
    }

    private bool TryScanNumber(out Token token)
    {
        int start = _position;
        ReadOnlySpan<char> span = _expression.AsSpan(start);

        if (!CharacterUtilities.IsNumberStart(span[0]))
        {
            token = default;
            return false;
        }

        if (span is ['0', ('x' or 'X'), ..])
        {
            _position += 2;
            SkipHexDigits();
            token = Token.Number(_expression.AsMemory(start, _position - start));
            return true;
        }

        if (span[0] is '+' or '-')
        {
            _position++;
        }

        do
        {
            SkipDigits();

            _ = TryConsume('.');

            if (!AtEnd)
            {
                SkipDigits();
            }
        }
        while (At('.'));

        token = Token.Number(_expression.AsMemory(start, _position - start));
        return true;
    }

    private void SkipWhiteSpace()
    {
        while (!AtEnd && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipDigits()
    {
        while (!AtEnd && char.IsDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipHexDigits()
    {
        while (!AtEnd && CharacterUtilities.IsHexDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipIdentifierChars()
    {
        while (!AtEnd && CharacterUtilities.IsIdentifier(_expression[_position]))
        {
            _position++;
        }
    }

    private bool Same(TokenKind token)
        => IsNext(token) && Advance();
}
