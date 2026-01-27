// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
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

    private enum FunctionKind
    {
        Exists,
        HasTrailingSlash
    }

    private sealed class FunctionDescriptor(FunctionKind kind, string name, int argumentCount)
    {
        public FunctionKind Kind => kind;
        public string Name => name;
        public int ArgumentCount => argumentCount;
    }

    public readonly struct Error(string resourceName, int position, object[] formatArgs)
    {
        public int Position => position;
        public string ResourceName => resourceName;
        public object[] FormatArgs => formatArgs ?? [];
    }

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

    private static readonly FrozenDictionary<string, FunctionDescriptor> s_functions = new Dictionary<string, FunctionDescriptor>(StringComparer.OrdinalIgnoreCase)
    {
        { "Exists", new(FunctionKind.Exists, "Exists", argumentCount: 1) },
        { "HasTrailingSlash", new(FunctionKind.HasTrailingSlash, "HasTrailingSlash", argumentCount: 1) }
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static string EndOfInput => field ??= ResourceUtilities.GetResourceString("EndOfInputTokenName");

    private readonly string _expression;
    private readonly ParserOptions _options;
    private readonly IElementLocation _elementLocation;
    private readonly bool _throwException;

    private readonly ILoggingService? _loggingService;
    private readonly BuildEventContext _logBuildEventContext;

    private int _position;
    private Error? _error;

    private bool _warnedForExpression;

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

    private bool AllowUnknownFunctions => (_options & ParserOptions.AllowUnknownFunctions) != 0;

    private bool TryReportError(string resourceName, int position, object[]? formatArgs = null)
    {
        // We've already recorded an error.
        if (_error is not null)
        {
            return false;
        }

        _error = new Error(resourceName, position, formatArgs ?? []);

        if (_throwException)
        {
            ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, resourceName, formatArgs);
        }

        return false;
    }

    private bool TryReportBuiltInMetadataNotAllowed(string name)
    {
        // Error position is 1-based
        int position = _position + 1;
        return TryReportError("BuiltInMetadataNotAllowedInThisConditional", position, [_expression, position, name]);
    }

    private bool TryReportCustomMetadataNotAllowed(string name)
    {
        // Error position is 1-based
        int position = _position + 1;
        return TryReportError("CustomMetadataNotAllowedInThisConditional", position, [_expression, position, name]);
    }

    private bool TryReportIllFormedEquals()
    {
        // Error position is 1-based
        int position = _position + 1;
        string nextChar = IsAtEnd() ? EndOfInput : _expression[_position].ToString();

        return TryReportError("IllFormedEqualsInCondition", position, [_expression, position, nextChar]);
    }

    private bool TryReportIllFormedItemListCloseParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError("IllFormedItemListCloseParenthesisInCondition", position, [_expression, position]);
    }

    private bool TryReportIllFormedItemListOpenParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError("IllFormedItemListOpenParenthesisInCondition", position, [_expression, position]);
    }

    private bool TryReportIllFormedItemListQuote(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError("IllFormedItemListQuoteInCondition", position, [_expression, position]);
    }

    private bool TryReportIllFormedPropertyCloseParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError("IllFormedPropertyCloseParenthesisInCondition", position, [_expression, position]);
    }

    private bool TryReportIllFormedPropertyOpenParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError("IllFormedPropertyOpenParenthesisInCondition", position, [_expression, position]);
    }

    private bool TryReportIllFormedPropertySpace(int position)
    {
        // Error position is 1-based.
        position++;

        return TryReportError("IllFormedPropertySpaceInCondition", position, [_expression, position, " "]);
    }

    private bool IllFormedQuotedString(int position)
    {
        // Error position is 1-based.
        position++;

        return TryReportError("IllFormedQuotedStringInCondition", position, [_expression, position]);
    }

    private bool TryReportIncorrectNumberOfFunctionArguments(int position, int argumentCount, int expectedArgumentCount)
        => TryReportError("IncorrectNumberOfFunctionArguments", position + 1, [_expression, argumentCount, expectedArgumentCount]);

    private bool TryReportItemListNotAllowed(int? position = null)
    {
        // Error position is 1-based
        int pos = (position ?? _position) + 1;

        return TryReportError("ItemListNotAllowedInThisConditional", pos, [_expression, pos]);
    }

    private bool TryReportUndefinedFunctionCall(int position, string name)
        => TryReportError("UndefinedFunctionCall", position + 1, [_expression, name]);

    private bool TryReportUnexpectedToken()
    {
        // Error position is 1-based
        int position = _position + 1;
        string nextChar = IsAtEnd() ? EndOfInput : _expression[_position].ToString();

        return TryReportError("UnexpectedTokenInCondition", position, [_expression, position, nextChar]);
    }

    private bool TryConsume(char ch)
    {
        if (!IsAtEnd() && _expression[_position] == ch)
        {
            _position++;
            return true;
        }

        return false;
    }

    private bool TryPeekChar(int offset, out char ch)
    {
        int pos = _position + offset;

        if (pos < _expression.Length)
        {
            ch = _expression[pos];
            return true;
        }

        ch = '\0';
        return false;
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
            TryReportUnexpectedToken();
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

                node = TryConsume('=')
                    ? new LessThanOrEqualExpressionNode()
                    : new LessThanExpressionNode();

                return true;

            case '>':
                _position++;

                node = TryConsume('=')
                    ? new GreaterThanOrEqualExpressionNode()
                    : new GreaterThanExpressionNode();

                return true;

            case '=':
                _position++;

                if (TryConsume('='))
                {
                    node = new EqualExpressionNode();
                    return true;
                }

                return TryReportIllFormedEquals();

            case '!':
                _position++;

                if (TryConsume('='))
                {
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
            return TryReportUnexpectedToken();
        }

        char ch = _expression[_position];

        // Check for '!' (not operator)
        if (ch == '!')
        {
            _position++;

            // Check if it's != (handled by relational operator)
            if (TryConsume('='))
            {
                return TryReportUnexpectedToken();
            }

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

            if (!TryConsume(')'))
            {
                return TryReportUnexpectedToken();
            }

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
            return TryReportUnexpectedToken();
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

            if (TryConsume('('))
            {
                if (!AllowUnknownFunctions)
                {
                    if (!TryMatchFunctionSpan(span, out FunctionDescriptor? function))
                    {
                        return TryReportUndefinedFunctionCall(position: start, name: span.ToString());
                    }

                    SkipWhiteSpace();

                    if (!TryParseArgumentList(out ImmutableArray<GenericExpressionNode> argumentList))
                    {
                        return false;
                    }

                    if (argumentList.Length != function.ArgumentCount)
                    {
                        return TryReportIncorrectNumberOfFunctionArguments(position: start, argumentList.Length, function.ArgumentCount);
                    }

                    node = function.Kind switch
                    {
                        FunctionKind.Exists => new ExistsCallExpressionNode(function.Name, argumentList),
                        FunctionKind.HasTrailingSlash => new HasTrailingSlashCallExpressionNode(function.Name, argumentList),

                        _ => ErrorUtilities.ThrowInternalErrorUnreachable<GenericExpressionNode>()
                    };

                    return true;
                }
                else
                {
                    SkipWhiteSpace();

                    if (!TryParseArgumentList(out ImmutableArray<GenericExpressionNode> argumentList))
                    {
                        return false;
                    }

                    node = new UnknownFunctionCallExpressionNode(span.ToString(), argumentList);
                    return true;
                }
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

            return node is not null
                || TryReportUnexpectedToken();
        }

        // Fall back to other argument types
        return TryParseArgument(out node);
    }

    private bool TryParseArgumentList([NotNullWhen(true)] out ImmutableArray<GenericExpressionNode> argumentList)
    {
        if (TryConsume(')'))
        {
            argumentList = [];
            return true;
        }

        ImmutableArray<GenericExpressionNode>.Builder? builder = null;

        while (TryParseArgument(out GenericExpressionNode? arg))
        {
            builder ??= ImmutableArray.CreateBuilder<GenericExpressionNode>();
            builder.Add(arg);

            SkipWhiteSpace();

            if (TryConsume(')'))
            {
                argumentList = builder.ToImmutable();
                return true;
            }

            if (!TryConsume(','))
            {
                break;
            }
        }

        argumentList = default;
        return TryReportUnexpectedToken();
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
            if ((_options & ParserOptions.AllowItemLists) == 0 && TryPeekChar(1, out char c) && c == '(')
            {
                return TryReportItemListNotAllowed();
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

        return TryReportUnexpectedToken();
    }

    private bool TryParsePropertyOrItemMetadata([NotNullWhen(true)] out string? result)
    {
        result = null;
        int start = _position;
        _position++;

        if (IsAtEnd() || _expression[_position] != '(')
        {
            return TryReportIllFormedPropertyOpenParenthesis(start);
        }

        if (!ScanForPropertyExpressionEnd(_expression, _position, out int indexResult))
        {
            return TryReportIllFormedPropertySpace(indexResult);
        }

        _position = indexResult;

        if (IsAtEnd())
        {
            return TryReportIllFormedPropertyCloseParenthesis(start);
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
            return TryReportBuiltInMetadataNotAllowed(metadataName);
        }

        if (((_options & ParserOptions.AllowCustomMetadata) == 0) && !isItemSpecModifier)
        {
            return TryReportCustomMetadataNotAllowed(metadataName);
        }

        return true;
    }

    private bool TryParseItemList([NotNullWhen(true)] out string? result)
    {
        result = null;
        int start = _position;
        _position++;

        if (!TryConsume('('))
        {
            return TryReportIllFormedItemListOpenParenthesis(start);
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
            return inReplacement
                ? TryReportIllFormedItemListQuote(start)
                : TryReportIllFormedItemListCloseParenthesis(start);
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
                    return TryReportItemListNotAllowed(start);
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
            return IllFormedQuotedString(start);
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

    private static bool TryMatchKeywordSpan(ReadOnlySpan<char> span, out KeywordKind keyword)
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

    private static bool TryMatchFunctionSpan(ReadOnlySpan<char> span, [NotNullWhen(true)] out FunctionDescriptor? function)
    {
#if NET
        if (s_functions.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(span, out function))
        {
            return true;
        }
#else
        foreach (KeyValuePair<string, FunctionDescriptor> pair in s_functions)
        {
            if (span.Equals(pair.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                function = pair.Value;
                return true;
            }
        }
#endif

        function = null;
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
