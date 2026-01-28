// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
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
internal ref partial struct Parser
{
    private static readonly Lookup<KeywordKind> s_keywords = new(
        ("and", KeywordKind.And),
        ("or", KeywordKind.Or),
        ("true", KeywordKind.True),
        ("false", KeywordKind.False),
        ("on", KeywordKind.On),
        ("off", KeywordKind.Off),
        ("yes", KeywordKind.Yes),
        ("no", KeywordKind.No));

    private static readonly Lookup<KnownFunction> s_knownFunctions = new(
        ("Exists", new(KnownFunctionKind.Exists, "Exists", argumentCount: 1)),
        ("HasTrailingSlash", new(KnownFunctionKind.HasTrailingSlash, "HasTrailingSlash", argumentCount: 1)));

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
        IElementLocation elementLocation,
        LoggingContext? loggingContext,
        bool throwException)
    {
        _expression = expression;
        _options = options;
        _throwException = throwException;
        _elementLocation = elementLocation;
        _position = 0;

        _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
        _loggingService = loggingContext?.LoggingService;

        // We currently have no support (and no scenarios) for disallowing property references in conditions.
        ErrorUtilities.VerifyThrow(AllowProperties, "Properties should always be allowed.");
    }

    private readonly bool AllowBuiltInMetadata
        => (_options & ParserOptions.AllowBuiltInMetadata) == ParserOptions.AllowBuiltInMetadata;

    private readonly bool AllowCustomMetadata
        => (_options & ParserOptions.AllowCustomMetadata) == ParserOptions.AllowCustomMetadata;

    private readonly bool AllowItemLists
        => (_options & ParserOptions.AllowItemLists) == ParserOptions.AllowItemLists;

    private readonly bool AllowAllItemMetadata
        => (_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata;

    private readonly bool AllowAnyItemMetadata
        => (_options & ParserOptions.AllowItemMetadata) != 0;

    private readonly bool AllowProperties
        => (_options & ParserOptions.AllowProperties) == ParserOptions.AllowProperties;

    private readonly bool AllowUnknownFunctions
        => (_options & ParserOptions.AllowUndefinedFunctions) == ParserOptions.AllowUndefinedFunctions;

    private readonly bool AtEnd
        => _position >= _expression.Length;

    private readonly ReadOnlySpan<char> RemainingSpan
        => _expression.AsSpan(_position);

    private readonly bool At(char ch)
        => !AtEnd && _expression[_position] == ch;

    private readonly bool At(ReadOnlySpan<char> chars)
        => _position + chars.Length < _expression.Length
        && _expression.AsSpan(_position, chars.Length).Equals(chars, StringComparison.Ordinal);

    private bool Advance(out char ch)
    {
        if (!AtEnd)
        {
            ch = _expression[_position];
            _position++;
            return true;
        }

        ch = '\0';
        return false;
    }

    private bool AdvancePast(char ch)
    {
        if (!AtEnd && _expression[_position] == ch)
        {
            _position++;
            return true;
        }

        return false;
    }

    private readonly bool Next(char ch)
    {
        int pos = _position + 1;

        return pos < _expression.Length && _expression[pos] == ch;
    }

    private readonly bool Next(out char ch)
    {
        int pos = _position + 1;

        if (pos < _expression.Length)
        {
            ch = _expression[pos];
            return true;
        }

        ch = default;
        return false;
    }

    private void AdvancePastWhiteSpace()
    {
        while (_position < _expression.Length && char.IsWhiteSpace(_expression[_position]))
        {
            _position++;
        }
    }

    private bool AdvancePast(KeywordKind keyword)
    {
        ReadOnlySpan<char> span = RemainingSpan;

        if (!LexingUtilities.TryLexIdentifier(span, out ReadOnlySpan<char> identifierSpan))
        {
            return false;
        }

        if (!s_keywords.TryGetValue(identifierSpan, out KeywordKind found) || found != keyword)
        {
            return false;
        }

        _position += identifierSpan.Length;
        return true;
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
        var parser = new Parser(expression, options, elementLocation, loggingContext, throwException: true);

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
        var parser = new Parser(expression, options, elementLocation, loggingContext: null, throwException: false);

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

        AdvancePastWhiteSpace();

        if (!AtEnd)
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

        AdvancePastWhiteSpace();

        while (AdvancePast(KeywordKind.Or))
        {
            if (!TryParseAndExpression(out GenericExpressionNode? rhs))
            {
                return false;
            }

            OperatorExpressionNode orNode = new OrExpressionNode();
            orNode.LeftChild = node;
            orNode.RightChild = rhs;
            node = orNode;

            AdvancePastWhiteSpace();
        }

        // Check for potential change in behavior
        if (_loggingService is { } loggingService
            && !_warnedForExpression &&
            node.PotentialAndOrConflict())
        {
            // We only want to warn once even if there multiple () sub expressions
            _warnedForExpression = true;

            // Log a warning regarding the fact the expression may have been evaluated
            // incorrectly in earlier version of MSBuild
            loggingService.LogWarning(
                _logBuildEventContext,
                subcategoryResourceName: null,
                new BuildEventFileInfo(_elementLocation),
                "ConditionMaybeEvaluatedIncorrectly",
                _expression);
        }

        return true;
    }

    private bool TryParseAndExpression([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        if (!TryParseComparisonExpression(out node))
        {
            return false;
        }

        AdvancePastWhiteSpace();

        while (AdvancePast(KeywordKind.And))
        {
            if (!TryParseComparisonExpression(out GenericExpressionNode? rhs))
            {
                return false;
            }

            OperatorExpressionNode andNode = new AndExpressionNode();
            andNode.LeftChild = node;
            andNode.RightChild = rhs;
            node = andNode;

            AdvancePastWhiteSpace();
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

        AdvancePastWhiteSpace();

        if (AtEnd)
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

        if (AtEnd)
        {
            return false;
        }

        char ch = _expression[_position];

        switch (ch)
        {
            case '<':
                _position++;

                node = AdvancePast('=')
                    ? new LessThanOrEqualExpressionNode()
                    : new LessThanExpressionNode();

                return true;

            case '>':
                _position++;

                node = AdvancePast('=')
                    ? new GreaterThanOrEqualExpressionNode()
                    : new GreaterThanExpressionNode();

                return true;

            case '=':
                _position++;

                if (AdvancePast('='))
                {
                    node = new EqualExpressionNode();
                    return true;
                }

                return TryReportIllFormedEquals();

            case '!':
                _position++;

                if (AdvancePast('='))
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

        AdvancePastWhiteSpace();

        if (AtEnd)
        {
            return TryReportUnexpectedToken();
        }

        // Check for '!' (not operator)
        if (AdvancePast('!'))
        {
            // Check if it's != (handled by relational operator)
            if (AdvancePast('='))
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
        if (AdvancePast('('))
        {
            if (!TryParseOrExpression(out GenericExpressionNode? child))
            {
                return false;
            }

            AdvancePastWhiteSpace();

            if (!AdvancePast(')'))
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

        AdvancePastWhiteSpace();

        if (AtEnd)
        {
            return TryReportUnexpectedToken();
        }

        // Check for function call or simple string: identifier followed optionally by '('
        if (LexingUtilities.TryLexIdentifier(RemainingSpan, out ReadOnlySpan<char> identifierSpan))
        {
            int start = _position;
            _position += identifierSpan.Length;

            // Look ahead for '(' to detect function call
            AdvancePastWhiteSpace();

            if (AdvancePast('('))
            {
                if (!AllowUnknownFunctions)
                {
                    if (!s_knownFunctions.TryGetValue(identifierSpan, out KnownFunction? function))
                    {
                        return TryReportUndefinedFunctionCall(position: start, name: identifierSpan.ToString());
                    }

                    AdvancePastWhiteSpace();

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
                        KnownFunctionKind.Exists => new ExistsCallExpressionNode(function.Name, argumentList),
                        KnownFunctionKind.HasTrailingSlash => new HasTrailingSlashCallExpressionNode(function.Name, argumentList),

                        _ => ErrorUtilities.ThrowInternalErrorUnreachable<GenericExpressionNode>()
                    };

                    return true;
                }
                else
                {
                    AdvancePastWhiteSpace();

                    if (!TryParseArgumentList(out ImmutableArray<GenericExpressionNode> argumentList))
                    {
                        return false;
                    }

                    node = new FunctionCallExpressionNode.Undefined(identifierSpan.ToString(), argumentList);
                    return true;
                }
            }

            if (s_keywords.TryGetValue(identifierSpan, out KeywordKind keyword))
            {
                if (TryCreateBooleanLiteral(keyword, identifierSpan, out BooleanLiteralNode? booleanNode))
                {
                    node = booleanNode;
                    return true;
                }

                return TryReportUnexpectedToken(start);
            }

            // Just a normal identifier
            node = new StringExpressionNode(identifierSpan.ToString(), false);
            return true;
        }

        // Fall back to other argument types
        return TryParseArgument(out node);
    }

    private bool TryParseArgumentList([NotNullWhen(true)] out ImmutableArray<GenericExpressionNode> argumentList)
    {
        if (AdvancePast(')'))
        {
            argumentList = [];
            return true;
        }

        ImmutableArray<GenericExpressionNode>.Builder? builder = null;

        while (TryParseArgument(out GenericExpressionNode? arg))
        {
            builder ??= ImmutableArray.CreateBuilder<GenericExpressionNode>();
            builder.Add(arg);

            AdvancePastWhiteSpace();

            if (AdvancePast(')'))
            {
                argumentList = builder.ToImmutable();
                return true;
            }

            if (!AdvancePast(','))
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

        AdvancePastWhiteSpace();

        if (AtEnd)
        {
            return false;
        }

        char ch = _expression[_position];

        // Property reference: $(...)
        if (ch == '$')
        {
            if (TryParseProperty(out string? propertyExpression))
            {
                node = new StringExpressionNode(propertyExpression, expandable: true);
                return true;
            }

            return false;
        }

        // Item metadata: %(...)
        if (ch == '%')
        {
            if (!AllowAnyItemMetadata && Next('('))
            {
                return TryReportItemMetadataNotAllowed();
            }

            if (TryParseItemMetadata(out string? itemMetadataExpression))
            {
                node = new StringExpressionNode(itemMetadataExpression, expandable: true);
                return true;
            }

            return false;
        }

        // Item list: @(...)
        if (ch == '@')
        {
            if (!AllowItemLists && Next('('))
            {
                return TryReportItemListNotAllowed();
            }

            if (TryParseItemList(out string? itemListExpression))
            {
                node = new StringExpressionNode(itemListExpression, expandable: true);
                return true;
            }

            return false;
        }

        // Quoted string: '...'
        if (ch == '\'')
        {
            return TryParseQuotedString(out node);
        }

        // Numeric literal
        if (TryParseNumeric(out NumericExpressionNode? numericNode))
        {
            node = numericNode;
            return true;
        }

        // Identifier or keyword (but NOT function - ParseArgumentOrFunction handles that)
        if (LexingUtilities.TryLexIdentifier(RemainingSpan, out ReadOnlySpan<char> identifierSpan))
        {
            _position += identifierSpan.Length;

            if (s_keywords.TryGetValue(identifierSpan, out KeywordKind keyword))
            {
                if (TryCreateBooleanLiteral(keyword, identifierSpan, out BooleanLiteralNode? booleanNode))
                {
                    node = booleanNode;
                    return true;
                }

                node = null;
                return false;
            }

            // Just a normal identifier
            node = new StringExpressionNode(identifierSpan.ToString(), false);
            return true;
        }

        return TryReportUnexpectedToken();
    }

    private bool TryParseProperty([NotNullWhen(true)] out string? result)
    {
        Debug.Assert(At('$'), "Property reference must start with '$' character.");
        Debug.Assert(AllowProperties, "Properties should have been rejected earlier.");

        result = null;
        int start = _position;
        _position++;

        if (!AdvancePast('('))
        {
            return TryReportIllFormedPropertyOpenParenthesis(start);
        }

        if (!TryScanForPropertyExpressionEnd(_expression, _position, out int indexResult))
        {
            return TryReportIllFormedSpace(indexResult);
        }

        _position = indexResult;

        if (AtEnd)
        {
            return TryReportIllFormedPropertyCloseParenthesis(start);
        }

        _position++;
        result = _expression.Substring(start, _position - start);
        return true;
    }

    private bool TryParseItemMetadata([NotNullWhen(true)] out string? result)
    {
        Debug.Assert(At('%'), "Item metadata must start with '%' character.");
        Debug.Assert(AllowAnyItemMetadata, "Metadata should have been rejected earlier.");

        // Item metadata comes in two forms:
        //
        // 1. %(MetadataName)
        // 2. %(ItemType.MetadataName)
        //
        // Whitespace is allowed after the '(', before and after the '.', and before the ')'.

        result = null;
        int start = _position;
        _position++;

        if (!AdvancePast('('))
        {
            return TryReportIllFormedItemMetadataOpenParenthesis(start);
        }

        AdvancePastWhiteSpace();

        if (!TryParseName(out string? metadataName))
        {
            return TryReportUnexpectedToken();
        }

        AdvancePastWhiteSpace();

        if (AdvancePast('.'))
        {
            AdvancePastWhiteSpace();

            if (!TryParseName(out metadataName))
            {
                return TryReportUnexpectedToken();
            }

            AdvancePastWhiteSpace();
        }

        if (!AdvancePast(')'))
        {
            return TryReportIllFormedItemMetadataCloseParenthesis(start);
        }

        if (!CheckMetadataAllowed(metadataName))
        {
            return false;
        }

        result = _expression.Substring(start, _position - start);
        return true;
    }

    private bool TryParseName([NotNullWhen(true)] out string? result)
    {
        if (LexingUtilities.TryLexName(RemainingSpan, out ReadOnlySpan<char> nameSpan))
        {
            _position += nameSpan.Length;
            result = nameSpan.ToString();
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryScanForPropertyExpressionEnd(string expression, int index, out int indexResult)
    {
        int parenToClose = 1;
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
                        parenToClose++;
                    }
                    else if (character == ')')
                    {
                        parenToClose--;
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
                        if (!TryScanForPropertyExpressionEnd(expression, index + 2, out index))
                        {
                            indexResult = index;
                            return false;
                        }
                    }

                    if (parenToClose == 0)
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
        if (AllowAllItemMetadata)
        {
            // If we allow all item metadata, we don't have to check whether it's built-in or custom.
            return true;
        }

        if (FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName))
        {
            if (!AllowBuiltInMetadata)
            {
                return TryReportBuiltInMetadataNotAllowed(metadataName);
            }
        }
        else if (!AllowCustomMetadata)
        {
            return TryReportCustomMetadataNotAllowed(metadataName);
        }

        return true;
    }

    private bool TryParseItemList([NotNullWhen(true)] out string? result)
    {
        Debug.Assert(At('@'), "Item list must start with '@' character.");
        Debug.Assert(AllowItemLists, "Item lists should have been rejected earlier.");

        result = null;
        int start = _position;
        _position++;

        if (!AdvancePast('('))
        {
            return TryReportIllFormedItemListOpenParenthesis(start);
        }

        // The initial '(' has already been consumed, so we start with one open parenthesis to close.
        int parenToClose = 1;

        while (Advance(out char ch))
        {
            if (ch == '\'')
            {
                // Scan to the end of the replacement string until we find the closing quote.
                int index = 0;
                ReadOnlySpan<char> span = RemainingSpan;

                bool foundQuote = false;

                while (index < span.Length && !foundQuote)
                {
                    foundQuote = span[index] == '\'';
                    index++;
                }

                _position += index;

                if (!foundQuote)
                {
                    return TryReportIllFormedItemListQuote(start);
                }
            }
            else if (ch == '(')
            {
                parenToClose++;
            }
            else if (ch == ')')
            {
                parenToClose--;

                if (parenToClose == 0)
                {
                    // Found the last closing parenthesis. All done.
                    break;
                }
            }
        }

        if (parenToClose > 0)
        {
            return TryReportIllFormedItemListCloseParenthesis(start);
        }

        result = _expression.Substring(start, _position - start);
        return true;
    }

    private bool TryParseQuotedString([NotNullWhen(true)] out GenericExpressionNode? result)
    {
        result = null;
        bool expandable = false;

        if (!AdvancePast('\''))
        {
            return false;
        }

        int start = _position;

        if (AtEnd)
        {
            return IllFormedQuotedString(start);
        }

        do
        {
            char ch = _expression[_position];

            if (ch == '\'')
            {
                // The text is the content between the quotes.
                ReadOnlySpan<char> span = _expression.AsSpan(start, _position - start);

                // Skip closing quote
                _position++;

                result = TryParseBooleanLiteral(span, out BooleanLiteralNode? booleanNode)
                    ? booleanNode
                    : new StringExpressionNode(span.ToString(), expandable);

                return true;
            }

            if (ch == '%')
            {
                if (Next('('))
                {
                    if (!AllowAnyItemMetadata)
                    {
                        return TryReportItemMetadataNotAllowed();
                    }

                    // Parse the item metadata to skip over it
                    if (!TryParseItemMetadata(out _))
                    {
                        return false;
                    }

                    expandable = true;
                    continue;
                }
                else
                {
                    // Standalone % for escaping.
                    // UNDONE: This is currently too permissive and should verify that this is an escape sequence.
                    expandable = true;
                }
            }
            else if (ch == '@')
            {
                if (!AllowItemLists && Next('('))
                {
                    return TryReportItemListNotAllowed(start);
                }

                // Parse the item list to skip over it
                if (!TryParseItemList(out _))
                {
                    return false;
                }

                expandable = true;
                continue;
            }
            else if (ch == '$' && Next('('))
            {
                expandable = true;
            }

            _position++;
            AdvancePastWhiteSpace();
        }
        while (!AtEnd);

        return IllFormedQuotedString(start);
    }

    private bool TryParseBooleanLiteral(ReadOnlySpan<char> span, [NotNullWhen(true)] out BooleanLiteralNode? result)
    {
        if (span.IsEmpty)
        {
            result = null;
            return false;
        }

        bool negated = false;

        if (span[0] == '!')
        {
            negated = true;
            span = span[1..];
        }

        if (!s_keywords.TryGetValue(span, out KeywordKind keyword) ||
            !TryGetBooleanValue(keyword, out bool value))
        {
            result = null;
            return false;
        }

        if (negated)
        {
            value = !value;
        }

        result = new BooleanLiteralNode(value, span.ToString());
        return true;
    }

    private bool TryParseNumeric([NotNullWhen(true)] out NumericExpressionNode? result)
    {
        ReadOnlySpan<char> span = RemainingSpan;

        if (LexingUtilities.TryLexHexNumber(span, out ReadOnlySpan<char> numberSpan) ||
            LexingUtilities.TryLexDecimalNumber(span, out numberSpan))
        {
            _position += numberSpan.Length;
            result = new NumericExpressionNode(numberSpan.ToString());
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryCreateBooleanLiteral(
        KeywordKind keyword,
        ReadOnlySpan<char> span,
        [NotNullWhen(true)] out BooleanLiteralNode? result)
    {
        if (TryGetBooleanValue(keyword, out bool value))
        {
            result = new BooleanLiteralNode(value, span.ToString());
            return true;
        }

        result = null;
        return false;
    }

    private static bool TryGetBooleanValue(KeywordKind keyword, out bool value)
    {
        switch (keyword)
        {
            case KeywordKind.True:
            case KeywordKind.On:
            case KeywordKind.Yes:
                value = true;
                return true;

            case KeywordKind.False:
            case KeywordKind.Off:
            case KeywordKind.No:
                value = false;
                return true;

            default:
                value = false;
                return false;
        }
    }

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
        return TryReportError(ParseErrors.BuiltInMetadataNotAllowed, position, [_expression, position, name]);
    }

    private bool TryReportCustomMetadataNotAllowed(string name)
    {
        // Error position is 1-based
        int position = _position + 1;
        return TryReportError(ParseErrors.CustomMetadataNotAllowed, position, [_expression, position, name]);
    }

    private bool TryReportIllFormedEquals()
    {
        // Error position is 1-based
        int position = _position + 1;
        string nextChar = AtEnd ? EndOfInput : _expression[_position].ToString();

        return TryReportError(ParseErrors.IllFormedEquals, position, [_expression, position, nextChar]);
    }

    private bool TryReportIllFormedItemListCloseParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedItemListCloseParenthesis, position, [_expression, position]);
    }

    private bool TryReportIllFormedItemListOpenParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedItemListOpenParenthesis, position, [_expression, position]);
    }

    private bool TryReportIllFormedItemListQuote(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedItemListQuote, position, [_expression, position]);
    }

    private bool TryReportIllFormedPropertyCloseParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedPropertyCloseParenthesis, position, [_expression, position]);
    }

    private bool TryReportIllFormedPropertyOpenParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedPropertyOpenParenthesis, position, [_expression, position]);
    }

    private bool TryReportIllFormedItemMetadataCloseParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedItemMetadataCloseParenthesis, position, [_expression, position]);
    }

    private bool TryReportIllFormedItemMetadataOpenParenthesis(int position)
    {
        // Error position is 1-based
        position++;

        return TryReportError(ParseErrors.IllFormedItemMetadataOpenParenthesis, position, [_expression, position]);
    }

    private bool TryReportIllFormedSpace(int position)
    {
        // Error position is 1-based.
        position++;

        return TryReportError(ParseErrors.IllFormedSpace, position, [_expression, position, " "]);
    }

    private bool IllFormedQuotedString(int position)
    {
        // Error position is 1-based.
        position++;

        return TryReportError(ParseErrors.IllFormedQuotedString, position, [_expression, position]);
    }

    private bool TryReportIncorrectNumberOfFunctionArguments(int position, int argumentCount, int expectedArgumentCount)
    {
        // Error position is 1-based.
        position++;

        return TryReportError(ParseErrors.IncorrectNumberOfFunctionArguments, position, [_expression, argumentCount, expectedArgumentCount]);
    }

    private bool TryReportItemListNotAllowed(int? position = null)
    {
        // Error position is 1-based
        int pos = (position ?? _position) + 1;

        return TryReportError(ParseErrors.ItemListNotAllowed, pos, [_expression, pos]);
    }

    private bool TryReportItemMetadataNotAllowed(int? position = null)
    {
        // Error position is 1-based
        int pos = (position ?? _position) + 1;

        return TryReportError(ParseErrors.ItemMetadataNotAllowed, pos, [_expression, pos]);
    }

    private bool TryReportUndefinedFunctionCall(int position, string name)
    {
        // Error position is 1-based.
        position++;

        return TryReportError(ParseErrors.UndefinedFunctionCall, position, [_expression, name]);
    }

    private bool TryReportUnexpectedToken(int? position = null)
    {
        int pos = position ?? _position;
        string nextChar = AtEnd ? EndOfInput : _expression[pos].ToString();

        // Error position is 1-based
        pos++;

        return TryReportError(ParseErrors.UnexpectedToken, pos, [_expression, pos, nextChar]);
    }
}
