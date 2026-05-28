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

            if (!IsNext(TokenKind.EndOfInput))
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
            _errorArgs = [_expression, _current.ToString(), _errorPosition];
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

        private bool TryParseExprPrime(GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (Same(TokenKind.EndOfInput))
            {
                result = lhs;
                return true;
            }

            if (Same(TokenKind.Or))
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

            if (!IsNext(TokenKind.EndOfInput) && !TryParseBooleanTermPrime(node, out node))
            {
                result = null;
                return false;
            }

            result = node;
            return true;
        }

        private bool TryParseBooleanTermPrime(GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (IsNext(TokenKind.EndOfInput))
            {
                result = lhs;
                return true;
            }

            if (Same(TokenKind.And))
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
            if (Same(TokenKind.LessThan))
            {
                result = new LessThanExpressionNode();
                return true;
            }

            if (Same(TokenKind.GreaterThan))
            {
                result = new GreaterThanExpressionNode();
                return true;
            }

            if (Same(TokenKind.LessThanOrEqualTo))
            {
                result = new LessThanOrEqualExpressionNode();
                return true;
            }

            if (Same(TokenKind.GreaterThanOrEqualTo))
            {
                result = new GreaterThanOrEqualExpressionNode();
                return true;
            }

            if (Same(TokenKind.EqualTo))
            {
                result = new EqualExpressionNode();
                return true;
            }

            if (Same(TokenKind.NotEqualTo))
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
            if (Same(TokenKind.FunctionName))
            {
                if (!Same(TokenKind.LeftParenthesis))
                {
                    return UnexpectedTokenInConditionAndReturn(out result);
                }

                if (!TryParseArglist(out List<GenericExpressionNode> arglist))
                {
                    result = null;
                    return false;
                }

                if (!Same(TokenKind.RightParenthesis))
                {
                    return UnexpectedTokenInConditionAndReturn(out result);
                }

                result = new FunctionCallExpressionNode(current.Text.ToString(), arglist);
                return true;
            }

            if (Same(TokenKind.LeftParenthesis))
            {
                if (!TryParseExpr(out GenericExpressionNode child))
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

            if (IsNext(TokenKind.RightParenthesis))
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

                if (!Same(TokenKind.Comma))
                {
                    return true;
                }
            }
        }

        private bool TryParseArg(out GenericExpressionNode result)
        {
            Token current = _current;

            if (Same(TokenKind.String))
            {
                result = new StringExpressionNode(current.Text.ToString(), current.Expandable);
                return true;
            }

            if (Same(TokenKind.Number))
            {
                result = new NumericExpressionNode(current.Text.ToString());
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
                        int start = _position;
                        if (!TryScanProperty())
                        {
                            return false;
                        }

                        _current = Token.Property(_expression.AsMemory(start, _position - start));
                    }

                    break;

                case '%':
                    {
                        int start = _position;
                        if (!TryScanItemMetadata())
                        {
                            return false;
                        }

                        _current = Token.ItemMetadata(_expression.AsMemory(start, _position - start));
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

                        _current = Token.ItemList(_expression.AsMemory(start, _position - start));
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

                        _current = Token.String(_expression.AsMemory(start, _position - start - 1), expandable);
                    }

                    break;

                default:
                    {
                        int start = _position;

                        if (TryScanNumber())
                        {
                            _current = Token.Number(_expression.AsMemory(start, _position - start));
                            return true;
                        }

                        if (ParseSimpleStringOrFunction())
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
                    ReadOnlySpan<char> itemMetadataExpression = _expression.AsSpan(start, _position - start);
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
        /// Helper to verify that any AllowBuiltInMetadata or AllowCustomMetadata
        /// specifications are not respected.
        /// Returns true if it is ok, otherwise false.
        /// </summary>
        private bool CheckForUnexpectedMetadata(int start, ReadOnlySpan<char> expression)
        {
            if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
            {
                return true;
            }

            ReadOnlySpan<char> name = expression;

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

                _current = At('(')
                    ? Token.FunctionName(_expression.AsMemory(start, end - start))
                    : Token.String(_expression.AsMemory(start, end - start));
            }

            return true;
        }

        private bool TryScanNumber()
        {
            if (!CharacterUtilities.IsNumberStart(_expression[_position]))
            {
                return false;
            }

            if ((_expression.Length - _position) > 2 && At('0') && (_expression[_position + 1] is 'x' or 'X'))
            {
                _position += 2;
                SkipHexDigits();
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

        private bool Same(TokenKind token)
            => IsNext(token) && Advance();
    }
}
