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

    private TokenKind _currentKind;
    private int _currentStart;
    private int _currentEnd;
    private bool _currentExpandable;
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
            _errorArgs = [_expression, CurrentSegment.ToString(), _errorPosition];
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
        result = _currentKind;

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
        int functionStart = _currentStart;
        int functionEnd = _currentEnd;
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

            StringSegment functionName = Segment(functionStart, functionEnd - functionStart);

            if (functionName.Equals("Exists", StringComparison.OrdinalIgnoreCase))
            {
                result = new ExistsCallNode(arglist);
            }
            else if (functionName.Equals("HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
            {
                result = new HasTrailingSlashCallNode(arglist);
            }
            else
            {
                _errorPosition = _position + 1;
                _errorResource = "UndefinedFunctionCall";
                _errorArgs = [_expression, functionName.ToString()];
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
        int start = _currentStart;
        int end = _currentEnd;
        bool expandable = _currentExpandable;

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
            result = new StringLiteralNode(Segment(start, end - start), expandable);
            return true;
        }

        if (TryConsume(TokenKind.Number))
        {
            result = new NumberLiteralNode(Segment(start, end - start));
            return true;
        }

        if (TryConsume(TokenKind.Property) ||
            TryConsume(TokenKind.ItemMetadata) ||
            TryConsume(TokenKind.ItemList))
        {
            result = new StringLiteralNode(Segment(start, end - start), expandable: true);
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    ///  Checks if the given segment is a boolean keyword (true/false/on/off/yes/no).
    /// </summary>
    private readonly bool AtEnd
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _position >= _expression.Length;
    }

    /// <summary>
    ///  Returns a segment for the current token's text.
    /// </summary>
    private readonly StringSegment CurrentSegment
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_expression, _currentStart, _currentEnd - _currentStart);
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
        => _currentKind == kind;

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
            SetCurrent(TokenKind.EndOfInput, 0);
            return true;
        }

        switch (_expression[_position])
        {
            case ',':
                SetCurrent(TokenKind.Comma, length: 1);
                break;

            case '(':
                SetCurrent(TokenKind.LeftParenthesis, length: 1);
                break;

            case ')':
                SetCurrent(TokenKind.RightParenthesis, length: 1);
                break;

            case '$':
                if (!TryScanProperty())
                {
                    return false;
                }

                break;

            case '%':
                if (!TryScanItemMetadata())
                {
                    return false;
                }

                break;

            case '@':
                if (!TryScanItemList())
                {
                    return false;
                }

                break;

            case '!':
                if (PeekNext('='))
                {
                    SetCurrent(TokenKind.NotEqualTo, length: 2);
                }
                else
                {
                    SetCurrent(TokenKind.Not, length: 1);
                }

                break;

            case '>':
                if (PeekNext('='))
                {
                    SetCurrent(TokenKind.GreaterThanOrEqualTo, length: 2);
                }
                else
                {
                    SetCurrent(TokenKind.GreaterThan, length: 1);
                }

                break;

            case '<':
                if (PeekNext('='))
                {
                    SetCurrent(TokenKind.LessThanOrEqualTo, length: 2);
                }
                else
                {
                    SetCurrent(TokenKind.LessThan, length: 1);
                }

                break;

            case '=':
                if (PeekNext('='))
                {
                    SetCurrent(TokenKind.EqualTo, length: 2);
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
                if (!TryScanQuotedString())
                {
                    return false;
                }

                break;

            default:
                int start = _position;

                if (TryScanNumber() ||
                    TryScanIdentifier())
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCurrent(TokenKind kind, int length)
    {
        _currentKind = kind;
        _currentStart = _position;
        _currentEnd = _position + length;
        _currentExpandable = false;
        _position += length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCurrentFrom(TokenKind kind, int start, bool expandable = false)
    {
        _currentKind = kind;
        _currentStart = start;
        _currentEnd = _position;
        _currentExpandable = expandable;
    }

    /// <summary>
    ///  Scans a property expression of the form $(propertyname).
    /// </summary>
    private bool TryScanProperty()
    {
        int start = _position;

        Consume('$');

        if (!TryConsume('('))
        {
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

                if (!TryScanProperty())
                {
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
                        return SetErrorInfo(pos, "IllFormedPropertySpaceInCondition");
                    }

                    _position++;
                    SetCurrentFrom(TokenKind.Property, start);
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

        return SetErrorInfo(start, "IllFormedPropertyCloseParenthesisInCondition");
    }

    /// <summary>
    ///  Scans an item metadata expression of the form %(metadataname).
    /// </summary>
    private bool TryScanItemMetadata()
    {
        int start = _position;

        Consume('%');

        if (!TryConsume('('))
        {
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
                    return false;
                }

                SetCurrentFrom(TokenKind.ItemMetadata, start);
                return true;
            }

            if (At(' '))
            {
                return SetErrorInfo(_position, "IllFormedMetadataSpaceInCondition");
            }

            _position++;
        }

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
    private bool TryScanItemList()
    {
        int start = _position;

        Consume('@');

        if (!TryConsume('('))
        {
            return SetErrorInfo(start, "IllFormedItemListOpenParenthesisInCondition");
        }

        if ((_options & ParserOptions.AllowItemLists) == 0)
        {
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
                    SetCurrentFrom(TokenKind.ItemList, start);
                    return true;
                }

                parenCount--;
                continue;
            }

            _position++;
        }

        return SetErrorInfo(
            start,
            inReplacement ? "IllFormedItemListQuoteInCondition" : "IllFormedItemListCloseParenthesisInCondition");
    }

    /// <summary>
    ///  Scans a quoted string that may contain property, item, or metadata expressions.
    /// </summary>
    private bool TryScanQuotedString()
    {
        bool expandable = false;
        int start = _position;

        Consume('\'');

        while (!AtEnd)
        {
            if (TryConsume('\''))
            {
                // The text segment excludes the surrounding quotes.
                _currentStart = start + 1;
                _currentEnd = _position - 1;

                if (!expandable)
                {
                    StringSegment text = Segment(_currentStart, _currentEnd - _currentStart);

                    if (ConversionUtilities.ValidBooleanTrue(text))
                    {
                        _currentKind = TokenKind.True;
                        _currentExpandable = false;
                        return true;
                    }

                    if (ConversionUtilities.ValidBooleanFalse(text))
                    {
                        _currentKind = TokenKind.False;
                        _currentExpandable = false;
                        return true;
                    }
                }

                _currentKind = TokenKind.String;
                _currentExpandable = expandable;
                return true;
            }

            if (At("%("))
            {
                if (!TryScanItemMetadata())
                {
                    return false;
                }

                expandable = true;
                continue;
            }

            if (At("@("))
            {
                if (!TryScanItemList())
                {
                    return false;
                }

                expandable = true;
                continue;
            }

            if (At("$("))
            {
                if (!TryScanProperty())
                {
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

        return SetErrorInfo(start, "IllFormedQuotedStringInCondition");
    }

    // There is a bug here that spaces are not required around 'and' and 'or'. For example,
    // this works perfectly well:
    // Condition="%(a.Identity)!=''and%(a.m)=='1'"
    // Since people now depend on this behavior, we must not change it.
    private bool TryScanIdentifier()
    {
        int start = _position;

        if (!CharacterUtilities.IsIdentifierStart(_expression[start]))
        {
            return false;
        }

        ScanIdentifierChars();

        StringSegment identifier = SegmentFrom(start);

        // Check for 'and'/'or' keywords
        if (identifier is ['o' or 'O', 'r' or 'R'])
        {
            SetCurrentFrom(TokenKind.Or, start);
            return true;
        }

        if (identifier is ['a' or 'A', 'n' or 'N', 'd' or 'D'])
        {
            SetCurrentFrom(TokenKind.And, start);
            return true;
        }

        // Check for boolean keywords (true/false/on/off/yes/no)
        if (ConversionUtilities.ValidBooleanTrue(identifier))
        {
            SetCurrentFrom(TokenKind.True, start);
            return true;
        }

        if (ConversionUtilities.ValidBooleanFalse(identifier))
        {
            SetCurrentFrom(TokenKind.False, start);
            return true;
        }

        int end = _position;

        SkipWhiteSpace();

        _currentKind = At('(') ? TokenKind.FunctionName : TokenKind.String;
        _currentStart = start;
        _currentEnd = end;
        _currentExpandable = false;
        return true;
    }

    private bool TryScanNumber()
    {
        int start = _position;
        StringSegment remaining = Remaining;

        if (!CharacterUtilities.IsNumberStart(remaining[0]))
        {
            return false;
        }

        if (remaining is ['0', ('x' or 'X'), ..])
        {
            _position += 2;
            ScanHexDigits();
            SetCurrentFrom(TokenKind.Number, start);
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

        SetCurrentFrom(TokenKind.Number, start);
        return true;
    }
}
