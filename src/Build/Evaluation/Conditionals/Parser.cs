// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
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
    /// <remarks>
    /// UNDONE: When we copied over the conditionals code, we didn't copy over the unit tests for scanner, parser, and expression tree.
    /// </remarks>
    internal sealed class Parser
    {
        private readonly string _expression;
        private readonly ParserOptions _options;
        private readonly ElementLocation _elementLocation;
        private readonly Scanner _lexer;

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

        internal int errorPosition = 0; // useful for unit tests

        public Parser(
            string expression,
            ParserOptions optionSettings,
            ElementLocation elementLocation,
            LoggingContext loggingContext = null)
        {
            // We currently have no support (and no scenarios) for disallowing property references in conditions.
            ErrorUtilities.VerifyThrow((optionSettings & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

            _expression = expression;
            _options = optionSettings;
            _elementLocation = elementLocation;

            _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
            _loggingService = loggingContext?.LoggingService;

            _lexer = new Scanner(expression, _options);

            if (!_lexer.Advance())
            {
                errorPosition = _lexer.GetErrorPosition();
                ThrowInvalidProject();
            }
        }

        /// <summary>
        /// Main entry point for parser.
        /// You pass in the expression you want to parse, and you get an
        /// expression tree out the back end.
        /// </summary>
        internal static GenericExpressionNode Parse(
            string expression,
            ParserOptions optionSettings,
            ElementLocation elementLocation,
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
            if (!_lexer.IsNext(TokenKind.EndOfInput))
            {
                errorPosition = _lexer.GetErrorPosition();
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
            if (!_lexer.IsNext(TokenKind.EndOfInput))
            {
                node = ExprPrime(node);
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

        private GenericExpressionNode ExprPrime(GenericExpressionNode lhs)
        {
            if (Same(TokenKind.EndOfInput))
            {
                return lhs;
            }
            else if (Same(TokenKind.Or))
            {
                OperatorExpressionNode orNode = new OrExpressionNode();
                GenericExpressionNode rhs = BooleanTerm();
                orNode.LeftChild = lhs;
                orNode.RightChild = rhs;
                return ExprPrime(orNode);
            }
            else
            {
                // I think this is ok.  ExprPrime always shows up at
                // the rightmost side of the grammar rhs, the EndOfInput case
                // takes care of things
                return lhs;
            }
        }

        private GenericExpressionNode BooleanTerm()
        {
            GenericExpressionNode node = RelationalExpr();
            if (node == null)
            {
                errorPosition = _lexer.GetErrorPosition();
                ThrowUnexpectedTokenInCondition();
            }

            if (!_lexer.IsNext(TokenKind.EndOfInput))
            {
                node = BooleanTermPrime(node);
            }
            return node;
        }

        private GenericExpressionNode BooleanTermPrime(GenericExpressionNode lhs)
        {
            if (_lexer.IsNext(TokenKind.EndOfInput))
            {
                return lhs;
            }
            else if (Same(TokenKind.And))
            {
                GenericExpressionNode rhs = RelationalExpr();
                if (rhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowUnexpectedTokenInCondition();
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = lhs;
                andNode.RightChild = rhs;
                return BooleanTermPrime(andNode);
            }
            else
            {
                // Should this be error case?
                return lhs;
            }
        }

        private GenericExpressionNode RelationalExpr()
        {
            {
                GenericExpressionNode lhs = Factor();
                if (lhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowUnexpectedTokenInCondition();
                }

                OperatorExpressionNode node = RelationalOperation();
                if (node == null)
                {
                    return lhs;
                }
                GenericExpressionNode rhs = Factor();
                node.LeftChild = lhs;
                node.RightChild = rhs;
                return node;
            }
        }

        private OperatorExpressionNode RelationalOperation()
        {
            OperatorExpressionNode node = null;
            if (Same(TokenKind.LessThan))
            {
                node = new LessThanExpressionNode();
            }
            else if (Same(TokenKind.GreaterThan))
            {
                node = new GreaterThanExpressionNode();
            }
            else if (Same(TokenKind.LessThanOrEqualTo))
            {
                node = new LessThanOrEqualExpressionNode();
            }
            else if (Same(TokenKind.GreaterThanOrEqualTo))
            {
                node = new GreaterThanOrEqualExpressionNode();
            }
            else if (Same(TokenKind.EqualTo))
            {
                node = new EqualExpressionNode();
            }
            else if (Same(TokenKind.NotEqualTo))
            {
                node = new NotEqualExpressionNode();
            }
            return node;
        }

        private GenericExpressionNode Factor()
        {
            // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
            GenericExpressionNode arg = Arg();

            // If it's one of those, return it.
            if (arg != null)
            {
                return arg;
            }

            // If it's not one of those, check for other TokenTypes.
            Token current = _lexer.CurrentToken;
            if (Same(TokenKind.Function))
            {
                if (!Same(TokenKind.LeftParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowUnexpectedTokenInCondition();
                    return null;
                }
                var arglist = new List<GenericExpressionNode>();
                Arglist(arglist);
                if (!Same(TokenKind.RightParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowUnexpectedTokenInCondition();
                    return null;
                }
                return new FunctionCallExpressionNode(current.Text, arglist);
            }
            else if (Same(TokenKind.LeftParenthesis))
            {
                GenericExpressionNode child = Expr();
                if (Same(TokenKind.RightParenthesis))
                {
                    return child;
                }
                else
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowUnexpectedTokenInCondition();
                }
            }
            else if (Same(TokenKind.Not))
            {
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor();
                if (expr == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowUnexpectedTokenInCondition();
                }
                notNode.LeftChild = expr;
                return notNode;
            }
            else
            {
                errorPosition = _lexer.GetErrorPosition();
                ThrowUnexpectedTokenInCondition();
            }
            return null;
        }

        private void Arglist(List<GenericExpressionNode> arglist)
        {
            if (!_lexer.IsNext(TokenKind.RightParenthesis))
            {
                Args(arglist);
            }
        }

        private void Args(List<GenericExpressionNode> arglist)
        {
            GenericExpressionNode arg = Arg();
            arglist.Add(arg);
            if (Same(TokenKind.Comma))
            {
                Args(arglist);
            }
        }

        private GenericExpressionNode Arg()
        {
            Token current = _lexer.CurrentToken;
            if (Same(TokenKind.String))
            {
                return new StringExpressionNode(current.Text, current.IsExpandable);
            }
            else if (Same(TokenKind.Numeric))
            {
                return new NumericExpressionNode(current.Text);
            }
            else if (Same(TokenKind.Property))
            {
                return new StringExpressionNode(current.Text, true /* requires expansion */);
            }
            else if (Same(TokenKind.ItemMetadata))
            {
                return new StringExpressionNode(current.Text, true /* requires expansion */);
            }
            else if (Same(TokenKind.ItemList))
            {
                return new StringExpressionNode(current.Text, true /* requires expansion */);
            }
            else
            {
                return null;
            }
        }

        private bool Same(TokenKind token)
        {
            if (_lexer.IsNext(token))
            {
                if (!_lexer.Advance())
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ThrowInvalidProject();
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ThrowInvalidProject()
        {
            if (_lexer.UnexpectedlyFound != null)
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), _expression, errorPosition, _lexer.UnexpectedlyFound);
            }
            else
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), _expression, errorPosition);
            }
        }

        private void ThrowUnexpectedTokenInCondition()
            => ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _expression, _lexer.IsNextString(), errorPosition);
    }
}
