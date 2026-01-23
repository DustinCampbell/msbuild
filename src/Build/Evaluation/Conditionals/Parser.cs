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

            _options = optionSettings;
            _elementLocation = elementLocation;

            _lexer = new Scanner(expression, _options);
            if (!_lexer.Advance())
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
            }
            GenericExpressionNode node = Expr(expression);
            if (!_lexer.IsNext(TokenKind.EndOfInput))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }
            return node;
        }

        //
        // Top node of grammar
        //    See grammar for how the following methods relate to each
        //    other.
        //
        private GenericExpressionNode Expr(string expression)
        {
            GenericExpressionNode node = BooleanTerm(expression);
            if (!_lexer.IsNext(TokenKind.EndOfInput))
            {
                node = ExprPrime(expression, node);
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
                LoggingServices.LogWarning(_logBuildEventContext, null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", expression);
            }
            #endregion

            return node;
        }

        private GenericExpressionNode ExprPrime(string expression, GenericExpressionNode lhs)
        {
            if (Same(expression, TokenKind.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, TokenKind.Or))
            {
                OperatorExpressionNode orNode = new OrExpressionNode();
                GenericExpressionNode rhs = BooleanTerm(expression);
                orNode.LeftChild = lhs;
                orNode.RightChild = rhs;
                return ExprPrime(expression, orNode);
            }
            else
            {
                // I think this is ok.  ExprPrime always shows up at
                // the rightmost side of the grammar rhs, the EndOfInput case
                // takes care of things
                return lhs;
            }
        }

        private GenericExpressionNode BooleanTerm(string expression)
        {
            GenericExpressionNode node = RelationalExpr(expression);
            if (node == null)
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }

            if (!_lexer.IsNext(TokenKind.EndOfInput))
            {
                node = BooleanTermPrime(expression, node);
            }
            return node;
        }

        private GenericExpressionNode BooleanTermPrime(string expression, GenericExpressionNode lhs)
        {
            if (_lexer.IsNext(TokenKind.EndOfInput))
            {
                return lhs;
            }
            else if (Same(expression, TokenKind.And))
            {
                GenericExpressionNode rhs = RelationalExpr(expression);
                if (rhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = lhs;
                andNode.RightChild = rhs;
                return BooleanTermPrime(expression, andNode);
            }
            else
            {
                // Should this be error case?
                return lhs;
            }
        }

        private GenericExpressionNode RelationalExpr(string expression)
        {
            {
                GenericExpressionNode lhs = Factor(expression);
                if (lhs == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }

                OperatorExpressionNode node = RelationalOperation(expression);
                if (node == null)
                {
                    return lhs;
                }
                GenericExpressionNode rhs = Factor(expression);
                node.LeftChild = lhs;
                node.RightChild = rhs;
                return node;
            }
        }

        private OperatorExpressionNode RelationalOperation(string expression)
        {
            OperatorExpressionNode node = null;
            if (Same(expression, TokenKind.LessThan))
            {
                node = new LessThanExpressionNode();
            }
            else if (Same(expression, TokenKind.GreaterThan))
            {
                node = new GreaterThanExpressionNode();
            }
            else if (Same(expression, TokenKind.LessThanOrEqualTo))
            {
                node = new LessThanOrEqualExpressionNode();
            }
            else if (Same(expression, TokenKind.GreaterThanOrEqualTo))
            {
                node = new GreaterThanOrEqualExpressionNode();
            }
            else if (Same(expression, TokenKind.EqualTo))
            {
                node = new EqualExpressionNode();
            }
            else if (Same(expression, TokenKind.NotEqualTo))
            {
                node = new NotEqualExpressionNode();
            }
            return node;
        }

        private GenericExpressionNode Factor(string expression)
        {
            // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
            GenericExpressionNode arg = this.Arg(expression);

            // If it's one of those, return it.
            if (arg != null)
            {
                return arg;
            }

            // If it's not one of those, check for other TokenTypes.
            Token current = _lexer.CurrentToken;
            if (Same(expression, TokenKind.Function))
            {
                if (!Same(expression, TokenKind.LeftParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _lexer.IsNextString(), errorPosition);
                    return null;
                }
                var arglist = new List<GenericExpressionNode>();
                Arglist(expression, arglist);
                if (!Same(expression, TokenKind.RightParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                    return null;
                }
                return new FunctionCallExpressionNode(current.Text, arglist);
            }
            else if (Same(expression, TokenKind.LeftParenthesis))
            {
                GenericExpressionNode child = Expr(expression);
                if (Same(expression, TokenKind.RightParenthesis))
                {
                    return child;
                }
                else
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
            }
            else if (Same(expression, TokenKind.Not))
            {
                OperatorExpressionNode notNode = new NotExpressionNode();
                GenericExpressionNode expr = Factor(expression);
                if (expr == null)
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                }
                notNode.LeftChild = expr;
                return notNode;
            }
            else
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }
            return null;
        }

        private void Arglist(string expression, List<GenericExpressionNode> arglist)
        {
            if (!_lexer.IsNext(TokenKind.RightParenthesis))
            {
                Args(expression, arglist);
            }
        }

        private void Args(string expression, List<GenericExpressionNode> arglist)
        {
            GenericExpressionNode arg = Arg(expression);
            arglist.Add(arg);
            if (Same(expression, TokenKind.Comma))
            {
                Args(expression, arglist);
            }
        }

        private GenericExpressionNode Arg(string expression)
        {
            Token current = _lexer.CurrentToken;
            if (Same(expression, TokenKind.String))
            {
                return new StringExpressionNode(current.Text, current.IsExpandable);
            }
            else if (Same(expression, TokenKind.Numeric))
            {
                return new NumericExpressionNode(current.Text);
            }
            else if (Same(expression, TokenKind.Property))
            {
                return new StringExpressionNode(current.Text, true /* requires expansion */);
            }
            else if (Same(expression, TokenKind.ItemMetadata))
            {
                return new StringExpressionNode(current.Text, true /* requires expansion */);
            }
            else if (Same(expression, TokenKind.ItemList))
            {
                return new StringExpressionNode(current.Text, true /* requires expansion */);
            }
            else
            {
                return null;
            }
        }

        private bool Same(string expression, TokenKind token)
        {
            if (_lexer.IsNext(token))
            {
                if (!_lexer.Advance())
                {
                    errorPosition = _lexer.GetErrorPosition();
                    if (_lexer.UnexpectedlyFound != null)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
                    }
                    else
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, _lexer.GetErrorResource(), expression, errorPosition);
                    }
                }
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
