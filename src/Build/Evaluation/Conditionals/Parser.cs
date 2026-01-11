// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

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
    private readonly string _expression;
    private readonly ElementLocation _elementLocation;
    private readonly Scanner _lexer;

    internal int errorPosition = 0; // useful for unit tests

    #region REMOVE_COMPAT_WARNING

    private bool _warnedForExpression;

    /// <summary>
    ///  Engine Logging Service reference where events will be logged to.
    /// </summary>
    private readonly ILoggingService? _loggingServices;

    /// <summary>
    ///  Location contextual information which are attached to logging events to
    ///  say where they are in relation to the process, engine, project, target,task which is executing.
    /// </summary>
    private readonly BuildEventContext _logBuildEventContext;

    #endregion

    public Parser(string expression, ParserOptions options, ElementLocation elementLocation, LoggingContext? loggingContext = null)
    {
        // We currently have no support (and no scenarios) for disallowing property references
        // in Conditions.
        ErrorUtilities.VerifyThrow((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

        _expression = expression;
        _elementLocation = elementLocation;

        _loggingServices = loggingContext?.LoggingService;
        _logBuildEventContext = loggingContext?.BuildEventContext ?? BuildEventContext.Invalid;

        _lexer = new Scanner(expression, options);
    }

    //
    // Main entry point for parser.
    // You pass in the expression you want to parse, and you get an
    // ExpressionTree out the back end.
    //
    public static GenericExpressionNode Parse(
        string expression,
        ParserOptions options,
        ElementLocation elementLocation,
        LoggingContext? loggingContext = null)
    {
        var parser = new Parser(expression, options, elementLocation, loggingContext);
        return parser.Parse();
    }

    public GenericExpressionNode Parse()
    {
        if (!Advance())
        {
            // We should never get here because Advance always throws on error.
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        GenericExpressionNode node = Expr();

        Expect(Token.TokenType.EndOfInput);

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
        if (_loggingServices != null && !_warnedForExpression &&
            node.PotentialAndOrConflict())
        {
            // We only want to warn once even if there multiple () sub expressions
            _warnedForExpression = true;

            // Log a warning regarding the fact the expression may have been evaluated
            // incorrectly in earlier version of MSBuild
            _loggingServices.LogWarning(_logBuildEventContext, null, new BuildEventFileInfo(_elementLocation), "ConditionMaybeEvaluatedIncorrectly", _expression);
        }
        #endregion

        return node;
    }

    private GenericExpressionNode ExprPrime(GenericExpressionNode lhs)
    {
        if (Same(Token.TokenType.Or))
        {
            OperatorExpressionNode orNode = new OrExpressionNode();
            GenericExpressionNode rhs = BooleanTerm();
            orNode.LeftChild = lhs;
            orNode.RightChild = rhs;

            return ExprPrime(orNode);
        }

        return lhs;
    }

    private GenericExpressionNode BooleanTerm()
    {
        if (!TryParseRelationalExpression(out var node))
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
        if (Same(Token.TokenType.And))
        {
            if (!TryParseRelationalExpression(out var rhs))
            {
                ThrowUnexpectedTokenInCondition();
            }

            OperatorExpressionNode andNode = new AndExpressionNode();
            andNode.LeftChild = lhs;
            andNode.RightChild = rhs;

            return BooleanTermPrime(andNode);
        }

        return lhs;
    }

    private bool TryParseRelationalExpression([NotNullWhen(true)] out GenericExpressionNode? result)
    {
        if (!TryParseFactor(out var lhs))
        {
            result = null;
            return false;
        }

        if (!TryParseRelationalOperation(out var node))
        {
            result = lhs;
            return true;
        }

        if (!TryParseFactor(out var rhs))
        {
            result = null;
            return false;
        }

        node.LeftChild = lhs;
        node.RightChild = rhs;

        result = node;
        return true;
    }

    private bool TryParseRelationalOperation([NotNullWhen(true)] out OperatorExpressionNode? result)
    {
        result = null;

        if (Same(Token.TokenType.LessThan))
        {
            result = new LessThanExpressionNode();
        }
        else if (Same(Token.TokenType.GreaterThan))
        {
            result = new GreaterThanExpressionNode();
        }
        else if (Same(Token.TokenType.LessThanOrEqualTo))
        {
            result = new LessThanOrEqualExpressionNode();
        }
        else if (Same(Token.TokenType.GreaterThanOrEqualTo))
        {
            result = new GreaterThanOrEqualExpressionNode();
        }
        else if (Same(Token.TokenType.EqualTo))
        {
            result = new EqualExpressionNode();
        }
        else if (Same(Token.TokenType.NotEqualTo))
        {
            result = new NotEqualExpressionNode();
        }

        return result != null;
    }

    private bool TryParseFactor([NotNullWhen(true)] out GenericExpressionNode? result)
    {
        // Checks for TokenTypes String, Numeric, Property, ItemMetadata, and ItemList.
        // If it's one of those, return it.
        if (TryParseArgument(out var argument))
        {
            result = argument;
            return true;
        }

        // If it's not one of those, check for other TokenTypes.
        Token current = _lexer.CurrentToken;

        // Function call
        if (Same(Token.TokenType.Function))
        {
            Expect(Token.TokenType.LeftParenthesis);

            var arglist = new List<GenericExpressionNode>();

            if (!TryParseArgumentList(arglist))
            {
                result = null;
                return false;
            }

            result = new FunctionCallExpressionNode(current.String, arglist);
            return true;
        }

        // Parenthesized expression
        if (Same(Token.TokenType.LeftParenthesis))
        {
            GenericExpressionNode child = Expr();
            Expect(Token.TokenType.RightParenthesis);

            result = child;
            return true;
        }

        // Not expression
        if (Same(Token.TokenType.Not))
        {
            if (!TryParseFactor(out var expr))
            {
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

    private bool TryParseArgumentList(List<GenericExpressionNode> argumentList)
    {
        if (Same(Token.TokenType.RightParenthesis))
        {
            return true;
        }

        if (!TryParseArgument(out var argument))
        {
            return false;
        }

        argumentList.Add(argument);

        while (Same(Token.TokenType.Comma))
        {
            if (!TryParseArgument(out argument))
            {
                return false;
            }

            argumentList.Add(argument);
        }

        Expect(Token.TokenType.RightParenthesis);
        return true;
    }

    private bool TryParseArgument([NotNullWhen(true)] out GenericExpressionNode? result)
    {
        Token current = _lexer.CurrentToken;

        result = null;

        if (Same(Token.TokenType.String))
        {
            result = new StringExpressionNode(current.String, current.Expandable);
        }
        else if (Same(Token.TokenType.Numeric))
        {
            result = new NumericExpressionNode(current.String);
        }
        else if (Same(Token.TokenType.Property))
        {
            result = new StringExpressionNode(current.String, expandable: true);
        }
        else if (Same(Token.TokenType.ItemMetadata))
        {
            result = new StringExpressionNode(current.String, expandable: true);
        }
        else if (Same(Token.TokenType.ItemList))
        {
            result = new StringExpressionNode(current.String, expandable: true);
        }

        return result != null;
    }

    private void Expect(Token.TokenType token)
    {
        if (!Same(token))
        {
            ThrowUnexpectedTokenInCondition();
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

    [DoesNotReturn]
    private void ThrowUnexpectedTokenInCondition()
    {
        errorPosition = _lexer.GetErrorPosition();
        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _expression, _lexer.IsNextString(), errorPosition);
    }
}
