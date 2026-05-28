// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

#nullable disable

namespace Microsoft.Build.Evaluation
{

    [Flags]
    internal enum ParserOptions
    {
        None = 0x0,
        AllowProperties = 0x1,
        AllowItemLists = 0x2,
        AllowPropertiesAndItemLists = AllowProperties | AllowItemLists,
        AllowBuiltInMetadata = 0x4,
        AllowCustomMetadata = 0x8,
        AllowItemMetadata = AllowBuiltInMetadata | AllowCustomMetadata,
        AllowPropertiesAndItemMetadata = AllowProperties | AllowItemMetadata,
        AllowPropertiesAndCustomMetadata = AllowProperties | AllowCustomMetadata,
        AllowAll = AllowProperties | AllowItemLists | AllowItemMetadata
    };

    /// <summary>
    ///  Represents the result of parsing a condition expression.
    ///  Contains either a successfully parsed expression node or error information.
    /// </summary>
    internal readonly struct ParseResult
    {
        public GenericExpressionNode Node { get; }
        public string ErrorResource { get; }
        public object[] ErrorArgs { get; }
        internal int ErrorPosition { get; }

        private readonly ElementLocation _elementLocation;

        [MemberNotNullWhen(false, nameof(Node))]
        [MemberNotNullWhen(true, nameof(ErrorResource))]
        [MemberNotNullWhen(true, nameof(ErrorArgs))]
        public bool IsError => ErrorResource is not null;

        private ParseResult(GenericExpressionNode node, string errorResource, object[] errorArgs, int errorPosition, ElementLocation elementLocation)
        {
            Node = node;
            ErrorResource = errorResource;
            ErrorArgs = errorArgs;
            ErrorPosition = errorPosition;
            _elementLocation = elementLocation;
        }

        public static ParseResult Success(GenericExpressionNode node)
            => new(node, errorResource: null, errorArgs: null, errorPosition: 0, elementLocation: null);

        public static ParseResult Error(string resource, object[] args, int position, ElementLocation elementLocation)
            => new(node: null, resource, args, position, elementLocation);

        /// <summary>
        ///  Throws an <see cref="Exceptions.InvalidProjectFileException"/> if this result represents a parse error.
        /// </summary>
        public void ThrowIfError()
        {
            if (IsError)
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, ErrorResource, ErrorArgs);
            }
        }
    }

    /// <summary>
    /// This class implements the grammar for complex conditionals.
    ///
    /// The usage is:
    ///    ParseResult result = Parser.Parse(expression, options, elementLocation);
    ///
    /// The expression tree can then be evaluated and re-evaluated as needed.
    /// </summary>
    internal sealed class Parser
    {
        private readonly string _expression;
        private readonly ParserOptions _options;
        private readonly ElementLocation _elementLocation;

        private Token _current;
        private int _position;

        private int _errorPosition;
        private string _errorResource;
        private object[] _errorArgs;

        private static string s_endOfInput;

        // Older versions of MSBuild evaluated mixed 'and'/'or' conditions left-to-right
        // instead of giving 'and' higher precedence. When this was fixed, a compatibility
        // warning (MSB4130) was added to flag expressions that might now evaluate differently.
        // The warning fires for expressions with both 'and' and 'or' at the same level
        // without explicit parentheses.
        private readonly ILoggingService _loggingServices;
        private readonly BuildEventContext _logBuildEventContext;
        private bool _warnedForExpression;

        private string EndOfInput
        {
            get
            {
                if (s_endOfInput is null)
                {
                    s_endOfInput = ResourceUtilities.GetResourceString("EndOfInputTokenName");
                }

                return s_endOfInput;
            }
        }

        private Parser(string expression, ParserOptions options, ElementLocation elementLocation, LoggingContext loggingContext)
        {
            // We currently have no support (and no scenarios) for disallowing property references in Conditions.
            Assumed.True((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

            _expression = expression;
            _options = options;
            _elementLocation = elementLocation;
            _position = 0;
            _errorPosition = -1;
            _loggingServices = loggingContext?.LoggingService;
            _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
        }

        //
        // Main entry point for parser.
        // You pass in the expression you want to parse, and you get a
        // ParseResult containing either the parsed expression tree or error information.
        //
        internal static ParseResult Parse(string expression, ParserOptions options, ElementLocation elementLocation, LoggingContext loggingContext = null)
        {
            var parser = new Parser(expression, options, elementLocation, loggingContext);
            return parser.ParseCore();
        }

        private ParseResult ParseCore()
        {
            if (!Advance())
            {
                return ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);
            }

            if (!TryParseExpr(out GenericExpressionNode node))
            {
                Assumed.NotNull(_errorResource);
                return ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);
            }

            if (!IsNext(Token.TokenType.EndOfInput))
            {
                UnexpectedTokenInCondition();
                return ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);
            }

            return ParseResult.Success(node);
        }

        private bool UnexpectedTokenInConditionAndReturn<T>(out T result)
            where T : class
        {
            result = null;
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
            _errorArgs = [_expression, _current.String, _errorPosition];
        }

        private bool SetScanError(int position, string resource, string extraArg = null)
        {
            _errorPosition = position;
            _errorResource = resource;
            _errorArgs = extraArg is not null
                ? [_expression, position, extraArg]
                : [_expression, position];

            return false;
        }

        [MemberNotNullWhen(true, nameof(_errorResource))]
        [MemberNotNullWhen(true, nameof(_errorArgs))]
        private bool HasError => _errorResource is not null;

        private bool TryParseExpr(out GenericExpressionNode result)
        {
            if (!TryParseBooleanTerm(out GenericExpressionNode node))
            {
                result = null;
                return false;
            }

            if (!IsNext(Token.TokenType.EndOfInput))
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

        private bool TryParseExprPrime(GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (Same(Token.TokenType.EndOfInput))
            {
                result = lhs;
                return true;
            }

            if (Same(Token.TokenType.Or))
            {
                if (!TryParseBooleanTerm(out GenericExpressionNode rhs))
                {
                    result = null;
                    return false;
                }

                OperatorExpressionNode orNode = new OrExpressionNode();
                orNode.LeftChild = lhs;
                orNode.RightChild = rhs;
                return TryParseExprPrime(orNode, out result);
            }

            // ExprPrime always shows up at the rightmost side of the grammar rhs,
            // the EndOfInput case takes care of things
            result = lhs;
            return true;
        }

        private bool TryParseBooleanTerm(out GenericExpressionNode result)
        {
            if (!TryParseRelationalExpr(out GenericExpressionNode node))
            {
                result = null;
                return false;
            }

            if (!IsNext(Token.TokenType.EndOfInput) && !TryParseBooleanTermPrime(node, out node))
            {
                result = null;
                return false;
            }

            result = node;
            return true;
        }

        private bool TryParseBooleanTermPrime(GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (IsNext(Token.TokenType.EndOfInput))
            {
                result = lhs;
                return true;
            }

            if (Same(Token.TokenType.And))
            {
                if (!TryParseRelationalExpr(out GenericExpressionNode rhs))
                {
                    result = null;
                    return false;
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = lhs;
                andNode.RightChild = rhs;
                return TryParseBooleanTermPrime(andNode, out result);
            }

            // Should this be error case?
            result = lhs;
            return true;
        }

        private bool TryParseRelationalExpr(out GenericExpressionNode result)
        {
            if (!TryParseFactor(out GenericExpressionNode lhs))
            {
                result = null;
                return false;
            }

            if (!TryParseRelationalOperation(out OperatorExpressionNode node))
            {
                result = lhs;
                return true;
            }

            if (!TryParseFactor(out GenericExpressionNode rhs))
            {
                result = null;
                return false;
            }

            node.LeftChild = lhs;
            node.RightChild = rhs;
            result = node;
            return true;
        }

        private bool TryParseRelationalOperation(out OperatorExpressionNode result)
        {
            if (Same(Token.TokenType.LessThan))
            {
                result = new LessThanExpressionNode();
                return true;
            }

            if (Same(Token.TokenType.GreaterThan))
            {
                result = new GreaterThanExpressionNode();
                return true;
            }

            if (Same(Token.TokenType.LessThanOrEqualTo))
            {
                result = new LessThanOrEqualExpressionNode();
                return true;
            }

            if (Same(Token.TokenType.GreaterThanOrEqualTo))
            {
                result = new GreaterThanOrEqualExpressionNode();
                return true;
            }

            if (Same(Token.TokenType.EqualTo))
            {
                result = new EqualExpressionNode();
                return true;
            }

            if (Same(Token.TokenType.NotEqualTo))
            {
                result = new NotEqualExpressionNode();
                return true;
            }

            result = null;
            return false;
        }

        private bool TryParseFactor(out GenericExpressionNode result)
        {
            // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
            if (TryParseArg(out result))
            {
                return true;
            }

            // If it's not one of those, check for other TokenTypes.
            Token current = _current;
            if (Same(Token.TokenType.Function))
            {
                if (!Same(Token.TokenType.LeftParenthesis))
                {
                    return UnexpectedTokenInConditionAndReturn(out result);
                }

                if (!TryParseArglist(out List<GenericExpressionNode> arglist))
                {
                    result = null;
                    return false;
                }

                if (!Same(Token.TokenType.RightParenthesis))
                {
                    return UnexpectedTokenInConditionAndReturn(out result);
                }

                result = new FunctionCallExpressionNode(current.String, arglist);
                return true;
            }

            if (Same(Token.TokenType.LeftParenthesis))
            {
                if (!TryParseExpr(out GenericExpressionNode child))
                {
                    result = null;
                    return false;
                }

                if (Same(Token.TokenType.RightParenthesis))
                {
                    result = child;
                    return true;
                }

                return UnexpectedTokenInConditionAndReturn(out result);
            }

            if (Same(Token.TokenType.Not))
            {
                if (!TryParseFactor(out GenericExpressionNode expr))
                {
                    result = null;
                    return false;
                }

                OperatorExpressionNode notNode = new NotExpressionNode();
                notNode.LeftChild = expr;
                result = notNode;
                return true;
            }

            return UnexpectedTokenInConditionAndReturn(out result);
        }

        private bool TryParseArglist(out List<GenericExpressionNode> result)
        {
            result = new List<GenericExpressionNode>();

            if (IsNext(Token.TokenType.RightParenthesis))
            {
                return true;
            }

            while (true)
            {
                if (!TryParseArg(out GenericExpressionNode arg))
                {
                    return UnexpectedTokenInConditionAndReturn(out result);
                }

                result.Add(arg);

                if (!Same(Token.TokenType.Comma))
                {
                    return true;
                }
            }
        }

        private bool TryParseArg(out GenericExpressionNode result)
        {
            Token current = _current;

            if (Same(Token.TokenType.String))
            {
                result = new StringExpressionNode(current.String, current.Expandable);
                return true;
            }

            if (Same(Token.TokenType.Numeric))
            {
                result = new NumericExpressionNode(current.String);
                return true;
            }

            if (Same(Token.TokenType.Property))
            {
                result = new StringExpressionNode(current.String, expandable: true);
                return true;
            }

            if (Same(Token.TokenType.ItemMetadata))
            {
                result = new StringExpressionNode(current.String, expandable: true);
                return true;
            }

            if (Same(Token.TokenType.ItemList))
            {
                result = new StringExpressionNode(current.String, expandable: true);
                return true;
            }

            result = null;
            return false;
        }

        private bool IsNext(Token.TokenType type)
            => _current.IsToken(type);

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

            if (_current?.IsToken(Token.TokenType.EndOfInput) == true)
            {
                return true;
            }

            SkipWhiteSpace();

            // Update error position after skipping whitespace
            _errorPosition = _position + 1;

            if (_position >= _expression.Length)
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
                    if (!ParseProperty())
                    {
                        return false;
                    }

                    break;

                case '%':
                    if (!ParseItemMetadata())
                    {
                        return false;
                    }

                    break;

                case '@':
                    int start = _position;
                    if ((_options & ParserOptions.AllowItemLists) == 0 && NextIs('('))
                    {
                        return SetScanError(start + 1, "ItemListNotAllowedInThisConditional");
                    }

                    if (!ParseItemList())
                    {
                        return false;
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
                        return SetScanError(_position + 1, "IllFormedEqualsInCondition", unexpectedlyFound);
                    }

                    break;

                case '\'':
                    if (!ParseQuotedString())
                    {
                        return false;
                    }

                    break;

                default:
                    if (!ParseRemaining())
                    {
                        return false;
                    }

                    break;
            }

            return true;
        }

        /// <summary>
        /// Parses either the $(propertyname) syntax or the %(metadataname) syntax,
        /// and returns the parsed string beginning with the '$' or '%', and ending with the
        /// closing parenthesis.
        /// </summary>
        private string ParsePropertyOrItemMetadata()
        {
            int start = _position;
            _position++;

            if (_position < _expression.Length && _expression[_position] != '(')
            {
                SetScanError(start + 1, "IllFormedPropertyOpenParenthesisInCondition", _expression[_position].ToString());
                return null;
            }

            var result = ScanForPropertyExpressionEnd(_expression, _position++, out int indexResult);
            if (!result)
            {
                SetScanError(indexResult, "IllFormedPropertySpaceInCondition", _expression[indexResult].ToString());
                return null;
            }

            _position = indexResult;
            if (_position >= _expression.Length)
            {
                SetScanError(start + 1, "IllFormedPropertyCloseParenthesisInCondition", EndOfInput);
                return null;
            }

            _position++;
            return _expression.Substring(start, _position - start);
        }

        /// <summary>
        /// Scan for the end of the property expression
        /// </summary>
        /// <param name="expression">property expression to parse</param>
        /// <param name="index">current index to start from</param>
        /// <param name="indexResult">If successful, the index corresponds to the end of the property expression.
        /// In case of scan failure, it is the error position index.</param>
        /// <returns>result indicating whether or not the scan was successful.</returns>
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

                        index++;
                    }
                }
            }

            indexResult = index;
            return true;
        }

        /// <summary>
        /// Parses a string of the form $(propertyname).
        /// </summary>
        private bool ParseProperty()
        {
            string propertyExpression = ParsePropertyOrItemMetadata();

            if (propertyExpression is null)
            {
                return false;
            }

            _current = new Token(Token.TokenType.Property, propertyExpression);
            return true;
        }

        /// <summary>
        /// Parses a string of the form %(itemmetadataname).
        /// </summary>
        private bool ParseItemMetadata()
        {
            string itemMetadataExpression = ParsePropertyOrItemMetadata();

            if (itemMetadataExpression is null)
            {
                // Override the error resource set by ParsePropertyOrItemMetadata
                // to the generic "UnexpectedCharacter" message for metadata.
                _errorResource = "UnexpectedCharacterInCondition";
                return false;
            }

            _current = new Token(Token.TokenType.ItemMetadata, itemMetadataExpression);

            if (!CheckForUnexpectedMetadata(itemMetadataExpression))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Helper to verify that any AllowBuiltInMetadata or AllowCustomMetadata
        /// specifications are not respected.
        /// Returns true if it is ok, otherwise false.
        /// </summary>
        private bool CheckForUnexpectedMetadata(string expression)
        {
            if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
            {
                return true;
            }

            // start and end delimit the metadata name within expression (end is exclusive).
            int start = 0;
            int end = expression.Length;

            if (expression is ['%', '(', .., ')'])
            {
                start = 2;
                end -= 1;
            }

            int dotIndex = expression.IndexOf('.', start);

            // Note: The '.' can't be the first or last character.
            if (dotIndex > start && dotIndex < end - 1)
            {
                start = dotIndex + 1;
            }

            string name = start > 0
                ? expression.Substring(start, end - start)
                : expression;

            bool isItemSpecModifier = ItemSpecModifiers.IsItemSpecModifier(name);

            if (((_options & ParserOptions.AllowBuiltInMetadata) == 0) && isItemSpecModifier)
            {
                return SetScanError(_position, "BuiltInMetadataNotAllowedInThisConditional", name);
            }

            if (((_options & ParserOptions.AllowCustomMetadata) == 0) && !isItemSpecModifier)
            {
                return SetScanError(_position, "CustomMetadataNotAllowedInThisConditional", name);
            }

            return true;
        }

        private bool ParseInternalItemList()
        {
            int start = _position;
            _position++;

            if (_position < _expression.Length && _expression[_position] != '(')
            {
                return SetScanError(start + 1, "IllFormedItemListOpenParenthesisInCondition");
            }

            _position++;
            bool fInReplacement = false;
            int parenToClose = 0;
            while (_position < _expression.Length)
            {
                if (_expression[_position] == '\'')
                {
                    fInReplacement = !fInReplacement;
                }
                else if (_expression[_position] == '(' && !fInReplacement)
                {
                    parenToClose++;
                }
                else if (_expression[_position] == ')' && !fInReplacement)
                {
                    if (parenToClose == 0)
                    {
                        break;
                    }

                    parenToClose--;
                }

                _position++;
            }

            if (_position >= _expression.Length)
            {
                return SetScanError(
                    start + 1,
                    fInReplacement ? "IllFormedItemListQuoteInCondition" : "IllFormedItemListCloseParenthesisInCondition");
            }

            _position++;
            return true;
        }

        private bool ParseItemList()
        {
            int start = _position;
            if (!ParseInternalItemList())
            {
                return false;
            }

            _current = new Token(Token.TokenType.ItemList, _expression.Substring(start, _position - start));
            return true;
        }

        /// <summary>
        /// Parse any part of the conditional expression that is quoted. It may contain a property, item, or
        /// metadata element that needs expansion during evaluation.
        /// </summary>
        private bool ParseQuotedString()
        {
            _position++;
            int start = _position;
            bool expandable = false;
            while (_position < _expression.Length && _expression[_position] != '\'')
            {
                if (_expression[_position] == '%' && NextIs('('))
                {
                    expandable = true;
                    string name = string.Empty;

                    int endOfName = _expression.IndexOf(')', _position) - 1;
                    if (endOfName < 0)
                    {
                        endOfName = _expression.Length - 1;
                    }

                    if (_position + 3 < _expression.Length)
                    {
                        name = _expression.Substring(_position + 2, endOfName - _position - 2 + 1);
                    }

                    if (!CheckForUnexpectedMetadata(name))
                    {
                        return false;
                    }
                }
                else if (_expression[_position] == '@' && NextIs('('))
                {
                    expandable = true;

                    if ((_options & ParserOptions.AllowItemLists) == 0)
                    {
                        return SetScanError(start + 1, "ItemListNotAllowedInThisConditional");
                    }

                    ParseInternalItemList();
                    continue;
                }
                else if (_expression[_position] == '$' && NextIs('('))
                {
                    expandable = true;
                }
                else if (_expression[_position] == '%')
                {
                    expandable = true;
                }

                _position++;
            }

            if (_position >= _expression.Length)
            {
                return SetScanError(start, "IllFormedQuotedStringInCondition");
            }

            string originalTokenString = _expression.Substring(start, _position - start);
            _current = new Token(Token.TokenType.String, originalTokenString, expandable);
            _position++;
            return true;
        }

        private bool ParseRemaining()
        {
            int start = _position;
            if (CharacterUtilities.IsNumberStart(_expression[_position]))
            {
                if (!ParseNumeric(start))
                {
                    return false;
                }
            }
            else if (CharacterUtilities.IsSimpleStringStart(_expression[_position]))
            {
                if (!ParseSimpleStringOrFunction(start))
                {
                    return false;
                }
            }
            else
            {
                return SetScanError(
                    start + 1,
                    "UnexpectedCharacterInCondition",
                    _expression[_position].ToString());
            }

            return true;
        }

        // There is a bug here that spaces are not required around 'and' and 'or'. For example,
        // this works perfectly well:
        // Condition="%(a.Identity)!=''and%(a.m)=='1'"
        // Since people now depend on this behavior, we must not change it.
        private bool ParseSimpleStringOrFunction(int start)
        {
            SkipSimpleStringChars();

            if (_expression.AsSpan(start, _position - start).Equals("and", StringComparison.OrdinalIgnoreCase))
            {
                _current = Token.And;
            }
            else if (_expression.AsSpan(start, _position - start).Equals("or", StringComparison.OrdinalIgnoreCase))
            {
                _current = Token.Or;
            }
            else
            {
                int end = _position;

                SkipWhiteSpace();

                if (_position < _expression.Length && _expression[_position] == '(')
                {
                    _current = new Token(Token.TokenType.Function, _expression.Substring(start, end - start));
                }
                else
                {
                    string tokenValue = _expression.Substring(start, end - start);
                    _current = new Token(Token.TokenType.String, tokenValue);
                }
            }

            return true;
        }

        private bool ParseNumeric(int start)
        {
            if ((_expression.Length - _position) > 2 && _expression[_position] == '0' && (_expression[_position + 1] == 'x' || _expression[_position + 1] == 'X'))
            {
                _position += 2;
                SkipHexDigits();
                _current = new Token(Token.TokenType.Numeric, _expression.Substring(start, _position - start));
                return true;
            }

            if (CharacterUtilities.IsNumberStart(_expression[_position]))
            {
                if (_expression[_position] is '+' or '-')
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

                _current = new Token(Token.TokenType.Numeric, _expression.Substring(start, _position - start));
                return true;
            }

            return SetScanError(start + 1, "UnexpectedCharacterInCondition");
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

        private bool Same(Token.TokenType token)
            => IsNext(token) && Advance();
    }
}
