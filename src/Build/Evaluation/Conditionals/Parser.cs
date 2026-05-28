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
        {
            Assumed.NotNull(resource);
            Assumed.NotNull(args);
            Assumed.Positive(position);

            return new(node: null, resource, args, position, elementLocation);
        }

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
                return ErrorResult();
            }

            if (!TryParseExpr(out GenericExpressionNode node))
            {
                Assumed.NotNull(_errorResource);
                return ErrorResult();
            }

            if (!IsNext(Token.TokenType.EndOfInput))
            {
                UnexpectedTokenInCondition();
                return ErrorResult();
            }

            return ParseResult.Success(node);
        }

        private ParseResult ErrorResult()
            => ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);

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

        private bool SetErrorInfo(int position, string resource, string extraArg = null)
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

        private bool TryConsume(string s)
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

            _position += s.Length;
            return true;
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

            if (_current?.IsToken(Token.TokenType.EndOfInput) == true)
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
                        int start = _position;
                        if (!TryScanProperty())
                        {
                            return false;
                        }

                        _current = new Token(Token.TokenType.Property, _expression.Substring(start, _position - start));
                    }

                    break;

                case '%':
                    {
                        int start = _position;
                        if (!TryScanItemMetadata())
                        {
                            return false;
                        }

                        _current = new Token(Token.TokenType.ItemMetadata, _expression.Substring(start, _position - start));
                    }

                    break;

                case '@':
                    {
                        int start = _position;
                        if ((_options & ParserOptions.AllowItemLists) == 0 && NextIs('('))
                        {
                            return SetErrorInfo(start, "ItemListNotAllowedInThisConditional");
                        }

                        if (!TryScanItemList())
                        {
                            return false;
                        }

                        _current = new Token(Token.TokenType.ItemList, _expression.Substring(start, _position - start));
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
                        int start = _position + 1;
                        if (!TryScanQuotedString(out bool expandable))
                        {
                            return false;
                        }

                        _current = new Token(Token.TokenType.String, _expression.Substring(start, _position - start - 1), expandable);
                    }

                    break;

                default:
                    {
                        int start = _position;

                        if (ParseNumeric() ||
                            ParseSimpleStringOrFunction())
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
        private bool TryScanProperty()
        {
            int start = _position;

            Assume('$');

            if (!TryConsume('('))
            {
                return SetErrorInfo(start, "IllFormedPropertyOpenParenthesisInCondition");
            }

            var result = ScanForPropertyExpressionEnd(_expression, _position - 1, out int indexResult);
            if (!result)
            {
                return SetErrorInfo(indexResult, "IllFormedPropertySpaceInCondition");
            }

            _position = indexResult;
            if (AtEnd)
            {
                return SetErrorInfo(start, "IllFormedPropertyCloseParenthesisInCondition");
            }

            _position++;
            return true;
        }

        /// <summary>
        /// Scans an item metadata expression of the form %(metadataname).
        /// Expects _position at '%' on entry.
        /// </summary>
        private bool TryScanItemMetadata()
        {
            int start = _position;

            Assume('%');

            if (!TryConsume('('))
            {
                return SetErrorInfo(start, "IllFormedMetadataOpenParenthesisInCondition");
            }

            // Scan for the closing ')'. Metadata references are simply %(Name) or %(ItemType.Name).
            while (!AtEnd)
            {
                if (TryConsume(')'))
                {
                    string itemMetadataExpression = _expression.Substring(start, _position - start);
                    return CheckForUnexpectedMetadata(start, itemMetadataExpression);
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
        /// Helper to verify that any AllowBuiltInMetadata or AllowCustomMetadata
        /// specifications are not respected.
        /// Returns true if it is ok, otherwise false.
        /// </summary>
        private bool CheckForUnexpectedMetadata(int start, string expression)
        {
            if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
            {
                return true;
            }

            // start and end delimit the metadata name within expression (end is exclusive).
            int nameStart = 0;
            int nameEnd = expression.Length;

            if (expression is ['%', '(', .., ')'])
            {
                nameStart = 2;
                nameEnd -= 1;
            }

            int dotIndex = expression.IndexOf('.', nameStart);

            // Note: The '.' can't be the first or last character.
            if (dotIndex > nameStart && dotIndex < nameEnd - 1)
            {
                nameStart = dotIndex + 1;
            }

            string name = nameStart > 0
                ? expression.Substring(nameStart, nameEnd - nameStart)
                : expression;

            bool isItemSpecModifier = ItemSpecModifiers.IsItemSpecModifier(name);

            if (((_options & ParserOptions.AllowBuiltInMetadata) == 0) && isItemSpecModifier)
            {
                return SetErrorInfo(start, "BuiltInMetadataNotAllowedInThisConditional", name);
            }

            if (((_options & ParserOptions.AllowCustomMetadata) == 0) && !isItemSpecModifier)
            {
                return SetErrorInfo(start, "CustomMetadataNotAllowedInThisConditional", name);
            }

            return true;
        }

        /// <summary>
        /// Scans past an item list expression starting at the current position.
        /// Expects At('@') on entry.
        /// On success, _position is advanced past the closing ')'.
        /// </summary>
        private bool TryScanItemList()
        {
            int start = _position;

            Assume('@');

            if (!TryConsume('('))
            {
                return SetErrorInfo(start, "IllFormedItemListOpenParenthesisInCondition");
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
        /// Scans a quoted string that may contain property, item, or metadata expressions.
        /// Expects _position at opening quote on entry.
        /// On success, _position is advanced past the closing quote.
        /// </summary>
        private bool TryScanQuotedString(out bool expandable)
        {
            expandable = false;
            int start = _position;

            Assume('\'');

            while (!AtEnd)
            {
                if (TryConsume('\''))
                {
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
                    if ((_options & ParserOptions.AllowItemLists) == 0)
                    {
                        return SetErrorInfo(_position, "ItemListNotAllowedInThisConditional");
                    }

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
        private bool ParseSimpleStringOrFunction()
        {
            if (!CharacterUtilities.IsSimpleStringStart(_expression[_position]))
            {
                return false;
            }

            int start = _position;

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

                if (At('('))
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

        private bool ParseNumeric()
        {
            if (!CharacterUtilities.IsNumberStart(_expression[_position]))
            {
                return false;
            }

            int start = _position;

            if ((_expression.Length - _position) > 2 && At('0') && (_expression[_position + 1] == 'x' || _expression[_position + 1] == 'X'))
            {
                _position += 2;
                SkipHexDigits();
                _current = new Token(Token.TokenType.Numeric, _expression.Substring(start, _position - start));
                return true;
            }

            if (_expression[_position] is '+' or '-')
            {
                _position++;
            }

            do
            {
                SkipDigits();
                TryConsume('.');

                if (!AtEnd)
                {
                    SkipDigits();
                }
            }
            while (At('.'));

            _current = new Token(Token.TokenType.Numeric, _expression.Substring(start, _position - start));
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

        private void SkipSimpleStringChars()
        {
            while (!AtEnd && CharacterUtilities.IsSimpleStringChar(_expression[_position]))
            {
                _position++;
            }
        }

        private bool Same(Token.TokenType token)
            => IsNext(token) && Advance();
    }
}
