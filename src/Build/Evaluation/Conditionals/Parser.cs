// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Parses MSBuild condition expressions into expression trees.
/// </summary>
/// <remarks>
///  <para>The grammar is:</para>
///  <code>
///  Expression     :=  AndExpression ( 'or' AndExpression )*
///  AndExpression  :=  Comparison ( 'and' Comparison )*
///  Comparison     :=  Primary ( ComparisonOp Primary )?
///  ComparisonOp   :=  '==' | '!=' | '&lt;' | '&gt;' | '&lt;=' | '&gt;='
///  Primary        :=  Literal
///                   |  FunctionName '(' ArgumentList ')'
///                   |  '(' Expression ')'
///                   |  '!' Primary
///  ArgumentList   :=  ( Literal ( ',' Literal )* )?
///  Literal        :=  BooleanKeyword | String | Number | Property | ItemMetadata | ItemList
///  </code>
///  <para>
///  The resulting expression tree can then be evaluated and re-evaluated as needed.
///  </para>
/// </remarks>
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
    private bool _hasAnd;
    private bool _hasOr;

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

    /// <summary>
    ///  Parses a condition expression string into an expression tree.
    /// </summary>
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

        if (!TryParseExpression(out ExpressionNode? node))
        {
            return ErrorResult();
        }

        if (!At(TokenKind.EndOfInput))
        {
            SetUnexpectedTokenError();
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

    private bool SetUnexpectedTokenError<T>(out T result)
    {
        result = default!;
        return SetUnexpectedTokenError();
    }

    private bool SetUnexpectedTokenError()
    {
        if (!HasError)
        {
            _errorResource = "UnexpectedTokenInCondition";
            _errorArgs = [_expression, _current.ToString(), _errorPosition];
        }

        return false;
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

    private bool TryParseExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseAndExpression(out ExpressionNode? node))
        {
            result = null;
            return false;
        }

        while (!At(TokenKind.EndOfInput) && TryConsume(TokenKind.Or))
        {
            _hasOr = true;

            if (!TryParseAndExpression(out ExpressionNode? rhs))
            {
                result = null;
                return false;
            }

            node = new OrOperatorNode(node, rhs);
        }

        // Check for potential change in behavior
        if (_loggingServices != null && !_warnedForExpression && _hasAnd && _hasOr)
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

    private bool TryParseAndExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseComparison(out ExpressionNode? node))
        {
            result = null;
            return false;
        }

        while (!At(TokenKind.EndOfInput) && TryConsume(TokenKind.And))
        {
            _hasAnd = true;

            if (!TryParseComparison(out ExpressionNode? rhs))
            {
                result = null;
                return false;
            }

            node = new AndOperatorNode(node, rhs);
        }

        result = node;
        return true;
    }

    private bool TryParseComparison([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParsePrimary(out ExpressionNode? lhs))
        {
            result = null;
            return false;
        }

        if (!TryParseComparisonOperator(out TokenKind operatorKind))
        {
            result = lhs;
            return true;
        }

        if (!TryParsePrimary(out ExpressionNode? rhs))
        {
            result = null;
            return false;
        }

        result = operatorKind switch
        {
            TokenKind.LessThan => new LessThanOperatorNode(lhs, rhs),
            TokenKind.GreaterThan => new GreaterThanOperatorNode(lhs, rhs),
            TokenKind.LessThanOrEqualTo => new LessThanOrEqualOperatorNode(lhs, rhs),
            TokenKind.GreaterThanOrEqualTo => new GreaterThanOrEqualOperatorNode(lhs, rhs),
            TokenKind.EqualTo => new EqualOperatorNode(lhs, rhs),
            TokenKind.NotEqualTo => new NotEqualOperatorNode(lhs, rhs),

            _ => Assumed.Unreachable<ExpressionNode>($"Unexpected operator kind: {operatorKind}"),
        };
        return true;
    }

    private bool TryParseComparisonOperator(out TokenKind result)
    {
        result = _current.Kind;

        if (result is TokenKind.LessThan or TokenKind.GreaterThan
                   or TokenKind.LessThanOrEqualTo or TokenKind.GreaterThanOrEqualTo
                   or TokenKind.EqualTo or TokenKind.NotEqualTo)
        {
            return Advance();
        }

        result = default;
        return false;
    }

    private bool TryParsePrimary([NotNullWhen(true)] out ExpressionNode? result)
    {
        // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
        if (TryParseLiteral(out result))
        {
            return true;
        }

        // If it's not one of those, check for other TokenTypes.
        Token current = _current;
        if (TryConsume(TokenKind.FunctionName))
        {
            if (!TryConsume(TokenKind.LeftParenthesis))
            {
                return SetUnexpectedTokenError(out result);
            }

            if (!TryParseArgumentList(out ImmutableArray<ExpressionNode> arglist))
            {
                result = null;
                return false;
            }

            if (!TryConsume(TokenKind.RightParenthesis))
            {
                return SetUnexpectedTokenError(out result);
            }

            if (current.Text.Equals("Exists", StringComparison.OrdinalIgnoreCase))
            {
                result = new ExistsCallNode(arglist);
            }
            else if (current.Text.Equals("HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
            {
                result = new HasTrailingSlashCallNode(arglist);
            }
            else
            {
                _errorPosition = _position + 1;
                _errorResource = "UndefinedFunctionCall";
                _errorArgs = [_expression, current.Text.ToString()];
                result = null;
                return false;
            }

            return true;
        }

        if (TryConsume(TokenKind.LeftParenthesis))
        {
            // Save and reset the and/or tracking state so that the
            // parenthesized sub-expression is checked independently.
            bool savedHasAnd = _hasAnd;
            bool savedHasOr = _hasOr;
            _hasAnd = false;
            _hasOr = false;

            if (!TryParseExpression(out ExpressionNode? child))
            {
                result = null;
                return false;
            }

            // Restore the outer state. The parenthesized sub-expression
            // was already checked in TryParseExpr, so its and/or flags
            // should not contribute to the outer expression's check.
            _hasAnd = savedHasAnd;
            _hasOr = savedHasOr;

            if (TryConsume(TokenKind.RightParenthesis))
            {
                result = child;
                return true;
            }

            return SetUnexpectedTokenError(out result);
        }

        if (TryConsume(TokenKind.Not))
        {
            // Fold !true/!false into a BooleanLiteralNode at parse time.
            if (TryConsume(TokenKind.True))
            {
                result = new BooleanLiteralNode(false);
                return true;
            }

            if (TryConsume(TokenKind.False))
            {
                result = new BooleanLiteralNode(true);
                return true;
            }

            if (!TryParsePrimary(out ExpressionNode? expr))
            {
                result = null;
                return false;
            }

            result = new NotOperatorNode(expr);
            return true;
        }

        return SetUnexpectedTokenError(out result);
    }

    private bool TryParseArgumentList(out ImmutableArray<ExpressionNode> result)
    {
        if (At(TokenKind.RightParenthesis))
        {
            result = [];
            return true;
        }

        // Parse the first argument.
        if (!TryParseLiteral(out ExpressionNode? firstArg))
        {
            return SetUnexpectedTokenError(out result);
        }

        // If there's no comma, it's a single-argument list — avoid RefArrayBuilder.
        if (!TryConsume(TokenKind.Comma))
        {
            result = [firstArg];
            return true;
        }

        // Multiple arguments — use RefArrayBuilder for the rest.
        using RefArrayBuilder<ExpressionNode> args = new(initialCapacity: 4);
        args.Add(firstArg);

        while (true)
        {
            if (!TryParseLiteral(out ExpressionNode? arg))
            {
                return SetUnexpectedTokenError(out result);
            }

            args.Add(arg);

            if (!TryConsume(TokenKind.Comma))
            {
                result = args.ToImmutable();
                return true;
            }
        }
    }

    private bool TryParseLiteral([NotNullWhen(true)] out ExpressionNode? result)
    {
        Token current = _current;

        if (TryConsume(TokenKind.True))
        {
            result = new BooleanLiteralNode(true);
            return true;
        }

        if (TryConsume(TokenKind.False))
        {
            result = new BooleanLiteralNode(false);
            return true;
        }

        if (TryConsume(TokenKind.String))
        {
            result = new StringLiteralNode(current.Text, current.Expandable);
            return true;
        }

        if (TryConsume(TokenKind.Number))
        {
            result = new NumberLiteralNode(current.Text);
            return true;
        }

        if (TryConsume(TokenKind.Property) ||
            TryConsume(TokenKind.ItemMetadata) ||
            TryConsume(TokenKind.ItemList))
        {
            result = new StringLiteralNode(current.Text, expandable: true);
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    ///  Checks if the given segment is a boolean keyword (true/false/on/off/yes/no).
    /// </summary>
    private static bool TryGetBooleanKeywordValue(StringSegment text, out bool value)
    {
        if (text is ['t' or 'T', 'r' or 'R', 'u' or 'U', 'e' or 'E'] // true
                 or ['o' or 'O', 'n' or 'N'] // on
                 or ['y' or 'Y', 'e' or 'E', 's' or 'S']) // yes
        {
            value = true;
            return true;
        }

        if (text is ['f' or 'F', 'a' or 'A', 'l' or 'L', 's' or 'S', 'e' or 'E'] // false
                 or ['o' or 'O', 'f' or 'F', 'f' or 'F'] // off
                 or ['n' or 'N', 'o' or 'O']) // no
        {
            value = false;
            return true;
        }

        value = default;
        return false;
    }

    private readonly bool AtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position >= _expression.Length;
    }

    /// <summary>
    ///  Returns a segment of the expression from <paramref name="start"/> to the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly StringSegment SegmentFrom(int start)
        => new(_expression, start, _position - start);

    /// <summary>
    ///  Returns a segment of the expression from <paramref name="start"/> with the given <paramref name="length"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly StringSegment Segment(int start, int length)
        => new(_expression, start, length);

    /// <summary>
    ///  Returns a segment of the expression from the current position to the end.
    /// </summary>
    private readonly StringSegment Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_expression, _position, _expression.Length - _position);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool At(char c)
        => !AtEnd && _expression[_position] == c;

    private readonly bool At(string s)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool At(TokenKind kind)
        => _current.Kind == kind;

    private void Consume(char c)
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

    private bool TryConsume(TokenKind kind)
        => At(kind) && Advance();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool PeekNext(char c)
        => _position + 1 < _expression.Length && _expression[_position + 1] == c;

    private void SkipWhiteSpace()
    {
        while (!AtEnd && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }

    private void ScanDigits()
    {
        while (!AtEnd && char.IsDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void ScanHexDigits()
    {
        while (!AtEnd && CharacterUtilities.IsHexDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void ScanIdentifierChars()
    {
        while (!AtEnd && CharacterUtilities.IsIdentifier(_expression[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    ///  Advances to the next token. Returns true on success and false if a scanning error occurs.
    ///  After reaching end-of-input, subsequent calls return true without advancing further.
    /// </summary>
    private bool Advance()
    {
        if (HasError)
        {
            return false;
        }

        if (At(TokenKind.EndOfInput))
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
                if (!TryScanProperty(out _current))
                {
                    return false;
                }

                break;

            case '%':
                if (!TryScanItemMetadata(out _current))
                {
                    return false;
                }

                break;

            case '@':
                if (!TryScanItemList(out _current))
                {
                    return false;
                }

                break;

            case '!':
                if (PeekNext('='))
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
                if (PeekNext('='))
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
                if (PeekNext('='))
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
                if (PeekNext('='))
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
                if (!TryScanQuotedString(out _current))
                {
                    return false;
                }

                break;

            default:
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

        return true;
    }

    /// <summary>
    ///  Scans a property expression of the form $(propertyname).
    /// </summary>
    private bool TryScanProperty(out Token token)
    {
        int start = _position;

        Consume('$');

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
                    token = Token.Property(SegmentFrom(start));
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
    ///  Scans an item metadata expression of the form %(metadataname).
    /// </summary>
    private bool TryScanItemMetadata(out Token token)
    {
        int start = _position;

        Consume('%');

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
                StringSegment segment = SegmentFrom(start);

                if (!CheckForUnexpectedMetadata(start, segment))
                {
                    token = default;
                    return false;
                }

                token = Token.ItemMetadata(segment);
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
    ///  Verifies that metadata references are allowed by the current parser options.
    /// </summary>
    private bool CheckForUnexpectedMetadata(int start, StringSegment expression)
    {
        if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
        {
            return true;
        }

        StringSegment name = expression;

        if (name is ['%', '(', .. var segment, ')'])
        {
            name = segment;
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
    ///  Scans an item list expression of the form @(itemname).
    /// </summary>
    private bool TryScanItemList(out Token token)
    {
        int start = _position;

        Consume('@');

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
                    token = Token.ItemList(SegmentFrom(start));
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
    ///  Scans a quoted string that may contain property, item, or metadata expressions.
    /// </summary>
    private bool TryScanQuotedString(out Token token)
    {
        bool expandable = false;
        int start = _position;

        Consume('\'');

        while (!AtEnd)
        {
            if (TryConsume('\''))
            {
                StringSegment text = Segment(start + 1, _position - start - 2);

                if (!expandable)
                {
                    // Handle negated boolean keywords (e.g. '!true', '!off')
                    if (text is ['!', .. var remainder] && TryGetBooleanKeywordValue(remainder, out bool negatedValue))
                    {
                        token = negatedValue ? Token.False : Token.True;
                        return true;
                    }

                    if (TryGetBooleanKeywordValue(text, out bool booleanValue))
                    {
                        token = booleanValue ? Token.True : Token.False;
                        return true;
                    }
                }

                token = Token.String(text, expandable);
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

        if (!CharacterUtilities.IsIdentifierStart(_expression[start]))
        {
            token = default;
            return false;
        }

        ScanIdentifierChars();

        StringSegment identifier = SegmentFrom(start);

        // Check for 'and'/'or' keywords
        if (identifier is ['o' or 'O', 'r' or 'R'])
        {
            token = Token.Or;
            return true;
        }

        if (identifier is ['a' or 'A', 'n' or 'N', 'd' or 'D'])
        {
            token = Token.And;
            return true;
        }

        // Check for boolean keywords (true/false/on/off/yes/no)
        if (TryGetBooleanKeywordValue(identifier, out bool boolValue))
        {
            token = boolValue ? Token.True : Token.False;
            return true;
        }

        int end = _position;

        SkipWhiteSpace();

        token = At('(')
            ? Token.FunctionName(Segment(start, end - start))
            : Token.String(Segment(start, end - start));

        return true;
    }

    private bool TryScanNumber(out Token token)
    {
        int start = _position;
        StringSegment remaining = Remaining;

        if (!CharacterUtilities.IsNumberStart(remaining[0]))
        {
            token = default;
            return false;
        }

        if (remaining is ['0', ('x' or 'X'), ..])
        {
            _position += 2;
            ScanHexDigits();
            token = Token.Number(SegmentFrom(start));
            return true;
        }

        if (remaining[0] is '+' or '-')
        {
            _position++;
        }

        do
        {
            ScanDigits();

            _ = TryConsume('.');

            if (!AtEnd)
            {
                ScanDigits();
            }
        }
        while (At('.'));

        token = Token.Number(SegmentFrom(start));
        return true;
    }
}
