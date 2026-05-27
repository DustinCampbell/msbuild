// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

        // Older versions of MSBuild evaluated mixed 'and'/'or' conditions left-to-right
        // instead of giving 'and' higher precedence. When this was fixed, a compatibility
        // warning (MSB4130) was added to flag expressions that might now evaluate differently.
        // The warning fires for expressions with both 'and' and 'or' at the same level
        // without explicit parentheses.
        private readonly ILoggingService _loggingServices;
        private readonly BuildEventContext _logBuildEventContext;
        private bool _warnedForExpression;

        internal Parser(LoggingContext loggingContext = null)
        {
            _loggingServices = loggingContext?.LoggingService;
            _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;
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
            Assumed.True((optionSettings & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

            _options = optionSettings;
            _elementLocation = elementLocation;

            _lexer = new Scanner(expression, _options);
            if (!_lexer.Advance())
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, _lexer.GetErrorResource(), expression, errorPosition, _lexer.UnexpectedlyFound);
            }

            if (!TryParseExpr(expression, out GenericExpressionNode node))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
            }

            return node;
        }

        private bool TryParseExpr(string expression, out GenericExpressionNode result)
        {
            if (!TryParseBooleanTerm(expression, out GenericExpressionNode node))
            {
                result = null;
                return false;
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                if (!TryParseExprPrime(expression, node, out node))
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
                _loggingServices.LogWarning(_logBuildEventContext, subcategoryResourceName: null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", expression);
            }

            result = node;
            return true;
        }

        private bool TryParseExprPrime(string expression, GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (Same(expression, Token.TokenType.EndOfInput))
            {
                result = lhs;
                return true;
            }

            if (Same(expression, Token.TokenType.Or))
            {
                if (!TryParseBooleanTerm(expression, out GenericExpressionNode rhs))
                {
                    result = null;
                    return false;
                }

                OperatorExpressionNode orNode = new OrExpressionNode();
                orNode.LeftChild = lhs;
                orNode.RightChild = rhs;
                return TryParseExprPrime(expression, orNode, out result);
            }

            // ExprPrime always shows up at the rightmost side of the grammar rhs,
            // the EndOfInput case takes care of things
            result = lhs;
            return true;
        }

        private bool TryParseBooleanTerm(string expression, out GenericExpressionNode result)
        {
            if (!TryParseRelationalExpr(expression, out GenericExpressionNode node))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                result = null;
                return false;
            }

            if (!_lexer.IsNext(Token.TokenType.EndOfInput) && !TryParseBooleanTermPrime(expression, node, out node))
            {
                result = null;
                return false;
            }

            result = node;
            return true;
        }

        private bool TryParseBooleanTermPrime(string expression, GenericExpressionNode lhs, out GenericExpressionNode result)
        {
            if (_lexer.IsNext(Token.TokenType.EndOfInput))
            {
                result = lhs;
                return true;
            }

            if (Same(expression, Token.TokenType.And))
            {
                if (!TryParseRelationalExpr(expression, out GenericExpressionNode rhs))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                    result = null;
                    return false;
                }

                OperatorExpressionNode andNode = new AndExpressionNode();
                andNode.LeftChild = lhs;
                andNode.RightChild = rhs;
                return TryParseBooleanTermPrime(expression, andNode, out result);
            }

            // Should this be error case?
            result = lhs;
            return true;
        }

        private bool TryParseRelationalExpr(string expression, out GenericExpressionNode result)
        {
            if (!TryParseFactor(expression, out GenericExpressionNode lhs))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                result = null;
                return false;
            }

            if (!TryParseRelationalOperation(expression, out OperatorExpressionNode node))
            {
                result = lhs;
                return true;
            }

            if (!TryParseFactor(expression, out GenericExpressionNode rhs))
            {
                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                result = null;
                return false;
            }

            node.LeftChild = lhs;
            node.RightChild = rhs;
            result = node;
            return true;
        }

        private bool TryParseRelationalOperation(string expression, out OperatorExpressionNode result)
        {
            if (Same(expression, Token.TokenType.LessThan))
            {
                result = new LessThanExpressionNode();
                return true;
            }

            if (Same(expression, Token.TokenType.GreaterThan))
            {
                result = new GreaterThanExpressionNode();
                return true;
            }

            if (Same(expression, Token.TokenType.LessThanOrEqualTo))
            {
                result = new LessThanOrEqualExpressionNode();
                return true;
            }

            if (Same(expression, Token.TokenType.GreaterThanOrEqualTo))
            {
                result = new GreaterThanOrEqualExpressionNode();
                return true;
            }

            if (Same(expression, Token.TokenType.EqualTo))
            {
                result = new EqualExpressionNode();
                return true;
            }

            if (Same(expression, Token.TokenType.NotEqualTo))
            {
                result = new NotEqualExpressionNode();
                return true;
            }

            result = null;
            return false;
        }

        private bool TryParseFactor(string expression, out GenericExpressionNode result)
        {
            // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
            if (TryParseArg(expression, out result))
            {
                return true;
            }

            // If it's not one of those, check for other TokenTypes.
            Token current = _lexer.CurrentToken;
            if (Same(expression, Token.TokenType.Function))
            {
                if (!Same(expression, Token.TokenType.LeftParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _lexer.IsNextString(), errorPosition);
                    result = null;
                    return false;
                }

                var arglist = new List<GenericExpressionNode>();
                TryParseArglist(expression, arglist);
                if (!Same(expression, Token.TokenType.RightParenthesis))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                    result = null;
                    return false;
                }

                result = new FunctionCallExpressionNode(current.String, arglist);
                return true;
            }

            if (Same(expression, Token.TokenType.LeftParenthesis))
            {
                if (!TryParseExpr(expression, out GenericExpressionNode child))
                {
                    result = null;
                    return false;
                }

                if (Same(expression, Token.TokenType.RightParenthesis))
                {
                    result = child;
                    return true;
                }

                errorPosition = _lexer.GetErrorPosition();
                ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                result = null;
                return false;
            }

            if (Same(expression, Token.TokenType.Not))
            {
                if (!TryParseFactor(expression, out GenericExpressionNode expr))
                {
                    errorPosition = _lexer.GetErrorPosition();
                    ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", expression, _lexer.IsNextString(), errorPosition);
                    result = null;
                    return false;
                }

                OperatorExpressionNode notNode = new NotExpressionNode();
                notNode.LeftChild = expr;
                result = notNode;
                return true;
            }

            result = null;
            return false;
        }

        private void TryParseArglist(string expression, List<GenericExpressionNode> arglist)
        {
            if (!_lexer.IsNext(Token.TokenType.RightParenthesis))
            {
                TryParseArgs(expression, arglist);
            }
        }

        private void TryParseArgs(string expression, List<GenericExpressionNode> arglist)
        {
            TryParseArg(expression, out GenericExpressionNode arg);

            arglist.Add(arg);

            if (Same(expression, Token.TokenType.Comma))
            {
                TryParseArgs(expression, arglist);
            }
        }

        private bool TryParseArg(string expression, out GenericExpressionNode result)
        {
            Token current = _lexer.CurrentToken;

            if (Same(expression, Token.TokenType.String))
            {
                result = new StringExpressionNode(current.String, current.Expandable);
                return true;
            }

            if (Same(expression, Token.TokenType.Numeric))
            {
                result = new NumericExpressionNode(current.String);
                return true;
            }

            if (Same(expression, Token.TokenType.Property))
            {
                result = new StringExpressionNode(current.String, expandable: true);
                return true;
            }

            if (Same(expression, Token.TokenType.ItemMetadata))
            {
                result = new StringExpressionNode(current.String, expandable: true);
                return true;
            }

            if (Same(expression, Token.TokenType.ItemList))
            {
                result = new StringExpressionNode(current.String, expandable: true);
                return true;
            }

            result = null;
            return false;
        }

        private bool Same(string expression, Token.TokenType token)
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

            return false;
        }
    }
}
