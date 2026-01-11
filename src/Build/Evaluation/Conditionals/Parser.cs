// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Conditionals;
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

        Expect(TokenKind.EndOfInput);

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
        if (!_lexer.IsCurrent(TokenKind.EndOfInput))
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
        if (Same(TokenKind.Or))
        {
            GenericExpressionNode rhs = BooleanTerm();
            var orNode = new OrExpressionNode(lhs, rhs);

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

        if (!_lexer.IsCurrent(TokenKind.EndOfInput))
        {
            node = BooleanTermPrime(node);
        }

        return node;
    }

    private GenericExpressionNode BooleanTermPrime(GenericExpressionNode lhs)
    {
        if (Same(TokenKind.And))
        {
            if (!TryParseRelationalExpression(out var rhs))
            {
                ThrowUnexpectedTokenInCondition();
            }

            var andNode = new AndExpressionNode(lhs, rhs);

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

        if (!TryParseRelationalOperator(out var op))
        {
            result = lhs;
            return true;
        }

        if (!TryParseFactor(out var rhs))
        {
            result = null;
            return false;
        }

        result = CreateOperatorNode(op, lhs, rhs);
        return true;
    }

    private bool TryParseRelationalOperator(out TokenKind op)
    {
        op = _lexer.Current.Kind switch
        {
            TokenKind.LessThan => TokenKind.LessThan,
            TokenKind.GreaterThan => TokenKind.GreaterThan,
            TokenKind.LessThanOrEqualTo => TokenKind.LessThanOrEqualTo,
            TokenKind.GreaterThanOrEqualTo => TokenKind.GreaterThanOrEqualTo,
            TokenKind.EqualTo => TokenKind.EqualTo,
            TokenKind.NotEqualTo => TokenKind.NotEqualTo,
            _ => TokenKind.None,
        };

        return op != TokenKind.None && Advance();
    }

    private static OperatorExpressionNode CreateOperatorNode(TokenKind op, GenericExpressionNode lhs, GenericExpressionNode rhs)
        => op switch
        {
            TokenKind.LessThan => new LessThanExpressionNode(lhs, rhs),
            TokenKind.GreaterThan => new GreaterThanExpressionNode(lhs, rhs),
            TokenKind.LessThanOrEqualTo => new LessThanOrEqualExpressionNode(lhs, rhs),
            TokenKind.GreaterThanOrEqualTo => new GreaterThanOrEqualExpressionNode(lhs, rhs),
            TokenKind.EqualTo => new EqualExpressionNode(lhs, rhs),
            TokenKind.NotEqualTo => new NotEqualExpressionNode(lhs, rhs),
            _ => ErrorUtilities.ThrowInternalErrorUnreachable<OperatorExpressionNode>(),
        };

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
        Token current = _lexer.Current;

        // Function call
        if (Same(TokenKind.Function))
        {
            Expect(TokenKind.LeftParenthesis);

            if (!TryParseArgumentList(out var argumentList))
            {
                result = null;
                return false;
            }

            result = new FunctionCallExpressionNode(current.Text, argumentList);
            return true;
        }

        // Parenthesized expression
        if (Same(TokenKind.LeftParenthesis))
        {
            GenericExpressionNode child = Expr();
            Expect(TokenKind.RightParenthesis);

            result = child;
            return true;
        }

        // Not expression
        if (Same(TokenKind.Not))
        {
            // If the next token is a boolean literal, we can simplify
            if (_lexer.Current.IsBooleanTrue)
            {
                Advance();
                result = new BooleanLiteralNode(_lexer.Current.Text, value: false);
                return true;
            }
            else if (_lexer.Current.IsBooleanFalse)
            {
                Advance();
                result = new BooleanLiteralNode(_lexer.Current.Text, value: true);
                return true;
            }

            if (!TryParseFactor(out var expr))
            {
                result = null;
                return false;
            }

            result = new NotExpressionNode(expr);
            return true;
        }

        result = null;
        return false;
    }

    private bool TryParseArgumentList(out ImmutableArray<GenericExpressionNode> result)
    {
        if (Same(TokenKind.RightParenthesis))
        {
            result = [];
            return true;
        }

        if (!TryParseArgument(out var argument))
        {
            result = default;
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<GenericExpressionNode>();

        builder.Add(argument);

        while (Same(TokenKind.Comma))
        {
            if (!TryParseArgument(out argument))
            {
                result = default;
                return false;
            }

            builder.Add(argument);
        }

        Expect(TokenKind.RightParenthesis);

        result = builder.ToImmutable();
        return true;
    }

    private bool TryParseArgument([NotNullWhen(true)] out GenericExpressionNode? result)
    {
        Token current = _lexer.Current;

        result = current.Kind switch
        {
            TokenKind.True or TokenKind.On or TokenKind.Yes => new BooleanLiteralNode(current.Text, value: true),
            TokenKind.False or TokenKind.Off or TokenKind.No => new BooleanLiteralNode(current.Text, value: false),
            TokenKind.String => CreateFromStringToken(current),
            TokenKind.Numeric => new NumericExpressionNode(current.Text),
            TokenKind.Property => new StringExpressionNode(current.Text, expandable: true),
            TokenKind.ItemMetadata => new StringExpressionNode(current.Text, expandable: true),
            TokenKind.ItemList => new StringExpressionNode(current.Text, expandable: true),
            _ => null,
        };

        return result != null && Advance();

        static GenericExpressionNode CreateFromStringToken(Token token)
        {
            if (token.IsBooleanTrue)
            {
                return new BooleanLiteralNode(token.Text, value: true);
            }
            else if (token.IsBooleanFalse)
            {
                return new BooleanLiteralNode(token.Text, value: false);
            }

            return new StringExpressionNode(token.Text, token.IsExpandable);
        }
    }

    private void Expect(TokenKind kind)
    {
        if (!Same(kind))
        {
            ThrowUnexpectedTokenInCondition();
        }
    }

    private bool Same(TokenKind kind)
        => _lexer.IsCurrent(kind) && Advance();

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
        ProjectErrorUtilities.ThrowInvalidProject(_elementLocation, "UnexpectedTokenInCondition", _expression, _lexer.Current.Text, errorPosition);
    }
}
