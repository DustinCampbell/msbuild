// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using ElementLocation = Microsoft.Build.Construction.ElementLocation;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;

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
    /// This class implements the grammar for complex conditionals.
    ///
    /// The usage is:
    ///    Parser p = new Parser(CultureInfo);
    ///    ExpressionTree t = p.Parse(expression, XmlNode);
    ///
    /// The expression tree can then be evaluated and re-evaluated as needed.
    /// </summary>
    /// <remarks>
    /// UNDONE: When we copied over the conditionals code, we didn't copy over the unit tests for scanner, parser, and expression tree.
    /// </remarks>
    internal sealed class Parser
    {
        private Scanner _lexer;
        private string _expression;
        private ParserOptions _options;
        private ElementLocation _elementLocation;
        internal int errorPosition = 0; // useful for unit tests

        #region REMOVE_COMPAT_WARNING

        private bool _warnedForExpression = false;

        private BuildEventContext _logBuildEventContext;
        /// <summary>
        ///  Location contextual information which are attached to logging events to
        ///  say where they are in relation to the process, engine, project, target,task which is executing
        /// </summary>
        internal BuildEventContext LogBuildEventContext
        {
            get
            {
                return _logBuildEventContext;
            }
            set
            {
                _logBuildEventContext = value;
            }
        }
        private ILoggingService _loggingServices;
        /// <summary>
        /// Engine Logging Service reference where events will be logged to
        /// </summary>
        internal ILoggingService LoggingServices
        {
            set
            {
                _loggingServices = value;
            }

            get
            {
                return _loggingServices;
            }
        }
        #endregion

        internal Parser()
        {
            // nothing to see here, move along.
        }

        //
        // Main entry point for parser.
        // You pass in the expression you want to parse, and you get an
        // ExpressionTree out the back end.
        //
        internal GenericExpressionNode Parse(string expression, ParserOptions optionSettings, ElementLocation elementLocation)
        {
            // We currently have no support (and no scenarios) for disallowing property references
            // in Conditions.
            ErrorUtilities.VerifyThrow(0 != (optionSettings & ParserOptions.AllowProperties),
                "Properties should always be allowed.");

            _expression = expression;
            _options = optionSettings;
            _elementLocation = elementLocation;

            _lexer = new Scanner(expression, _options);
            if (!Advance())
            {
                // We should never get here because Advance always throws on error.
                ErrorUtilities.ThrowInternalErrorUnreachable();
            }

            GenericExpressionNode node = Expr();
            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
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
            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = ExprPrime(node);
            }

            #region REMOVE_COMPAT_WARNING
            // Check for potential change in behavior
            if (LoggingServices != null && !_warnedForExpression &&
                node.PotentialAndOrConflict())
            {
                // We only want to warn once even if there multiple () sub expressions
                _warnedForExpression = true;

                // Log a warning regarding the fact the expression may have been evaluated
                // incorrectly in earlier version of MSBuild
                LoggingServices.LogWarning(_logBuildEventContext, null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", _expression);
            }
            #endregion

            return node;
        }

        private GenericExpressionNode ExprPrime(GenericExpressionNode lhs)
        {
            if (Same(Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(Token.TokenType.Or))
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
                ThrowUnexpectedTokenInCondition();
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                node = BooleanTermPrime(node);
            }
            return node;
        }

        private GenericExpressionNode BooleanTermPrime(GenericExpressionNode lhs)
        {
            if (_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                return lhs;
            }
            else if (Same(Token.TokenType.And))
            {
                GenericExpressionNode rhs = RelationalExpr();
                if (rhs == null)
                {
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
            if (Same(Token.TokenType.LessThan))
            {
                node = new LessThanExpressionNode();
            }
            else if (Same(Token.TokenType.GreaterThan))
            {
                node = new GreaterThanExpressionNode();
            }
            else if (Same(Token.TokenType.LessThanOrEqualTo))
            {
                node = new LessThanOrEqualExpressionNode();
            }
            else if (Same(Token.TokenType.GreaterThanOrEqualTo))
            {
                node = new GreaterThanOrEqualExpressionNode();
            }
            else if (Same(Token.TokenType.EqualTo))
            {
                node = new EqualExpressionNode();
            }
            else if (Same(Token.TokenType.NotEqualTo))
            {
                node = new NotEqualExpressionNode();
            }
            return node;
        }

        private GenericExpressionNode Factor()
        {
            // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
            GenericExpressionNode arg = this.Arg();

            // If it's one of those, return it.
            if (arg != null)
            {
                return arg;
            }

            // If it's not one of those, check for other TokenTypes.
            Token current = _lexer.CurrentToken;
            if (Same(Token.TokenType.Function))
            {
                if (!Same(Token.TokenType.LeftParenthesis))
                {
                    ThrowUnexpectedTokenInCondition();
                    return null;
                }
                var arglist = new List<GenericExpressionNode>();
                Arglist(arglist);
                if (!Same(Token.TokenType.RightParenthesis))
                {
                    ThrowUnexpectedTokenInCondition();
                    return null;
                }
                return new FunctionCallExpressionNode(current.String, arglist);
            }
            else if (Same(Token.TokenType.LeftParenthesis))
            {
                GenericExpressionNode child = Expr();
                if (Same(Token.TokenType.RightParenthesis))
                {
                    return child;
                }
                else
                {
                    ThrowUnexpectedTokenInCondition();
                }
            }
            else if (Same(Token.TokenType.Not))
            {
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor();
                if (expr == null)
                {
                    ThrowUnexpectedTokenInCondition();
                }
                notNode.LeftChild = expr;
                return notNode;
            }
            else
            {
                ThrowUnexpectedTokenInCondition();
            }
            return null;
        }

        private void Arglist(List<GenericExpressionNode> arglist)
        {
            if (!_lexer.IsNext(Token.TokenType.RightParenthesis))
            {
                Args(arglist);
            }
        }

        private void Args(List<GenericExpressionNode> arglist)
        {
            GenericExpressionNode arg = Arg();
            arglist.Add(arg);
            if (Same(Token.TokenType.Comma))
            {
                Args(arglist);
            }
        }

        private GenericExpressionNode Arg()
        {
            Token current = _lexer.CurrentToken;
            if (Same(Token.TokenType.String))
            {
                return new StringExpressionNode(current.String, current.Expandable);
            }
            else if (Same(Token.TokenType.Numeric))
            {
                return new NumericExpressionNode(current.String);
            }
            else if (Same(Token.TokenType.Property))
            {
                return new StringExpressionNode(current.String, true /* requires expansion */);
            }
            else if (Same(Token.TokenType.ItemMetadata))
            {
                return new StringExpressionNode(current.String, true /* requires expansion */);
            }
            else if (Same(Token.TokenType.ItemList))
            {
                return new StringExpressionNode(current.String, true /* requires expansion */);
            }
            else
            {
                return null;
            }
        }

        private bool Same(Token.TokenType token)
            => _lexer.IsNext(token) && Advance();

        private bool Advance()
        {
            if (_lexer.Advance())
            {
                return true;
            }

            errorPosition = _lexer.GetErrorPosition();

            if (_lexer.UnexpectedlyFound is string unexpectedlyFound)
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), _expression, errorPosition, unexpectedlyFound);
            }
            else
            {
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), _expression, errorPosition);
            }

            return true;
        }

        private void ThrowUnexpectedTokenInCondition()
        {
            errorPosition = _lexer.GetErrorPosition();
            ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _expression, _lexer.IsNextString(), errorPosition);
        }
    }
}
