// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// This class implements the grammar for complex conditionals.
///
/// The usage is:
///    GenericExpressionNode tree = Parser.Parse(expression, options, elementLocation);
///
/// The expression tree can then be evaluated and re-evaluated as needed.
/// </summary>
internal sealed class Parser
{
    public readonly struct Error(string resourceName, int position, object[] formatArgs)
    {
        public int Position => position;
        public string ResourceName => resourceName;
        public object[] FormatArgs => formatArgs ?? [];
    }

    private static string EndOfInput => field ??= ResourceUtilities.GetResourceString("EndOfInputTokenName");

    private static readonly FrozenDictionary<string, KeywordKind> s_keywords = new Dictionary<string, KeywordKind>(StringComparer.OrdinalIgnoreCase)
    {
        { "and", KeywordKind.And },
        { "or", KeywordKind.Or },
        { "true", KeywordKind.True },
        { "false", KeywordKind.False },
        { "on", KeywordKind.On },
        { "off", KeywordKind.Off },
        { "yes", KeywordKind.Yes },
        { "no", KeywordKind.No }
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private readonly string _expression;
    private readonly ParserOptions _options;
    private readonly IElementLocation _elementLocation;
    private readonly bool _throwException;

    private readonly ILoggingService? _loggingService;
    private readonly BuildEventContext _logBuildEventContext;

    private int _position;
    private Error? _error;

    private bool _warnedForExpression;

    private enum KeywordKind
    {
        And,
        Or,
        True,
        False,
        On,
        Off,
        Yes,
        No
    }

    private Parser(
        string expression,
        ParserOptions options,
        bool throwException,
        IElementLocation elementLocation,
        LoggingContext? loggingContext = null)
    {
        // We currently have no support (and no scenarios) for disallowing property references in conditions.
        ErrorUtilities.VerifyThrow((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

        _expression = expression;
        _options = options;
        _throwException = throwException;
        _elementLocation = elementLocation;
        _position = 0;

        _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
        _loggingService = loggingContext?.LoggingService;
    }

    private void SetErrorOrThrow(string resourceName, int errorPosition = -1, string? unexpectedlyFound = null)
    {
        // We already have an error recorded.
        if (_error is not null)
        {
            return;
        }

        if (errorPosition == -1)
        {
            errorPosition = _position;
        }

        // Convert to 1-based
        errorPosition++;

        object[] formatArgs = unexpectedlyFound is not null
            ? [_expression, errorPosition, unexpectedlyFound]
            : [_expression, errorPosition];

        _error = new Error(resourceName, errorPosition, formatArgs);

        if (_throwException)
        {
            ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, resourceName, formatArgs);
        }
    }

    private void SetUnexpectedToken()
    {
        string unexpectedlyFound = _position < _expression.Length ? _expression[_position].ToString() : EndOfInput;

        SetErrorOrThrow("UnexpectedTokenInCondition", unexpectedlyFound: unexpectedlyFound);
    }

    /// <summary>
    /// Main entry point for parser.
    /// You pass in the expression you want to parse, and you get an
    /// expression tree out the back end.
    /// </summary>
    public static GenericExpressionNode Parse(
        string expression,
        ParserOptions options,
        IElementLocation elementLocation,
        LoggingContext? loggingContext = null)
    {
        var parser = new Parser(expression, options, throwException: true, elementLocation, loggingContext);

        var result = parser.ParseCore();
        ErrorUtilities.VerifyThrowInternalNull(result);

        return result;
    }

    public static bool TryParse(
        string expression,
        ParserOptions options,
        IElementLocation elementLocation,
        [NotNullWhen(true)] out GenericExpressionNode? node,
        out Error? error)
    {
        var parser = new Parser(expression, options, throwException: false, elementLocation);

        node = parser.ParseCore();
        error = parser._error;

        return error is null;
    }

    /// <summary>
    /// Parses the expression and returns the expression tree.
    /// </summary>
    private GenericExpressionNode? ParseCore()
    {
        if (!TryParseOrExpression(out GenericExpressionNode? node))
        {
            return null;
        }

        SkipWhiteSpace();

        if (!IsAtEnd())
        {
            SetUnexpectedToken();
        }

        return node;
    }

    //
    // Top node of grammar
    //    See grammar for how the following methods relate to each
    //    other.
    //
    private bool TryParseOrExpression([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        if (!TryParseAndExpression(out node))
        {
            return false;
        }

        SkipWhiteSpace();

        while (TryMatchKeyword(KeywordKind.Or))
        {
            if (!TryParseAndExpression(out GenericExpressionNode? rhs))
            {
                return false;
            }

            OperatorExpressionNode orNode = new OrExpressionNode();
            orNode.LeftChild = node;
            orNode.RightChild = rhs;
            node = orNode;

            SkipWhiteSpace();
        }

        #region REMOVE_COMPAT_WARNING
        // Check for potential change in behavior
        if (_loggingService != null && !_warnedForExpression &&
            node.PotentialAndOrConflict())
        {
            // We only want to warn once even if there multiple () sub expressions
            _warnedForExpression = true;

            // Log a warning regarding the fact the expression may have been evaluated
            // incorrectly in earlier version of MSBuild
            _loggingService.LogWarning(_logBuildEventContext, null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", _expression);
        }
        #endregion

        return true;
    }

    private bool TryParseAndExpression([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        if (!TryParseComparisonExpression(out node))
        {
            return false;
        }

        SkipWhiteSpace();

        while (TryMatchKeyword(KeywordKind.And))
        {
            if (!TryParseComparisonExpression(out GenericExpressionNode? rhs))
            {
                return false;
            }

            OperatorExpressionNode andNode = new AndExpressionNode();
            andNode.LeftChild = node;
            andNode.RightChild = rhs;
            node = andNode;

            SkipWhiteSpace();
        }

        return node is not null;
    }

    private bool TryParseComparisonExpression([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        node = null;

        if (!TryParseUnaryExpression(out GenericExpressionNode? lhs))
        {
            return false;
        }

        SkipWhiteSpace();

        if (IsAtEnd())
        {
            node = lhs;
            return true;
        }

        if (!TryParseRelationalOperator(out OperatorExpressionNode? opNode))
        {
            node = lhs;
            return true;
        }

        if (!TryParseUnaryExpression(out GenericExpressionNode? rhs))
        {
            return false;
        }

        opNode.LeftChild = lhs;
        opNode.RightChild = rhs;
        node = opNode;

        return true;
    }

    private bool TryParseRelationalOperator([NotNullWhen(true)] out OperatorExpressionNode? node)
    {
        node = null;

        if (IsAtEnd())
        {
            return false;
        }

        char ch = _expression[_position];

        switch (ch)
        {
            case '<':
                _position++;
                if (!IsAtEnd() && _expression[_position] == '=')
                {
                    _position++;
                    node = new LessThanOrEqualExpressionNode();
                }
                else
                {
                    node = new LessThanExpressionNode();
                }

                return true;

            case '>':
                _position++;
                if (!IsAtEnd() && _expression[_position] == '=')
                {
                    _position++;
                    node = new GreaterThanOrEqualExpressionNode();
                }
                else
                {
                    node = new GreaterThanExpressionNode();
                }

                return true;

            case '=':
                if (_position + 1 < _expression.Length && _expression[_position + 1] == '=')
                {
                    _position += 2;
                    node = new EqualExpressionNode();
                    return true;
                }

                // Single '=' is an error - point to the character after it
                int errorPosition = _position + 1;
                string unexpectedlyFound = _position + 1 < _expression.Length
                    ? _expression[_position + 1].ToString()
                    : EndOfInput;

                SetErrorOrThrow("IllFormedEqualsInCondition", errorPosition, unexpectedlyFound);
                _position++;
                return false;

            case '!':
                if (_position + 1 < _expression.Length && _expression[_position + 1] == '=')
                {
                    _position += 2;
                    node = new NotEqualExpressionNode();
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private bool TryParseUnaryExpression([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        node = null;

        SkipWhiteSpace();

        if (IsAtEnd())
        {
            SetUnexpectedToken();
            return false;
        }

        char ch = _expression[_position];

        // Check for '!' (not operator)
        if (ch == '!')
        {
            // Check if it's != (handled by relational operator)
            if (_position + 1 < _expression.Length && _expression[_position + 1] == '=')
            {
                SetUnexpectedToken();
                return false;
            }

            _position++;
            if (!TryParseUnaryExpression(out GenericExpressionNode? expr))
            {
                return false;
            }

            OperatorExpressionNode notNode = new NotExpressionNode();
            notNode.LeftChild = expr;
            node = notNode;

            return true;
        }

        // Check for '(' (grouped expression)
        if (ch == '(')
        {
            _position++;
            if (!TryParseOrExpression(out GenericExpressionNode? child))
            {
                return false;
            }

            SkipWhiteSpace();

            if (IsAtEnd() || _expression[_position] != ')')
            {
                SetUnexpectedToken();
                return false;
            }

            _position++;
            node = child;
            return true;
        }

        // Try to parse an argument (string, number, property, etc.) or function
        return TryParseFunctionCallOrIdentifier(out node);
    }

    private bool TryParseFunctionCallOrIdentifier([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        node = null;

        SkipWhiteSpace();

        if (IsAtEnd())
        {
            SetUnexpectedToken();
            return false;
        }

        char ch = _expression[_position];

        // Check for function call or simple string: identifier followed optionally by '('
        if (CharacterUtilities.IsSimpleStringStart(ch))
        {
            int start = _position;
            SkipSimpleStringChars();

            ReadOnlySpan<char> span = _expression.AsSpan(start, _position - start);

            // Look ahead for '(' to detect function call
            int savedPosition = _position;
            SkipWhiteSpace();

            if (!IsAtEnd() && _expression[_position] == '(')
            {
                // It's a function call - use the string as function name regardless of whether it's a keyword
                string functionName = span.ToString();
                _position++; // consume '('

                List<GenericExpressionNode>? arglist = null;

                SkipWhiteSpace();

                if (IsAtEnd() || _expression[_position] != ')')
                {
                    if (!TryParseArgumentList(out arglist))
                    {
                        return false;
                    }
                }

                SkipWhiteSpace();

                if (IsAtEnd() || _expression[_position] != ')')
                {
                    SetUnexpectedToken();
                    return false;
                }

                _position++;
                node = new FunctionCallExpressionNode(functionName, arglist ?? []);
                return true;
            }

            // Not a function - restore position and check if it's a keyword
            _position = savedPosition;

            if (TryMatchKeywordSpan(span, out KeywordKind keyword))
            {
                node = keyword switch
                {
                    KeywordKind.True or KeywordKind.On or KeywordKind.Yes => new BooleanLiteralNode(true, span.ToString()),
                    KeywordKind.False or KeywordKind.Off or KeywordKind.No => new BooleanLiteralNode(false, span.ToString()),
                    KeywordKind.And or KeywordKind.Or => null, // Reserved keywords - not valid as values
                    _ => new StringExpressionNode(span.ToString(), false)
                };
            }
            else
            {
                // Just a simple string
                node = new StringExpressionNode(span.ToString(), false);
            }

            if (node is null)
            {
                SetUnexpectedToken();
                return false;
            }

            return node is not null;
        }

        // Fall back to other argument types
        return TryParseArgument(out node);
    }

    private bool TryParseArgumentList([NotNullWhen(true)] out List<GenericExpressionNode>? arglist)
    {
        arglist = null;

        while (true)
        {
            if (!TryParseArgument(out GenericExpressionNode? arg))
            {
                if (IsAtEnd() || _expression[_position] != ')')
                {
                    SetUnexpectedToken();
                    return false;
                }

                return false;
            }

            arglist ??= [];
            arglist.Add(arg);

            SkipWhiteSpace();

            if (IsAtEnd() || _expression[_position] != ',')
            {
                break;
            }

            _position++; // consume ','
            SkipWhiteSpace();
        }

        arglist ??= [];
        return true;
    }

    private bool TryParseArgument([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        node = null;

        SkipWhiteSpace();

        if (IsAtEnd())
        {
            return false;
        }

        char ch = _expression[_position];

        // Property reference: $(...)
        if (ch == '$')
        {
            if (TryParsePropertyOrItemMetadata(out string? propertyExpression))
            {
                node = new StringExpressionNode(propertyExpression, expandable: true);
            }

            return node is not null;
        }

        // Item metadata: %(...)
        if (ch == '%')
        {
            if (TryParsePropertyOrItemMetadata(out string? itemMetadataExpression))
            {
                string expression = itemMetadataExpression;

                // Extract metadata name for validation
                if (expression.Length > 3 && expression[0] == '%' && expression[1] == '(' && expression[^1] == ')')
                {
                    string metadataName = expression.Substring(2, expression.Length - 3);

                    // If it's like %(a.b) find 'b'
                    int period = metadataName.IndexOf('.');
                    if (period > 0 && period < metadataName.Length - 1)
                    {
                        metadataName = metadataName.Substring(period + 1);
                    }

                    if (!CheckMetadataAllowed(metadataName))
                    {
                        return false;
                    }
                }

                node = new StringExpressionNode(itemMetadataExpression, expandable: true);
            }

            return node is not null;
        }

        // Item list: @(...)
        if (ch == '@')
        {
            if ((_options & ParserOptions.AllowItemLists) == 0)
            {
                if (_position + 1 < _expression.Length && _expression[_position + 1] == '(')
                {
                    SetErrorOrThrow("ItemListNotAllowedInThisConditional");
                    return false;
                }
            }

            if (TryParseItemList(out string? itemListExpression))
            {
                node = new StringExpressionNode(itemListExpression, expandable: true);
            }

            return node is not null;
        }

        // Quoted string: '...'
        if (ch == '\'')
        {
            if (TryParseQuotedString(out string? stringValue, out bool expandable))
            {
                node = new StringExpressionNode(stringValue, expandable);
            }

            return node is not null;
        }

        // Numeric literal
        if (CharacterUtilities.IsNumberStart(ch))
        {
            if (TryParseNumeric(out string? numericValue))
            {
                node = new NumericExpressionNode(numericValue);
            }

            return node is not null;
        }

        // Simple string or keyword (but NOT function - ParseArgumentOrFunction handles that)
        if (CharacterUtilities.IsSimpleStringStart(ch))
        {
            int start = _position;
            SkipSimpleStringChars();

            ReadOnlySpan<char> span = _expression.AsSpan(start, _position - start);

            // Check if it's a boolean keyword
            if (TryMatchKeywordSpan(span, out KeywordKind keyword))
            {
                node = keyword switch
                {
                    KeywordKind.True or KeywordKind.On or KeywordKind.Yes => new BooleanLiteralNode(true, span.ToString()),
                    KeywordKind.False or KeywordKind.Off or KeywordKind.No => new BooleanLiteralNode(false, span.ToString()),
                    KeywordKind.And or KeywordKind.Or => null, // Reserved keywords - not valid as values
                    _ => new StringExpressionNode(span.ToString(), false)
                };
            }
            else
            {
                // Just a simple string
                node = new StringExpressionNode(span.ToString(), false);
            }

            return node is not null;
        }

        SetUnexpectedToken();
        return false;
    }

    private bool TryParsePropertyOrItemMetadata([NotNullWhen(true)] out string? result)
    {
        result = null;
        int start = _position;
        _position++;

        if (IsAtEnd() || _expression[_position] != '(')
        {
            string unexpectedlyFound = IsAtEnd() ? EndOfInput : _expression[_position].ToString();
            SetErrorOrThrow("IllFormedPropertyOpenParenthesisInCondition", start, unexpectedlyFound);
            return false;
        }

        if (!ScanForPropertyExpressionEnd(_expression, _position++, out int indexResult))
        {
            string unexpectedlyFound = _expression[indexResult].ToString();
            SetErrorOrThrow("IllFormedPropertySpaceInCondition", indexResult, unexpectedlyFound);
            return false;
        }

        _position = indexResult;

        if (IsAtEnd())
        {
            SetErrorOrThrow("IllFormedPropertyCloseParenthesisInCondition", start, EndOfInput);
            return false;
        }

        _position++;
        result = _expression.Substring(start, _position - start);
        return true;
    }

    private static bool ScanForPropertyExpressionEnd(string expression, int index, out int indexResult)
    {
        int nestLevel = 0;
        bool whitespaceFound = false;
        bool nonIdentifierCharacterFound = false;
        indexResult = -1;
        unsafe
        {
            fixed (char* pchar = expression)
            {
                while (index < expression.Length)
                {
                    char character = pchar[index];
                    if (character == '(')
                    {
                        nestLevel++;
                    }
                    else if (character == ')')
                    {
                        nestLevel--;
                    }
                    else if (char.IsWhiteSpace(character))
                    {
                        whitespaceFound = true;
                        indexResult = index;
                    }
                    else if (!XmlUtilities.IsValidSubsequentElementNameCharacter(character))
                    {
                        nonIdentifierCharacterFound = true;
                    }

                    if (character == '$' && index < expression.Length - 1 && pchar[index + 1] == '(')
                    {
                        if (!ScanForPropertyExpressionEnd(expression, index + 1, out index))
                        {
                            indexResult = index;
                            return false;
                        }
                    }

                    if (nestLevel == 0)
                    {
                        if (whitespaceFound && !nonIdentifierCharacterFound)
                        {
                            return false;
                        }

                        indexResult = index;
                        return true;
                    }
                    else
                    {
                        index++;
                    }
                }
            }
        }
        indexResult = index;
        return true;
    }

    private bool CheckMetadataAllowed(string metadataName)
    {
        if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
        {
            return true;
        }

        bool isItemSpecModifier = FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName);

        if (((_options & ParserOptions.AllowBuiltInMetadata) == 0) && isItemSpecModifier)
        {
            SetErrorOrThrow("BuiltInMetadataNotAllowedInThisConditional", unexpectedlyFound: metadataName);
            return false;
        }

        if (((_options & ParserOptions.AllowCustomMetadata) == 0) && !isItemSpecModifier)
        {
            SetErrorOrThrow("CustomMetadataNotAllowedInThisConditional", unexpectedlyFound: metadataName);
            return false;
        }

        return true;
    }

    private bool TryParseItemList([NotNullWhen(true)] out string? result)
    {
        result = null;
        int start = _position;
        _position++;

        if (IsAtEnd() || _expression[_position] != '(')
        {
            SetErrorOrThrow("IllFormedItemListOpenParenthesisInCondition", start);
            return false;
        }

        _position++;

        bool inReplacement = false;
        int parenToClose = 0;

        while (_position < _expression.Length)
        {
            if (_expression[_position] == '\'')
            {
                inReplacement = !inReplacement;
            }
            else if (_expression[_position] == '(' && !inReplacement)
            {
                parenToClose++;
            }
            else if (_expression[_position] == ')' && !inReplacement)
            {
                if (parenToClose == 0)
                {
                    break;
                }

                parenToClose--;
            }

            _position++;
        }

        if (IsAtEnd())
        {
            SetErrorOrThrow(inReplacement ? "IllFormedItemListQuoteInCondition" : "IllFormedItemListCloseParenthesisInCondition", start);
            return false;
        }

        _position++;
        result = _expression.Substring(start, _position - start);
        return true;
    }

    private bool TryParseQuotedString([NotNullWhen(true)] out string? result, out bool expandable)
    {
        result = null;
        expandable = false;

        _position++; // Skip opening quote
        int start = _position;

        while (_position < _expression.Length && _expression[_position] != '\'')
        {
            char ch = _expression[_position];

            if (ch == '%')
            {
                if (_position + 1 < _expression.Length && _expression[_position + 1] == '(')
                {
                    expandable = true;

                    // Extract metadata name for validation
                    int endOfName = _expression.IndexOf(')', _position) - 1;
                    if (endOfName >= _position + 2)
                    {
                        string name = _expression.Substring(_position + 2, endOfName - _position - 1);
                        if (!CheckMetadataAllowed(name))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    // Standalone % for escaping
                    expandable = true;
                }
            }
            else if (ch == '@' && _position + 1 < _expression.Length && _expression[_position + 1] == '(')
            {
                expandable = true;

                if ((_options & ParserOptions.AllowItemLists) == 0)
                {
                    SetErrorOrThrow("ItemListNotAllowedInThisConditional", start);
                    return false;
                }

                // Parse the item list to skip over it properly
                int savedPos = _position;
                if (!TryParseItemList(out _))
                {
                    return false;
                }

                continue;
            }
            else if (ch == '$' && _position + 1 < _expression.Length && _expression[_position + 1] == '(')
            {
                expandable = true;
            }

            _position++;
        }

        if (IsAtEnd())
        {
            SetErrorOrThrow("IllFormedQuotedStringInCondition", start);
            return false;
        }

        result = _expression.Substring(start, _position - start);
        _position++; // Skip closing quote
        return true;
    }

    private bool TryParseNumeric([NotNullWhen(true)] out string? result)
    {
        result = null;
        int start = _position;

        // Hex number: 0x...
        if (_position + 2 < _expression.Length &&
            _expression[_position] == '0' &&
            (_expression[_position + 1] == 'x' || _expression[_position + 1] == 'X'))
        {
            _position += 2;
            SkipHexDigits();
            result = _expression.Substring(start, _position - start);
            return true;
        }

        // Decimal number
        if (_expression[_position] == '+' || _expression[_position] == '-')
        {
            _position++;
        }

        do
        {
            SkipDigits();
            if (_position < _expression.Length && _expression[_position] == '.')
            {
                _position++;
            }
            if (_position < _expression.Length)
            {
                SkipDigits();
            }
        }
        while (_position < _expression.Length && _expression[_position] == '.');

        result = _expression.Substring(start, _position - start);
        return true;
    }

    private bool TryMatchKeyword(KeywordKind keyword)
    {
        if (IsAtEnd())
        {
            return false;
        }

        int savedPosition = _position;

        if (!CharacterUtilities.IsSimpleStringStart(_expression[_position]))
        {
            return false;
        }

        int start = _position;
        SkipSimpleStringChars();

        ReadOnlySpan<char> span = _expression.AsSpan(start, _position - start);

        if (TryMatchKeywordSpan(span, out KeywordKind found) && found == keyword)
        {
            return true;
        }

        _position = savedPosition;
        return false;
    }

    private bool TryMatchKeywordSpan(ReadOnlySpan<char> span, out KeywordKind keyword)
    {
#if NET
        if (s_keywords.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(span, out keyword))
        {
            return true;
        }
#else
        foreach (KeyValuePair<string, KeywordKind> pair in s_keywords)
        {
            if (span.Equals(pair.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                keyword = pair.Value;
                return true;
            }
        }
#endif
        keyword = default;
        return false;
    }

    private void SkipWhiteSpace()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipDigits()
    {
        while (_position < _expression.Length && char.IsDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipHexDigits()
    {
        while (_position < _expression.Length && CharacterUtilities.IsHexDigit(_expression[_position]))
        {
            _position++;
        }
    }

    private void SkipSimpleStringChars()
    {
        while (_position < _expression.Length && CharacterUtilities.IsSimpleStringChar(_expression[_position]))
        {
            _position++;
        }
    }

    private bool IsAtEnd()
    {
        return _position >= _expression.Length;
    }
}
