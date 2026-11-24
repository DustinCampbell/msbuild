// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Parses an MSBuild condition expression into a reusable expression tree.
/// </summary>
/// <remarks>
///  The grammar below describes tokens emitted by <see cref="Scanner"/>, not the full
///  property-function or item-expansion language. Uppercase names denote tokens;
///  <c>EOF</c> denotes <see cref="TokenKind.EndOfInput"/>.
///  <code>
///   condition          = expression, EOF ;
///   expression         = booleanTerm, { OR, booleanTerm } ;
///   booleanTerm        = comparison, { AND, comparison } ;
///   comparison         = factor, [ comparisonOperator, factor ] ;
///   comparisonOperator = "==" | "!=" | "&lt;" | "&gt;" | "&lt;=" | "&gt;=" ;
///
///   factor             = argument
///                      | functionCall
///                      | "(", expression, ")"
///                      | "!", factor ;
///
///   functionCall       = FUNCTION_NAME, "(", [ argumentList ], ")" ;
///   argumentList       = [ argument ], { ",", [ argument ] } ;
///   argument           = STRING | NUMBER | PROPERTY | ITEM_METADATA | ITEM_LIST ;
///  </code>
///  <para>
///   Precedence, from highest to lowest, is negation, comparisons, <c>AND</c>, then <c>OR</c>.
///   Boolean operators associate left; comparisons cannot be chained without grouping.
///   Mixing <c>AND</c> and <c>OR</c> within a grouping records a compatibility warning without
///   changing precedence. Parentheses start a separate grouping for warning tracking.
///  </para>
///  <para>
///   Function arguments are individual argument tokens, not expressions. Missing arguments are
///   accepted during parsing: <c>F(,)</c>, <c>F(a,)</c>, and <c>F(,a)</c> produce
///   <see langword="null"/> argument entries, whereas <c>F()</c> has no arguments.
///   Boolean spellings are string tokens; function names and argument counts are checked during
///   evaluation rather than parsing.
///  </para>
///  <para>
///   Node-producing <c>TryParse</c> methods return <see langword="false"/> with a
///   <see langword="null"/> node and leave input untouched when no production matches.
///   Once a production has started, malformed input throws <see cref="InvalidProjectFileException"/>.
///  </para>
///  <para>
///   Each parser instance is used for one parse. Retain the instance when successful-parse warnings
///   must be reported through <see cref="LogWarnings"/>; cache callers report them only after winning
///   publication. The static <see cref="Parse(string, ParserOptions, IElementLocation, LoggingContext)"/>
///   convenience method does not report successful-parse warnings. Failed parses report recorded
///   warnings through the supplied logging context before throwing.
///  </para>
///  <para>
///   Evaluate the returned <see cref="ParsedCondition"/> through
///   <see cref="ConditionEvaluator.ConditionEvaluationState{P, I}.Evaluate"/> so private evaluation
///   state is initialized and released.
///  </para>
/// </remarks>
internal sealed class Parser
{
    /// <summary>
    ///  The condition text being parsed.
    /// </summary>
    private readonly string _text;

    /// <summary>
    ///  The scanner supplying condition tokens.
    /// </summary>
    private readonly Scanner _lexer;

    /// <summary>
    ///  The project-file location associated with diagnostics.
    /// </summary>
    private readonly IElementLocation _location;

    /// <summary>
    ///  Whether a completed expression mixed unparenthesized <c>AND</c> and <c>OR</c> operators.
    /// </summary>
    private bool _hasMixedAndOr;

    /// <summary>
    ///  The most recent scanner or parser error position.
    /// </summary>
    private int _errorPosition;

    /// <summary>
    ///  The number of evaluation-state slots assigned to expandable string nodes in this condition.
    /// </summary>
    private int _stringNodeStateCount;

