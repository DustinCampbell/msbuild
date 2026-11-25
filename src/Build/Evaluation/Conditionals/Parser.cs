// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;

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
///   <see langword="null"/> node when no production matches or a diagnostic error is recorded.
///   A non-match leaves input untouched. Parsing stops at the first error without returning a partial tree.
///  </para>
///  <para>
///   Each parser instance is used for one parse. Parsing returns errors and warnings as
///   <see cref="ParseDiagnostic"/> data without formatting messages, reporting warnings, or throwing
///   project-file exceptions. Callers own reporting and cache-publication policy and can explicitly
///   invoke <see cref="ReportDiagnostics"/>.
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
    ///  Diagnostics collected in discovery order, allocated only when a diagnostic is recorded.
    /// </summary>
    private List<ParseDiagnostic>? _diagnostics;

    /// <summary>
    ///  Whether a completed expression mixed unparenthesized <c>AND</c> and <c>OR</c> operators.
    /// </summary>
    private bool _hasMixedAndOr;

    /// <summary>
    ///  Whether the first fatal parsing or scanning error has been recorded.
    /// </summary>
    private bool _hasError;

    /// <summary>
    ///  The number of evaluation-state slots assigned to expandable string nodes in this condition.
    /// </summary>
    private int _stringNodeStateCount;

    /// <summary>
    ///  Initializes a parser for one condition expression.
    /// </summary>
    /// <param name="text">The condition text to parse.</param>
    /// <param name="options">The permitted reference forms. Must include <see cref="ParserOptions.AllowProperties"/>.</param>
    private Parser(string text, ParserOptions options)
    {
        // We currently have no support (and no scenarios) for disallowing property references
        // in Conditions.
        Assumed.True((options & ParserOptions.AllowProperties) != 0, "Properties should always be allowed.");

        _text = text;
        _lexer = new Scanner(text, options);
        _hasMixedAndOr = false;
    }

    /// <summary>
    ///  Parses a condition and collects diagnostics without reporting them.
    /// </summary>
    /// <param name="text">The condition text to parse.</param>
    /// <param name="options">The permitted reference forms. Must include <see cref="ParserOptions.AllowProperties"/>.</param>
    /// <returns>
    ///  The completed condition and any warnings, or diagnostics describing why parsing failed.
    /// </returns>
    public static ParseResult Parse(string text, ParserOptions options)
        => new Parser(text, options).Parse();

    /// <summary>
    ///  Parses <c>condition = expression, EOF</c> and collects diagnostics without reporting them.
    /// </summary>
    /// <returns>
    ///  The completed condition and any warnings, or diagnostics describing why parsing failed.
    /// </returns>
    /// <remarks>
    ///  Call once per parser instance. Warnings discovered before the first error are retained.
    ///  Successful parses with no diagnostics do not allocate a diagnostic collection.
    /// </remarks>
    private ParseResult Parse()
    {
        if (!TryAdvance())
        {
            return CreateResult(null);
        }

        if (!TryParseExpression(out GenericExpressionNode? node))
        {
            AddUnexpectedTokenDiagnostic();
            return CreateResult(null);
        }

        if (!_lexer.IsKind(TokenKind.EndOfInput))
        {
            AddUnexpectedTokenDiagnostic();
            return CreateResult(null);
        }

        return CreateResult(new ParsedCondition(node, new NodeStateLayout(_stringNodeStateCount)));

        ParseResult CreateResult(ParsedCondition? condition)
            => new(condition, _diagnostics?.ToImmutableArray() ?? []);
    }

    /// <summary>
    ///  Reports collected warnings and throws the existing project-file exception for the first error.
    /// </summary>
    /// <param name="diagnostics">The diagnostics collected in discovery order.</param>
    /// <param name="location">The project-file location associated with the condition.</param>
    /// <param name="loggingContext">The warning destination, or <see langword="null"/> to omit warnings.</param>
    /// <exception cref="InvalidProjectFileException">An error diagnostic was encountered.</exception>
    /// <remarks>
    ///  Successful cached parses call this only after winning publication. Failed parses are reported
    ///  before publication is attempted. Diagnostic positions are already included in the formatting arguments.
    /// </remarks>
    public static void ReportDiagnostics(ImmutableArray<ParseDiagnostic> diagnostics, ElementLocation location, LoggingContext? loggingContext)
    {
        foreach (ParseDiagnostic diagnostic in diagnostics)
        {
            switch (diagnostic.Severity)
            {
                case ParseDiagnosticSeverity.Error:
                    ProjectErrorUtilities.ThrowInvalidProject(location, diagnostic.ResourceName, diagnostic.Arguments);
                    break;

                case ParseDiagnosticSeverity.Warning:
                    loggingContext?.LoggingService.LogWarning(
                        loggingContext.BuildEventContext,
                        DiagnosticSubcategory.None,
                        location,
                        diagnostic.ResourceName,
                        diagnostic.Arguments);
                    break;

                default:
                    Assumed.Unreachable($"Unexpected diagnostic severity: {diagnostic.Severity}");
                    break;
            }
        }
    }

    /// <summary>
    ///  Parses an expression, combining <c>OR</c> operands from left to right.
    /// </summary>
    /// <param name="node">The parsed node, or <see langword="null"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if an expression was parsed; otherwise, <see langword="false"/>.
    /// </returns>
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
                AddUnexpectedTokenDiagnostic();
                node = null;
                return false;
            }

            hasOr = true;
            node = new OrExpressionNode(node, rhs);
        }

        if (_hasError)
        {
            node = null;
            return false;
        }

        if (hasAnd && hasOr && !_hasMixedAndOr)
        {
            _hasMixedAndOr = true;
            AddDiagnostic(new ParseDiagnostic(
                ParseDiagnosticSeverity.Warning, "ConditionMaybeEvaluatedIncorrectly", 0, [_text]));
        }

        return true;
    }

    /// <summary>
    ///  Parses a boolean term, combining <c>AND</c> operands from left to right.
    /// </summary>
    /// <param name="hasAnd">Set to <see langword="true"/> if this term consumes an <c>AND</c> operator.</param>
    /// <param name="node">The parsed node, or <see langword="null"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if a term was parsed; otherwise, <see langword="false"/>.
    /// </returns>
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
                AddUnexpectedTokenDiagnostic();
                node = null;
                return false;
            }

            hasAnd = true;
            node = new AndExpressionNode(node, rhs);
        }

        if (_hasError)
        {
            node = null;
            return false;
        }

        return true;
    }

    /// <summary>
    ///  Parses a factor with an optional comparison.
    /// </summary>
    /// <param name="node">The parsed node, or <see langword="null"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if a factor or comparison was parsed; otherwise, <see langword="false"/>.
    /// </returns>
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
            if (_hasError)
            {
                node = null;
                return false;
            }

            return true;
        }

        if (!TryParseFactor(out GenericExpressionNode? rhs))
        {
            AddUnexpectedTokenDiagnostic();
            node = null;
            return false;
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
    /// <param name="kind">The consumed operator kind, or <see cref="TokenKind.None"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if an operator was parsed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  <code>comparisonOperator = "==" | "!=" | "&lt;" | "&gt;" | "&lt;=" | "&gt;=" ;</code>
    /// </remarks>
    private bool TryParseComparisonOperator(out TokenKind kind)
    {
        kind = _lexer.CurrentTokenKind;

        if (kind is TokenKind.LessThan or
                    TokenKind.GreaterThan or
                    TokenKind.LessThanOrEqualTo or
                    TokenKind.GreaterThanOrEqualTo or
                    TokenKind.EqualTo or
                    TokenKind.NotEqualTo &&
                    TryAdvance())
        {
            return true;
        }

        kind = TokenKind.None;
        return false;
    }

    /// <summary>
    ///  Parses an argument, function call, grouped expression, or negated factor.
    /// </summary>
    /// <param name="node">The parsed node, or <see langword="null"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if a factor was parsed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  <code>factor = argument | functionCall | "(", expression, ")" | "!", factor ;</code>
    /// </remarks>
    private bool TryParseFactor([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        node = null;
        if (_hasError)
        {
            return false;
        }

        switch (_lexer.CurrentToken?.Kind)
        {
            case TokenKind.String:
            case TokenKind.Number:
            case TokenKind.Property:
            case TokenKind.ItemMetadata:
            case TokenKind.ItemList:
                return TryParseArgument(out node);

            case TokenKind.FunctionName:
                return TryParseFunctionCall(out node);

            case TokenKind.LeftParenthesis:
                if (!TryConsume(TokenKind.LeftParenthesis))
                {
                    return false;
                }

                if (!TryParseExpression(out node))
                {
                    AddUnexpectedTokenDiagnostic();
                    node = null;
                    return false;
                }

                if (!TryExpect(TokenKind.RightParenthesis))
                {
                    node = null;
                    return false;
                }

                return true;

            case TokenKind.Not:
                if (!TryConsume(TokenKind.Not))
                {
                    return false;
                }

                if (!TryParseFactor(out GenericExpressionNode? operand))
                {
                    AddUnexpectedTokenDiagnostic();
                    return false;
                }

                node = new NotExpressionNode(operand);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    ///  Parses a condition-function call and its argument tokens.
    /// </summary>
    /// <param name="node">The function-call node, or <see langword="null"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if a call was parsed; otherwise, <see langword="false"/>.
    /// </returns>
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
        node = null;
        Token current = _lexer.CurrentToken!;
        if (!TryConsume(TokenKind.FunctionName))
        {
            return false;
        }

        if (!TryExpect(TokenKind.LeftParenthesis))
        {
            return false;
        }

        List<GenericExpressionNode?> arguments = [];
        if (!_lexer.IsKind(TokenKind.RightParenthesis))
        {
            do
            {
                if (!TryParseArgument(out GenericExpressionNode? argument) && _hasError)
                {
                    return false;
                }

                arguments.Add(argument);
            }
            while (TryConsume(TokenKind.Comma));
        }

        if (!TryExpect(TokenKind.RightParenthesis))
        {
            return false;
        }

        node = new FunctionCallExpressionNode(current.Text, arguments);
        return true;
    }

    /// <summary>
    ///  Parses one string, number, property, metadata, or item-list argument token.
    /// </summary>
    /// <param name="node">The argument node, or <see langword="null"/> on a non-match or error.</param>
    /// <returns>
    ///  <see langword="true"/> if an argument was parsed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  <code>argument = STRING | NUMBER | PROPERTY | ITEM_METADATA | ITEM_LIST ;</code>
    /// </remarks>
    private bool TryParseArgument([NotNullWhen(true)] out GenericExpressionNode? node)
    {
        node = null;

        if (_hasError || _lexer.CurrentToken is not Token current)
        {
            return false;
        }

        switch (current.Kind)
        {
            case TokenKind.String:
                if (!TryAdvance())
                {
                    return false;
                }

                node = CreateStringNode(current.Text, current.Expandable);
                return true;

            case TokenKind.Number:
                if (!TryAdvance())
                {
                    return false;
                }

                node = new NumericExpressionNode(current.Text);
                return true;

            case TokenKind.Property:
            case TokenKind.ItemMetadata:
            case TokenKind.ItemList:
                if (!TryAdvance())
                {
                    return false;
                }

                node = CreateStringNode(current.Text, expandable: true);
                return true;

            default:
                return false;
        }

        // Only expandable strings require per-evaluation state slots; literals use -1.
        StringExpressionNode CreateStringNode(string value, bool expandable)
            => new(value, expandable, expandable ? _stringNodeStateCount++ : -1);
    }

    /// <summary>
    ///  Consumes the current token if it has the specified kind.
    /// </summary>
    /// <param name="kind">The expected token kind.</param>
    /// <returns>
    ///  <see langword="true"/> if the token matched and scanning advanced; otherwise,
    ///  <see langword="false"/>. A non-match does not consume input; a scanning failure records an error.
    /// </returns>
    private bool TryConsume(TokenKind kind)
        => !_hasError && _lexer.IsKind(kind) && TryAdvance();

    /// <summary>
    ///  Consumes a required token, recording an unexpected-token error if it does not match.
    /// </summary>
    /// <param name="kind">The required token kind.</param>
    /// <returns>
    ///  <see langword="true"/> if the token was consumed; otherwise, <see langword="false"/>.
    /// </returns>
    private bool TryExpect(TokenKind kind)
    {
        if (TryConsume(kind))
        {
            return true;
        }

        AddUnexpectedTokenDiagnostic();
        return false;
    }

    /// <summary>
    ///  Advances the scanner, recording its diagnostic if scanning fails.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if scanning succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    private bool TryAdvance()
    {
        if (_hasError)
        {
            return false;
        }

        if (_lexer.Advance())
        {
            return true;
        }

        int position = _lexer.ErrorPosition;
        string? formatArg = _lexer.ErrorFormatArg;
        object?[] arguments = formatArg is null ? [_text, position] : [_text, position, formatArg];
        AddDiagnostic(new ParseDiagnostic(ParseDiagnosticSeverity.Error, _lexer.ErrorResource, position, arguments));
        return false;
    }

    /// <summary>
    ///  Records the current token and its position unless an earlier error already e`xplains the failure.
    /// </summary>
    private void AddUnexpectedTokenDiagnostic()
    {
        if (_hasError)
        {
            return;
        }

        int position = _lexer.TokenPosition;
        AddDiagnostic(new ParseDiagnostic(
            ParseDiagnosticSeverity.Error, "UnexpectedTokenInCondition", position, [_text, _lexer.CurrentTokenText, position]));
    }

    /// <summary>
    ///  Appends a diagnostic and remembers when parsing must stop.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to collect.</param>
    private void AddDiagnostic(ParseDiagnostic diagnostic)
    {
        Assumed.False(_hasError, "Parsing must stop after the first error diagnostic.");
        _diagnostics ??= [];
        _diagnostics.Add(diagnostic);
        _hasError = diagnostic.Severity == ParseDiagnosticSeverity.Error;
    }
}
