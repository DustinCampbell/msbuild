// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
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
        private int _position;

        // Error tracking
        private bool _errorState;
        internal int _errorPosition = -1; // -1 = not set; >0 = 1-based position for tests
        private string _errorResource;
        private string _unexpectedlyFound;
        private static string s_endOfInput;

        /// <summary>
        ///  Location contextual information which are attached to logging events to
        ///  say where they are in relation to the process, engine, project, target,task which is executing.
        /// </summary>
        private readonly BuildEventContext _logBuildEventContext;

        /// <summary>
        ///  Engine Logging Service reference where events will be logged to.
        /// </summary>
        private readonly ILoggingService _loggingService;

        private bool _warnedForExpression = false;

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

        private string EndOfInput
        {
            get
            {
                if (s_endOfInput == null)
                {
                    s_endOfInput = ResourceUtilities.GetResourceString("EndOfInputTokenName");
                }

                return s_endOfInput;
            }
        }

        public Parser(
            string expression,
            ParserOptions optionSettings,
            IElementLocation elementLocation,
            LoggingContext loggingContext = null)
        {
            // We currently have no support (and no scenarios) for disallowing property references in conditions.
            ErrorUtilities.VerifyThrow((optionSettings & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

            _expression = expression;
            _options = optionSettings;
            _elementLocation = elementLocation;
            _position = 0;
            _errorState = false;

            _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
            _loggingService = loggingContext?.LoggingService;
        }

        /// <summary>
        /// Main entry point for parser.
        /// You pass in the expression you want to parse, and you get an
        /// expression tree out the back end.
        /// </summary>
        internal static GenericExpressionNode Parse(
            string expression,
            ParserOptions optionSettings,
            IElementLocation elementLocation,
            LoggingContext loggingContext = null)
        {
            var parser = new Parser(expression, optionSettings, elementLocation, loggingContext);

            return parser.Parse();
        }

        /// <summary>
        /// Parses the expression and returns the expression tree.
        /// </summary>
        internal GenericExpressionNode Parse()
        {
            GenericExpressionNode node = Expr();

            if (_errorState)
            {
                ThrowInvalidProject();
            }

            SkipWhiteSpace();
            if (!IsAtEnd())
            {
                SetError("UnexpectedTokenInCondition");
                ThrowUnexpectedTokenInCondition();
            }

            return node;
        }

        //
        // Top node of grammar
        //    See grammar for how the following methods relate to each
        //    other.
        //
        private GenericExpressionNode Expr()
        {
            GenericExpressionNode node = BooleanTerm();
            if (_errorState || node == null)
            {
                return node;
            }

            SkipWhiteSpace();
            while (!IsAtEnd() && TryMatchKeyword(KeywordKind.Or))
            {
                OperatorExpressionNode orNode = new OrExpressionNode();
                GenericExpressionNode rhs = BooleanTerm();
                if (_errorState || rhs == null)
                {
                    SetUnexpectedToken();
                    return null;
                }

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

            return node;
        }

        private GenericExpressionNode BooleanTerm()
        {
            GenericExpressionNode node = RelationalExpr();
            if (_errorState || node == null)
            {
                SetUnexpectedToken();
                return null;
            }

            SkipWhiteSpace();
            while (!IsAtEnd() && TryMatchKeyword(KeywordKind.And))
            {
                GenericExpressionNode rhs = RelationalExpr();
                if (_errorState || rhs == null)
                {
                    SetUnexpectedToken();
                    return null;
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = node;
                andNode.RightChild = rhs;
                node = andNode;
                SkipWhiteSpace();
            }

            return node;
        }

        private GenericExpressionNode RelationalExpr()
        {
            GenericExpressionNode lhs = Factor();
            if (_errorState || lhs == null)
            {
                SetUnexpectedToken();
                return null;
            }

            SkipWhiteSpace();
            OperatorExpressionNode node = TryParseRelationalOperator();
            if (node == null)
            {
                return lhs;
            }

            GenericExpressionNode rhs = Factor();
            if (_errorState || rhs == null)
            {
                SetUnexpectedToken();
                return null;
            }

            node.LeftChild = lhs;
            node.RightChild = rhs;
            return node;
        }

        private OperatorExpressionNode TryParseRelationalOperator()
        {
            if (IsAtEnd())
            {
                return null;
            }

            char ch = _expression[_position];

            switch (ch)
            {
                case '<':
                    _position++;
                    if (!IsAtEnd() && _expression[_position] == '=')
                    {
                        _position++;
                        return new LessThanOrEqualExpressionNode();
                    }
                    return new LessThanExpressionNode();

                case '>':
                    _position++;
                    if (!IsAtEnd() && _expression[_position] == '=')
                    {
                        _position++;
                        return new GreaterThanOrEqualExpressionNode();
                    }
                    return new GreaterThanExpressionNode();

                case '=':
                    if (_position + 1 < _expression.Length && _expression[_position + 1] == '=')
                    {
                        _position += 2;
                        return new EqualExpressionNode();
                    }
                    // Single '=' is an error - point to the character after it
                    _errorPosition = _position + 2; // Explicitly set position before SetError
                    SetError("IllFormedEqualsInCondition");
                    if (_position + 1 < _expression.Length)
                    {
                        _unexpectedlyFound = _expression[_position + 1].ToString();
                    }
                    else
                    {
                        _unexpectedlyFound = EndOfInput;
                    }
                    _position++;
                    return null;

                case '!':
                    if (_position + 1 < _expression.Length && _expression[_position + 1] == '=')
                    {
                        _position += 2;
                        return new NotEqualExpressionNode();
                    }
                    return null;

                default:
                    return null;
            }
        }

        private GenericExpressionNode Factor()
        {
            SkipWhiteSpace();

            if (IsAtEnd())
            {
                SetUnexpectedToken();
                return null;
            }

            char ch = _expression[_position];

            // Check for '!' (not operator)
            if (ch == '!')
            {
                // Check if it's != (handled by relational operator)
                if (_position + 1 < _expression.Length && _expression[_position + 1] == '=')
                {
                    SetUnexpectedToken();
                    return null;
                }

                _position++;
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor();
                if (_errorState || expr == null)
                {
                    SetUnexpectedToken();
                    return null;
                }
                notNode.LeftChild = expr;
                return notNode;
            }

            // Check for '(' (grouped expression)
            if (ch == '(')
            {
                _position++;
                GenericExpressionNode child = Expr();
                if (_errorState || child == null)
                {
                    SetUnexpectedToken();
                    return null;
                }

                SkipWhiteSpace();
                if (IsAtEnd() || _expression[_position] != ')')
                {
                    SetUnexpectedToken();
                    return null;
                }
                _position++;
                return child;
            }

            // Try to parse an argument (string, number, property, etc.) or function
            return ParseArgumentOrFunction();
        }

        private GenericExpressionNode ParseArgumentOrFunction()
        {
            SkipWhiteSpace();

            if (IsAtEnd())
            {
                SetUnexpectedToken();
                return null;
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
                    var arglist = new List<GenericExpressionNode>();

                    SkipWhiteSpace();
                    if (IsAtEnd() || _expression[_position] != ')')
                    {
                        ParseArgumentList(arglist);
                        if (_errorState)
                        {
                            return null;
                        }
                    }

                    SkipWhiteSpace();
                    if (IsAtEnd() || _expression[_position] != ')')
                    {
                        SetUnexpectedToken();
                        return null;
                    }
                    _position++;
                    return new FunctionCallExpressionNode(functionName, arglist);
                }

                // Not a function - restore position and check if it's a keyword
                _position = savedPosition;

                if (TryMatchKeywordSpan(span, out KeywordKind keyword))
                {
                    return keyword switch
                    {
                        KeywordKind.True or KeywordKind.On or KeywordKind.Yes => new BooleanLiteralNode(true, span.ToString()),
                        KeywordKind.False or KeywordKind.Off or KeywordKind.No => new BooleanLiteralNode(false, span.ToString()),
                        _ => new StringExpressionNode(span.ToString(), false)
                    };
                }

                // Just a simple string
                return new StringExpressionNode(span.ToString(), false);
            }

            // Fall back to other argument types
            return TryParseArgument();
        }

        private void ParseArgumentList(List<GenericExpressionNode> arglist)
        {
            while (true)
            {
                GenericExpressionNode arg = TryParseArgument();
                if (_errorState || arg == null)
                {
                    SetUnexpectedToken();
                    return;
                }

                arglist.Add(arg);

                SkipWhiteSpace();
                if (IsAtEnd() || _expression[_position] != ',')
                {
                    break;
                }

                _position++; // consume ','
                SkipWhiteSpace();
            }
        }

        private GenericExpressionNode TryParseArgument()
        {
            SkipWhiteSpace();

            if (IsAtEnd())
            {
                return null;
            }

            char ch = _expression[_position];

            // Property reference: $(...)
            if (ch == '$')
            {
                if (TryParsePropertyOrItemMetadata(out string propertyExpression))
                {
                    return new StringExpressionNode(propertyExpression, true /* requires expansion */);
                }
                return null;
            }

            // Item metadata: %(...)
            if (ch == '%')
            {
                if (TryParsePropertyOrItemMetadata(out string itemMetadataExpression))
                {
                    string expression = itemMetadataExpression;

                    // Extract metadata name for validation
                    if (expression.Length > 3 && expression[0] == '%' && expression[1] == '(' && expression[expression.Length - 1] == ')')
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
                            return null;
                        }
                    }

                    return new StringExpressionNode(itemMetadataExpression, true /* requires expansion */);
                }
                return null;
            }

            // Item list: @(...)
            if (ch == '@')
            {
                if ((_options & ParserOptions.AllowItemLists) == 0)
                {
                    if (_position + 1 < _expression.Length && _expression[_position + 1] == '(')
                    {
                        SetError("ItemListNotAllowedInThisConditional");
                        return null;
                    }
                }

                if (TryParseItemList(out string itemListExpression))
                {
                    return new StringExpressionNode(itemListExpression, true /* requires expansion */);
                }
                return null;
            }

            // Quoted string: '...'
            if (ch == '\'')
            {
                if (TryParseQuotedString(out string stringValue, out bool expandable))
                {
                    return new StringExpressionNode(stringValue, expandable);
                }
                return null;
            }

            // Numeric literal
            if (CharacterUtilities.IsNumberStart(ch))
            {
                if (TryParseNumeric(out string numericValue))
                {
                    return new NumericExpressionNode(numericValue);
                }
                return null;
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
                    return keyword switch
                    {
                        KeywordKind.True or KeywordKind.On or KeywordKind.Yes => new BooleanLiteralNode(true, span.ToString()),
                        KeywordKind.False or KeywordKind.Off or KeywordKind.No => new BooleanLiteralNode(false, span.ToString()),
                        _ => new StringExpressionNode(span.ToString(), false)
                    };
                }

                // Just a simple string
                return new StringExpressionNode(span.ToString(), false);
            }

            return null;
        }

        private bool TryParsePropertyOrItemMetadata(out string result)
        {
            result = null;
            int start = _position;
            _position++;

            if (IsAtEnd() || _expression[_position] != '(')
            {
                SetError("IllFormedPropertyOpenParenthesisInCondition");
                _errorPosition = start + 1;
                _unexpectedlyFound = IsAtEnd() ? EndOfInput : _expression[_position].ToString();
                return false;
            }

            if (!ScanForPropertyExpressionEnd(_expression, _position++, out int indexResult))
            {
                SetError("IllFormedPropertySpaceInCondition");
                _errorPosition = indexResult;
                _unexpectedlyFound = _expression[indexResult].ToString();
                return false;
            }

            _position = indexResult;

            if (IsAtEnd())
            {
                SetError("IllFormedPropertyCloseParenthesisInCondition");
                _errorPosition = start + 1;
                _unexpectedlyFound = EndOfInput;
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
                SetError("BuiltInMetadataNotAllowedInThisConditional");
                _unexpectedlyFound = metadataName;
                return false;
            }

            if (((_options & ParserOptions.AllowCustomMetadata) == 0) && !isItemSpecModifier)
            {
                SetError("CustomMetadataNotAllowedInThisConditional");
                _unexpectedlyFound = metadataName;
                return false;
            }

            return true;
        }

        private bool TryParseItemList(out string result)
        {
            result = null;
            int start = _position;
            _position++;

            if (IsAtEnd() || _expression[_position] != '(')
            {
                SetError("IllFormedItemListOpenParenthesisInCondition");
                _errorPosition = start + 1;
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
                SetError(inReplacement ? "IllFormedItemListQuoteInCondition" : "IllFormedItemListCloseParenthesisInCondition");
                _errorPosition = start + 1;
                return false;
            }

            _position++;
            result = _expression.Substring(start, _position - start);
            return true;
        }

        private bool TryParseQuotedString(out string result, out bool expandable)
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
                        SetError("ItemListNotAllowedInThisConditional");
                        _errorPosition = start + 1;
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
                SetError("IllFormedQuotedStringInCondition");
                _errorPosition = start;
                return false;
            }

            result = _expression.Substring(start, _position - start);
            _position++; // Skip closing quote
            return true;
        }

        private bool TryParseNumeric(out string result)
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
            } while (_position < _expression.Length && _expression[_position] == '.');

            result = _expression.Substring(start, _position - start);
            return true;
        }

        private bool TryMatchKeyword(KeywordKind keyword)
        {
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

        private void SetError(string resourceName)
        {
            _errorState = true;
            _errorResource = resourceName;
            if (_errorPosition == -1)
            {
                _errorPosition = _position + 1; // Convert to 1-based
            }
        }

        private void SetUnexpectedToken()
        {
            if (!_errorState)
            {
                SetError("UnexpectedTokenInCondition");
                if (_position < _expression.Length)
                {
                    _unexpectedlyFound = _expression[_position].ToString();
                }
                else
                {
                    _unexpectedlyFound = EndOfInput;
                }
            }
        }

        private void ThrowInvalidProject()
        {
            if (_errorResource == null)
            {
                ThrowUnexpectedCharacterInCondition();
            }
            else if (_unexpectedlyFound != null)
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _errorResource, _expression, _errorPosition, _unexpectedlyFound);
            }
            else
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _errorResource, _expression, _errorPosition);
            }
        }

        private void ThrowUnexpectedCharacterInCondition()
        {
            string unexpectedlyFound = _unexpectedlyFound ?? EndOfInput;

            ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedCharacterInCondition", _expression, _errorPosition, unexpectedlyFound);
        }

        private void ThrowUnexpectedTokenInCondition()
        {
            string foundToken = _position < _expression.Length ? _expression[_position].ToString() : EndOfInput;
            ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _expression, _errorPosition, foundToken);
        }
    }
}
