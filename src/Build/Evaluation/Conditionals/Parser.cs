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
    /// <remarks>
    /// UNDONE: When we copied over the conditionals code, we didn't copy over the unit tests for scanner, parser, and expression tree.
    /// </remarks>
    internal sealed class Parser
    {
        private readonly Scanner _lexer;
        private readonly string _expression;
        private readonly ElementLocation _elementLocation;

        private int _errorPosition;
        private string _errorResource;
        private object[] _errorArgs;

        // Older versions of MSBuild evaluated mixed 'and'/'or' conditions left-to-right
        // instead of giving 'and' higher precedence. When this was fixed, a compatibility
        // warning (MSB4130) was added to flag expressions that might now evaluate differently.
        // The warning fires for expressions with both 'and' and 'or' at the same level
        // without explicit parentheses.
        private readonly ILoggingService _loggingServices;
        private readonly BuildEventContext _logBuildEventContext;
        private bool _warnedForExpression;

        private Parser(string expression, ParserOptions options, ElementLocation elementLocation, LoggingContext loggingContext)
        {
            // We currently have no support (and no scenarios) for disallowing property references in Conditions.
            Assumed.True((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

            _expression = expression;
            _elementLocation = elementLocation;
            _loggingServices = loggingContext?.LoggingService;
            _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;

            _lexer = new Scanner(expression, options);
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
            if (!_lexer.Advance())
            {
                SetScannerError();
                return ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);
            }

            if (!TryParseExpr(out GenericExpressionNode node))
            {
                Assumed.NotNull(_errorResource);
                return ParseResult.Error(_errorResource, _errorArgs, _errorPosition, _elementLocation);
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
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

            _errorPosition = _lexer.GetErrorPosition();
            SetError("UnexpectedTokenInCondition", [_expression, _lexer.IsNextString(), _errorPosition]);
        }

        private bool SetScannerErrorAndReturn()
        {
            SetScannerError();
            return false;
        }

        private void SetScannerError()
        {
            if (HasError)
            {
                return;
            }

            _errorPosition = _lexer.GetErrorPosition();

            if (_lexer.UnexpectedlyFound != null)
            {
                SetError(_lexer.GetErrorResource(), [_expression, _errorPosition, _lexer.UnexpectedlyFound]);
            }
            else
            {
                SetError(_lexer.GetErrorResource(), [_expression, _errorPosition]);
            }
        }

        private void SetError(string resource, object[] args)
        {
            if (HasError)
            {
                return;
            }

            _errorPosition = _lexer.GetErrorPosition();
            _errorResource = resource;
            _errorArgs = args;
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

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
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

            if (!_lexer.IsNext(Token.TokenType.EndOfInput) && !TryParseBooleanTermPrime(node, out node))
            {
                result = null;
                return false;
            }

            result = node;
            return true;
        }

        private bool TryParseBooleanTermPrime(GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (_lexer.IsNext(Token.TokenType.EndOfInput))
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
            Token current = _lexer.CurrentToken;
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

            if (_lexer.IsNext(Token.TokenType.RightParenthesis))
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
            Token current = _lexer.CurrentToken;

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

        private bool Same(Token.TokenType token)
            => _lexer.IsNext(token) && (_lexer.Advance() || SetScannerErrorAndReturn());
    }
}
