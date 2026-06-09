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
            TokenKind.LessThan => new RelationalOperatorNode(RelationalOperationKind.LessThan, lhs, rhs),
            TokenKind.GreaterThan => new RelationalOperatorNode(RelationalOperationKind.GreaterThan, lhs, rhs),
            TokenKind.LessThanOrEqualTo => new RelationalOperatorNode(RelationalOperationKind.LessThanOrEqual, lhs, rhs),
            TokenKind.GreaterThanOrEqualTo => new RelationalOperatorNode(RelationalOperationKind.GreaterThanOrEqual, lhs, rhs),
            TokenKind.EqualTo => new EqualityOperatorNode(negate: false, lhs, rhs),
            TokenKind.NotEqualTo => new EqualityOperatorNode(negate: true, lhs, rhs),

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
        int start = _currentStart;
        int length = _currentEnd - _currentStart;

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

            if (IdentifierEquals(start, length, "Exists"))
            {
                result = new ExistsCallNode(arglist);
                return true;
            }

            if (IdentifierEquals(start, length, "HasTrailingSlash"))
            {
                result = new HasTrailingSlashCallNode(arglist);
                return true;
            }

            _errorPosition = _position + 1;
            _errorResource = "UndefinedFunctionCall";
            _errorArgs = [_expression, _expression.Substring(start, length)];
            result = null;
            return false;
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
            result = expandable
                ? new ExpandableStringNode(Segment(start, end - start))
                : new StringLiteralNode(Segment(start, end - start));
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
            result = new ExpandableStringNode(Segment(start, end - start));
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
    ///  Returns a segment of the expression from <paramref name="start"/> with the given <paramref name="length"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly StringSegment Segment(int start, int length)
        => new(_expression, start, length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool At(char c)
        => !AtEnd && _expression[_position] == c;

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
        if (At(c))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool IdentifierEquals(int start, int length, string keyword)
        => length == keyword.Length && string.Compare(_expression, start, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>
    ///  Matches the text at the given position to a boolean literal keyword.
    ///  Handles true/false/on/off/yes/no and their negated forms (!true/!false/!on/!off/!yes/!no).
    ///  Returns <see cref="TokenKind.None"/> if the text does not match any boolean keyword.
    ///  The '| 0x20' trick converts ASCII uppercase letters to lowercase by setting bit 5,
    ///  allowing a single switch case per letter instead of matching both cases.
    ///  Non-letter characters like '!' are unaffected since bit 5 is already set.
    /// </summary>
    private readonly TokenKind GetBooleanLiteralKind(int start, int length)
    {
        switch (length)
        {
            case 2:
                switch (_expression[start] | 0x20)
                {
                    case 'o': // "on"
                        return (_expression[start + 1] | 0x20) == 'n'
                            ? TokenKind.True
                            : TokenKind.None;

                    case 'n': // "no"
                        return (_expression[start + 1] | 0x20) == 'o'
                            ? TokenKind.False
                            : TokenKind.None;
                }

                return TokenKind.None;

            case 3:
                switch (_expression[start] | 0x20)
                {
                    case 'y': // "yes"
                        return (_expression[start + 1] | 0x20) == 'e'
                            && (_expression[start + 2] | 0x20) == 's'
                            ? TokenKind.True
                            : TokenKind.None;

                    case 'o': // "off"
                        return (_expression[start + 1] | 0x20) == 'f'
                            && (_expression[start + 2] | 0x20) == 'f'
                            ? TokenKind.False
                            : TokenKind.None;

                    case '!':
                        switch (_expression[start + 1] | 0x20)
                        {
                            case 'n': // "!no"
                                return (_expression[start + 2] | 0x20) == 'o'
                                    ? TokenKind.True
                                    : TokenKind.None;

                            case 'o': // "!on"
                                return (_expression[start + 2] | 0x20) == 'n'
                                    ? TokenKind.False
                                    : TokenKind.None;
                        }

                        return TokenKind.None;
                }

                return TokenKind.None;

            case 4:
                switch (_expression[start] | 0x20)
                {
                    case 't': // "true"
                        return (_expression[start + 1] | 0x20) == 'r'
                            && (_expression[start + 2] | 0x20) == 'u'
                            && (_expression[start + 3] | 0x20) == 'e'
                            ? TokenKind.True
                            : TokenKind.None;

                    case '!':
                        switch (_expression[start + 1] | 0x20)
                        {
                            case 'o': // "!off"
                                return (_expression[start + 2] | 0x20) == 'f'
                                    && (_expression[start + 3] | 0x20) == 'f'
                                    ? TokenKind.True
                                    : TokenKind.None;

                            case 'y': // "!yes"
                                return (_expression[start + 2] | 0x20) == 'e'
                                    && (_expression[start + 3] | 0x20) == 's'
                                    ? TokenKind.False
                                    : TokenKind.None;
                        }

                        return TokenKind.None;
                }

                return TokenKind.None;

            case 5:
                switch (_expression[start] | 0x20)
                {
                    case 'f': // "false"
                        return (_expression[start + 1] | 0x20) == 'a'
                            && (_expression[start + 2] | 0x20) == 'l'
                            && (_expression[start + 3] | 0x20) == 's'
                            && (_expression[start + 4] | 0x20) == 'e'
                            ? TokenKind.False
                            : TokenKind.None;

                    case '!': // "!true"
                        return (_expression[start + 1] | 0x20) == 't'
                            && (_expression[start + 2] | 0x20) == 'r'
                            && (_expression[start + 3] | 0x20) == 'u'
                            && (_expression[start + 4] | 0x20) == 'e'
                            ? TokenKind.False
                            : TokenKind.None;
                }

                return TokenKind.None;

            case 6: // "!false"
                return _expression[start] == '!'
                    && (_expression[start + 1] | 0x20) == 'f'
                    && (_expression[start + 2] | 0x20) == 'a'
                    && (_expression[start + 3] | 0x20) == 'l'
                    && (_expression[start + 4] | 0x20) == 's'
                    && (_expression[start + 5] | 0x20) == 'e'
                    ? TokenKind.True
                    : TokenKind.None;

            default:
                return TokenKind.None;
        }
    }

    /// <summary>
    ///  Returns true if the text at the given position represents a numeric literal.
    ///  Matches the same patterns as <see cref="TryScanNumber"/>: decimal numbers with
    ///  optional sign and decimal point(s), or hexadecimal numbers with "0x" prefix.
    /// </summary>
    private readonly bool IsNumberLiteral(int start, int length)
    {
        if (length == 0)
        {
            return false;
        }

        int pos = start;
        int end = start + length;
        char c = _expression[pos];

        // Hex: 0x...
        if (c == '0' && pos + 1 < end && (_expression[pos + 1] | 0x20) == 'x')
        {
            pos += 2;

            // Must have at least one hex digit.
            if (pos >= end || !CharacterUtilities.IsHexDigit(_expression[pos]))
            {
                return false;
            }

            while (pos < end && CharacterUtilities.IsHexDigit(_expression[pos]))
            {
                pos++;
            }

            return pos == end;
        }

        // Optional leading sign
        if (c is '+' or '-')
        {
            pos++;

            if (pos >= end)
            {
                return false;
            }
        }

        // Must have at least one digit or a decimal point followed by digits.
        if (!char.IsDigit(_expression[pos]) && _expression[pos] != '.')
        {
            return false;
        }

        // Scan digits and decimal points (matches TryScanNumber's loop structure).
        while (pos < end)
        {
            if (char.IsDigit(_expression[pos]))
            {
                pos++;
            }
            else if (_expression[pos] == '.')
            {
                pos++;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

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
            SetCurrentAndAdvance(TokenKind.EndOfInput, length: 0);
            return true;
        }

        switch (_expression[_position])
        {
            case ',':
                SetCurrentAndAdvance(TokenKind.Comma, length: 1);
                break;

            case '(':
                SetCurrentAndAdvance(TokenKind.LeftParenthesis, length: 1);
                break;

            case ')':
                SetCurrentAndAdvance(TokenKind.RightParenthesis, length: 1);
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
                    SetCurrentAndAdvance(TokenKind.NotEqualTo, length: 2);
                }
                else
                {
                    SetCurrentAndAdvance(TokenKind.Not, length: 1);
                }

                break;

            case '>':
                if (PeekNext('='))
                {
                    SetCurrentAndAdvance(TokenKind.GreaterThanOrEqualTo, length: 2);
                }
                else
                {
                    SetCurrentAndAdvance(TokenKind.GreaterThan, length: 1);
                }

                break;

            case '<':
                if (PeekNext('='))
                {
                    SetCurrentAndAdvance(TokenKind.LessThanOrEqualTo, length: 2);
                }
                else
                {
                    SetCurrentAndAdvance(TokenKind.LessThan, length: 1);
                }

                break;

            case '=':
                if (PeekNext('='))
                {
                    SetCurrentAndAdvance(TokenKind.EqualTo, length: 2);
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
    private void SetCurrentAndAdvance(TokenKind kind, int length)
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
            char c = _expression[_position];

            switch (c)
            {
                case '$':
                    // Handle nested property expressions by recursing into TryScanProperty.
                    if (PeekNext('('))
                    {
                        nonIdentifierCharacterFound = true;

                        if (!TryScanProperty())
                        {
                            return false;
                        }

                        continue;
                    }

                    nonIdentifierCharacterFound = true;
                    break;

                case '(':
                    nestLevel++;
                    nonIdentifierCharacterFound = true;
                    break;

                case ')':
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

                    break;

                default:
                    if (char.IsWhiteSpace(c))
                    {
                        whitespacePosition ??= _position;
                    }
                    else if (!XmlUtilities.IsValidSubsequentElementNameCharacter(c))
                    {
                        nonIdentifierCharacterFound = true;
                    }

                    break;
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
                // Verify that metadata references are allowed by the current parser options.
                if ((_options & ParserOptions.AllowItemMetadata) != ParserOptions.AllowItemMetadata)
                {
                    StringSegment name = Segment(start + 2, _position - start - 3);

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
            switch (_expression[_position])
            {
                case '\'':
                    inReplacement = !inReplacement;
                    break;

                case '(' when !inReplacement:
                    parenCount++;
                    break;

                case ')' when !inReplacement:
                    if (parenCount == 0)
                    {
                        _position++;
                        SetCurrentFrom(TokenKind.ItemList, start);
                        return true;
                    }

                    parenCount--;
                    break;
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
            switch (_expression[_position])
            {
                case '\'':
                    _position++;

                    // The text segment excludes the surrounding quotes.
                    _currentStart = start + 1;
                    _currentEnd = _position - 1;

                    if (!expandable)
                    {
                        TokenKind boolKind = GetBooleanLiteralKind(_currentStart, _currentEnd - _currentStart);

                        if (boolKind != TokenKind.None)
                        {
                            _currentKind = boolKind;
                            _currentExpandable = false;
                            return true;
                        }

                        if (IsNumberLiteral(_currentStart, _currentEnd - _currentStart))
                        {
                            _currentKind = TokenKind.Number;
                            _currentExpandable = false;
                            return true;
                        }
                    }

                    _currentKind = TokenKind.String;
                    _currentExpandable = expandable;
                    return true;

                case '%':
                    if (PeekNext('('))
                    {
                        if (!TryScanItemMetadata())
                        {
                            return false;
                        }
                    }
                    else
                    {
                        _position++;
                    }

                    expandable = true;
                    break;

                case '$':
                    expandable = true;
                    _position++;
                    break;

                case '@':
                    if (PeekNext('('))
                    {
                        if (!TryScanItemList())
                        {
                            return false;
                        }

                        expandable = true;
                    }
                    else
                    {
                        _position++;
                    }

                    break;

                default:
                    _position++;
                    break;
            }
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

        int length = _position - start;

        // Check for 'and'/'or' keywords by length + first char.
        switch (length)
        {
            // "or"
            case 2:
                if ((_expression[start] | 0x20) == 'o'
                    && (_expression[start + 1] | 0x20) == 'r')
                {
                    SetCurrentFrom(TokenKind.Or, start);
                    return true;
                }

                break;

            // "and"
            case 3:
                if ((_expression[start] | 0x20) == 'a'
                    && (_expression[start + 1] | 0x20) == 'n'
                    && (_expression[start + 2] | 0x20) == 'd')
                {
                    SetCurrentFrom(TokenKind.And, start);
                    return true;
                }

                break;
        }

        // Check for boolean literal keywords (true/false/on/off/yes/no).
        // Negated forms can't appear here since '!' isn't a valid identifier character.
        TokenKind boolKind = GetBooleanLiteralKind(start, length);

        if (boolKind != TokenKind.None)
        {
            SetCurrentFrom(boolKind, start);
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
        char c = _expression[_position];

        if (!CharacterUtilities.IsNumberStart(c))
        {
            return false;
        }

        if (c == '0' && _position + 1 < _expression.Length && (_expression[_position + 1] | 0x20) == 'x')
        {
            _position += 2;
            ScanHexDigits();
            SetCurrentFrom(TokenKind.Number, start);
            return true;
        }

        if (c is '+' or '-')
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