    /// <summary>
    ///  Initializes a parser for one condition expression.
    /// </summary>
    /// <param name="text">The condition text to parse.</param>
    /// <param name="options">The permitted reference forms. Must include <see cref="ParserOptions.AllowProperties"/>.</param>
    /// <param name="location">The project-file location to use for diagnostics.</param>
    public Parser(string text, ParserOptions options, IElementLocation location)
    {
        // We currently have no support (and no scenarios) for disallowing property references
        // in Conditions.
        Assumed.True((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

        _text = text;
        _lexer = new Scanner(text, options);
        _location = location;
        _hasMixedAndOr = false;
    }

    /// <summary>
    ///  The most recent scanner or parser error position.
    /// </summary>
    public int ErrorPosition => _errorPosition;

    /// <summary>
    ///  Parses a condition without retaining the parser instance.
    /// </summary>
    /// <param name="text">The condition text to parse.</param>
    /// <param name="options">The permitted reference forms. Must include <see cref="ParserOptions.AllowProperties"/>.</param>
    /// <param name="location">The project-file location to use for diagnostics.</param>
    /// <param name="loggingContext">Reports recorded warnings on parse failure, or <see langword="null"/> to omit them.</param>
    /// <returns>
    ///  The parsed expression tree and its evaluation-state layout.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">The condition contains invalid syntax or a disallowed reference.</exception>
    /// <remarks>
    ///  Successful-parse warnings are not reported. Use an instance and call <see cref="LogWarnings"/>
    ///  after <see cref="Parse(LoggingContext)"/> when those warnings are required.
    /// </remarks>
    public static ParsedCondition Parse(string text, ParserOptions options, IElementLocation location, LoggingContext? loggingContext = null)
        => new Parser(text, options, location).Parse(loggingContext);

    /// <summary>
    ///  Parses <c>condition = expression, EOF</c> and records compatibility warnings.
    /// </summary>
    /// <param name="loggingContext">Reports recorded warnings on parse failure, or <see langword="null"/> to omit them.</param>
    /// <returns>
    ///  The parsed expression tree and its evaluation-state layout.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">The condition contains invalid syntax or a disallowed reference.</exception>
    /// <remarks>
    ///  Call once per parser instance. The <paramref name="loggingContext"/> is used only to report
    ///  recorded warnings before a parse failure. After successful parsing, call
    ///  <see cref="LogWarnings"/> separately to report them.
    /// </remarks>
    public ParsedCondition Parse(LoggingContext? loggingContext = null)
    {
        try
        {
            if (!_lexer.Advance())
            {
                _errorPosition = _lexer.ErrorPosition;
                ProjectErrorUtilities.ThrowInvalidProject(_location, _lexer.ErrorResource, _text, _errorPosition, _lexer.ErrorFormatArg);
            }

            if (!TryParseExpression(out GenericExpressionNode? node))
            {
                ThrowUnexpectedToken();
            }

            if (!_lexer.IsKind(TokenKind.EndOfInput))
            {
                ThrowUnexpectedToken();
            }

            return new ParsedCondition(node, new NodeStateLayout(_stringNodeStateCount));
        }
        catch (InvalidProjectFileException)
        {
            // Failed parses cannot be published, so their diagnostics belong to this caller.
            LogWarnings(_text, loggingContext);
            throw;
        }
    }

    /// <summary>
    ///  Reports compatibility warnings recorded by this parser.
    /// </summary>
    /// <param name="expression">The condition text to include in a warning.</param>
    /// <param name="loggingContext">The warning destination, or <see langword="null"/> to omit warnings.</param>
    /// <remarks>
    ///  For a successful cached parse, call only after winning publication. Failed parses report
    ///  their recorded warnings from <see cref="Parse(LoggingContext)"/> before rethrowing.
    /// </remarks>
    public void LogWarnings(string expression, LoggingContext? loggingContext)
    {
        if (!_hasMixedAndOr || loggingContext is null)
        {
            return;
        }

        loggingContext.LoggingService.LogWarning(
            loggingContext.BuildEventContext,
            DiagnosticSubcategory.None,
            _location,
            "ConditionMaybeEvaluatedIncorrectly",
            expression);
    }

    /// <summary>
    ///  Parses an expression, combining <c>OR</c> operands from left to right.
    /// </summary>
    /// <param name="node">The parsed node, or <see langword="null"/> if no expression starts at the current token.</param>
    /// <returns>
    ///  <see langword="true"/> if an expression was parsed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">An expression starts but contains invalid syntax or a scanning error.</exception>
    /// <remarks>
    ///  <code>expression = booleanTerm, { OR, booleanTerm } ;</code>
    ///  Tracks mixed <c>AND</c> and <c>OR</c> operators only within this expression's grouping.
    /// </remarks>
    private bool TryParseExpression([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        // Track mixes like "A Or B And C" so we can warn users
        // to make the grouping explicit with parentheses.
        bool hasAnd = false;
        bool hasOr = false;
        if (!TryParseBooleanTerm(ref hasAnd, out node))
        {
            return false;
        }

        while (TryConsume(TokenKind.Or))
        {
            if (!TryParseBooleanTerm(ref hasAnd, out GenericExpressionNode? rhs))
            {
                ThrowUnexpectedToken();
            }

            hasOr = true;
            node = new OrExpressionNode(node, rhs);
        }

        _hasMixedAndOr |= hasAnd && hasOr;
        return true;
    }

    /// <summary>
    ///  Parses a boolean term, combining <c>AND</c> operands from left to right.
    /// </summary>
    /// <param name="hasAnd">Set to <see langword="true"/> if this term consumes an <c>AND</c> operator.</param>
    /// <param name="node">The parsed node, or <see langword="null"/> if no term starts at the current token.</param>
    /// <returns>
    ///  <see langword="true"/> if a term was parsed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">A term starts but contains invalid syntax or a scanning error.</exception>
    /// <remarks>
    ///  <code>booleanTerm = comparison, { AND, comparison } ;</code>
    /// </remarks>
    private bool TryParseBooleanTerm(ref bool hasAnd, [NotNullWhen(true)] out GenericExpressionNode? node)
    {
        if (!TryParseComparison(out node))
        {
            return false;
        }

        while (TryConsume(TokenKind.And))
        {
            if (!TryParseComparison(out GenericExpressionNode? rhs))
            {
                ThrowUnexpectedToken();
            }

            hasAnd = true;
            node = new AndExpressionNode(node, rhs);
        }

        return true;
    }

    /// <summary>
    ///  Parses a factor with an optional comparison.
    /// </summary>
    /// <param name="node">The parsed node, or <see langword="null"/> if no factor starts at the current token.</param>
    /// <returns>
    ///  <see langword="true"/> if a factor or comparison was parsed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">A comparison starts but contains invalid syntax or a scanning error.</exception>
    /// <remarks>
    ///  <code>comparison = factor, [ comparisonOperator, factor ] ;</code>
    ///  All comparison operators have the same precedence. Chained comparisons require grouping.
    /// </remarks>
    private bool TryParseComparison([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        if (!TryParseFactor(out node))
        {
            return false;
        }

        if (!TryParseComparisonOperator(out TokenKind kind))
        {
            return true;
        }

        if (!TryParseFactor(out GenericExpressionNode? rhs))
        {
            ThrowUnexpectedToken();
        }

        node = kind switch
        {
            TokenKind.LessThan => new LessThanExpressionNode(node, rhs),
            TokenKind.GreaterThan => new GreaterThanExpressionNode(node, rhs),
            TokenKind.LessThanOrEqualTo => new LessThanOrEqualExpressionNode(node, rhs),
            TokenKind.GreaterThanOrEqualTo => new GreaterThanOrEqualExpressionNode(node, rhs),
            TokenKind.EqualTo => new EqualExpressionNode(node, rhs),
            TokenKind.NotEqualTo => new NotEqualExpressionNode(node, rhs),

            _ => Assumed.Unreachable<GenericExpressionNode>(),
        };

        return true;
    }

    /// <summary>
    ///  Parses a comparison operator and returns its token kind without creating an expression node.
    /// </summary>
    /// <param name="kind">The consumed operator kind, or <see cref="TokenKind.None"/> if no operator matched.</param>
    /// <returns>
    ///  <see langword="true"/> if an operator was consumed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">Scanning fails while advancing past the operator.</exception>
    /// <remarks>
    ///  <code>comparisonOperator = "==" | "!=" | "&lt;" | "&gt;" | "&lt;=" | "&gt;=" ;</code>
    /// </remarks>
    private bool TryParseComparisonOperator(out TokenKind kind)
    {
        kind =
            TryConsume(TokenKind.LessThan)
                ? TokenKind.LessThan
          : TryConsume(TokenKind.GreaterThan)
                ? TokenKind.GreaterThan
          : TryConsume(TokenKind.LessThanOrEqualTo)
                ? TokenKind.LessThanOrEqualTo
          : TryConsume(TokenKind.GreaterThanOrEqualTo)
                ? TokenKind.GreaterThanOrEqualTo
          : TryConsume(TokenKind.EqualTo)
                ? TokenKind.EqualTo
          : TryConsume(TokenKind.NotEqualTo)
                ? TokenKind.NotEqualTo
                : TokenKind.None;

        return kind != TokenKind.None;
    }

    /// <summary>
    ///  Parses an argument, function call, grouped expression, or negated factor.
    /// </summary>
    /// <param name="node">The parsed node, or <see langword="null"/> if no factor starts at the current token.</param>
    /// <returns>
    ///  <see langword="true"/> if a factor was parsed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">A factor starts but contains invalid syntax or a scanning error.</exception>
    /// <remarks>
    ///  <code>factor = argument | functionCall | "(", expression, ")" | "!", factor ;</code>
    /// </remarks>
    private bool TryParseFactor([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        if (TryParseArgument(out node))
        {
            return true;
        }

        if (TryParseFunctionCall(out node))
        {
            return true;
        }

        if (TryConsume(TokenKind.LeftParenthesis))
        {
            if (!TryParseExpression(out node))
            {
                ThrowUnexpectedToken();
            }

            if (!TryConsume(TokenKind.RightParenthesis))
            {
                ThrowUnexpectedToken();
            }

            return true;
        }

        if (TryConsume(TokenKind.Not))
        {
            if (!TryParseFactor(out GenericExpressionNode? operand))
            {
                ThrowUnexpectedToken();
            }

            node = new NotExpressionNode(operand);
            return true;
        }

        node = null;
        return false;
    }

    /// <summary>
    ///  Parses a condition-function call and its argument tokens.
    /// </summary>
    /// <param name="node">The function-call node, or <see langword="null"/> if the current token is not a function name.</param>
    /// <returns>
    ///  <see langword="true"/> if a call was parsed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">A function call starts but contains invalid syntax or a scanning error.</exception>
    /// <remarks>
    ///  <code>
    ///   functionCall = FUNCTION_NAME, "(", [ argumentList ], ")" ;
    ///   argumentList = [ argument ], { ",", [ argument ] } ;
    ///  </code>
    ///  Arguments are not expressions. Missing arguments are retained as <see langword="null"/>
    ///  entries, except that an immediate <c>)</c> produces an empty list.
    ///  Function names and argument counts are not validated here.
    /// </remarks>
    private bool TryParseFunctionCall([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        Token current = _lexer.CurrentToken!;
        if (!TryConsume(TokenKind.FunctionName))
        {
            node = null;
            return false;
        }

        if (!TryConsume(TokenKind.LeftParenthesis))
        {
            ThrowUnexpectedToken();
        }

        List<GenericExpressionNode?> arguments = [];
        if (!_lexer.IsKind(TokenKind.RightParenthesis))
        {
            do
            {
                _ = TryParseArgument(out GenericExpressionNode? argument);
                arguments.Add(argument);
            }
            while (TryConsume(TokenKind.Comma));
        }

        if (!TryConsume(TokenKind.RightParenthesis))
        {
            ThrowUnexpectedToken();
        }

        node = new FunctionCallExpressionNode(current.Text, arguments);
        return true;
    }

    /// <summary>
    ///  Parses one string, number, property, metadata, or item-list argument token.
    /// </summary>
    /// <param name="node">The argument node, or <see langword="null"/> if the current token is not an argument.</param>
    /// <returns>
    ///  <see langword="true"/> if an argument was consumed; otherwise, <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">Scanning fails while advancing past the argument.</exception>
    /// <remarks>
    ///  <code>argument = STRING | NUMBER | PROPERTY | ITEM_METADATA | ITEM_LIST ;</code>
    /// </remarks>
    private bool TryParseArgument([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        Token current = _lexer.CurrentToken!;

        node = TryConsume(TokenKind.String)
                ? CreateStringNode(current.Text, current.Expandable)
             : TryConsume(TokenKind.Number)
                ? new NumericExpressionNode(current.Text)
             : TryConsume(TokenKind.Property)
                ? CreateStringNode(current.Text, expandable: true)
             : TryConsume(TokenKind.ItemMetadata)
                ? CreateStringNode(current.Text, expandable: true)
             : TryConsume(TokenKind.ItemList)
                ? CreateStringNode(current.Text, expandable: true)
                : null;

        return node is not null;
    }

    /// <summary>
    ///  Creates a string node, assigning an evaluation-state slot only when expansion is required.
    /// </summary>
    /// <param name="value">The unexpanded token text.</param>
    /// <param name="expandable">Whether the text may require expansion during evaluation.</param>
    /// <returns>
    ///  A string node whose state-slot index is <c>-1</c> for a non-expandable literal.
    /// </returns>
    private StringExpressionNode CreateStringNode(string value, bool expandable)
        => new(value, expandable, expandable ? _stringNodeStateCount++ : -1);

    /// <summary>
    ///  Consumes the current token if it has the specified kind.
    /// </summary>
    /// <param name="kind">The expected token kind.</param>
    /// <returns>
    ///  <see langword="true"/> if the token matched and scanning advanced; otherwise,
    ///  <see langword="false"/> without consuming input.
    /// </returns>
    /// <exception cref="InvalidProjectFileException">Scanning fails while advancing past a matching token.</exception>
    private bool TryConsume(TokenKind kind)
    {
        if (_lexer.IsKind(kind))
        {
            if (!_lexer.Advance())
            {
                _errorPosition = _lexer.ErrorPosition;
                if (_lexer.ErrorFormatArg != null)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(_location, _lexer.ErrorResource, _text, _errorPosition, _lexer.ErrorFormatArg);
                }
                else
                {
                    ProjectErrorUtilities.ThrowInvalidProject(_location, _lexer.ErrorResource, _text, _errorPosition);
                }
            }

            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    ///  Throws a project-file error identifying the current token and its position.
    /// </summary>
    /// <exception cref="InvalidProjectFileException">Always thrown.</exception>
    [DoesNotReturn]
    private void ThrowUnexpectedToken()
    {
        _errorPosition = _lexer.TokenPosition;
        ProjectErrorUtilities.ThrowInvalidProject(_location, "UnexpectedTokenInCondition", _text, _lexer.CurrentTokenText, _errorPosition);
    }
}
