// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Conditionals;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

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
internal ref struct Parser
{
    private readonly string _expression;
    private readonly ElementLocation _elementLocation;
    private readonly ParserOptions _options;
    private Scanner _lexer;

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
        _options = options;

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

    private bool TryParsePropertyExpression(Token dollarSign, [NotNullWhen(true)] out GenericExpressionNode? result)
    {
        int startPosition = dollarSign.Position;

        // Check if $ is followed by (
        if (!_lexer.IsCurrent(TokenKind.LeftParenthesis))
        {
            errorPosition = startPosition + 1;
            ProjectErrorUtilities.ThrowInvalidProject(
                _elementLocation,
                "IllFormedPropertyOpenParenthesisInCondition",
                _expression,
                errorPosition,
                _lexer.Current.Text.ToString());
        }

        // Scan to find the matching closing parenthesis, handling nesting
        // Pass the dollar sign position for error reporting
        if (!ScanPropertyExpressionEnd(startPosition, out int endPosition))
        {
            result = null;
            return false;
        }

        // Build the full text: $(...)
        var fullText = _expression.AsMemory(startPosition, endPosition - startPosition + 1);

        result = new StringExpressionNode(fullText, expandable: true);
        return true;
    }

    private bool ScanPropertyExpressionEnd(int dollarSignPosition, out int endPosition)
    {
        int nestLevel = 0;
        int tokenCount = 0;

        Token openParen = default;
        Token identifier = default;

        while (true)
        {
            Token current = _lexer.Current;

            if (current.Kind == TokenKind.EndOfInput)
            {
                errorPosition = dollarSignPosition + 1;
                ProjectErrorUtilities.ThrowInvalidProject(
                    _elementLocation,
                    "IllFormedPropertyCloseParenthesisInCondition",
                    _expression,
                    errorPosition);
            }

            // Check for nested property expressions
            if (current.Kind == TokenKind.DollarSign)
            {
                Token nestedDollar = current;
                Advance();

                if (!_lexer.IsCurrent(TokenKind.LeftParenthesis))
                {
                    errorPosition = nestedDollar.Position + 1;
                    ProjectErrorUtilities.ThrowInvalidProject(
                        _elementLocation,
                        "IllFormedPropertyOpenParenthesisInCondition",
                        _expression,
                        errorPosition,
                        _lexer.Current.Text.ToString());
                }

                if (!ScanPropertyExpressionEnd(nestedDollar.Position, out _))
                {
                    endPosition = -1;
                    return false;
                }

                continue;
            }

            if (current.Kind == TokenKind.LeftParenthesis)
            {
                nestLevel++;

                if (nestLevel == 1 && tokenCount == 0)
                {
                    // First '(' after '$'
                    openParen = current;
                }
            }
            else if (current.Kind == TokenKind.RightParenthesis)
            {
                nestLevel--;

                if (nestLevel == 0)
                {
                    // Check if this is the simple pattern: $( identifier )
                    if (tokenCount == 2 &&
                        openParen.IsKind(TokenKind.LeftParenthesis) &&
                        identifier.IsKind(TokenKind.Identifier))
                    {
                        // Gap after '('?
                        if (identifier.Position > openParen.Position + 1)
                        {
                            errorPosition = dollarSignPosition + 1;
                            ProjectErrorUtilities.ThrowInvalidProject(
                                _elementLocation,
                                "IllFormedPropertySpaceInCondition",
                                _expression,
                                errorPosition);
                        }

                        // Gap before ')'?
                        if (current.Position > identifier.Position + identifier.Text.Length)
                        {
                            errorPosition = dollarSignPosition + 1;
                            ProjectErrorUtilities.ThrowInvalidProject(
                                _elementLocation,
                                "IllFormedPropertySpaceInCondition",
                                _expression,
                                errorPosition);
                        }
                    }

                    endPosition = current.Position;
                    Advance();
                    return true;
                }
            }
            else if (current.Kind == TokenKind.Identifier &&
                     tokenCount == 1 &&
                     openParen.IsKind(TokenKind.LeftParenthesis))
            {
                // First (and potentially only) token after the '('
                identifier = current;
            }

            tokenCount++;

            if (!Advance())
            {
                endPosition = -1;
                return false;
            }
        }
    }

    private bool TryParseItemMetadataExpression(Token percentSign, [NotNullWhen(true)] out GenericExpressionNode? result)
    {
        int startPosition = percentSign.Position;

        Expect(TokenKind.LeftParenthesis);

        if (!TryConsume(TokenKind.Identifier, out Token metadataName))
        {
            result = null;
            return false;
        }

        // Check for qualified syntax: %(ItemType.MetadataName)
        if (Same(TokenKind.Dot))
        {
            if (!TryConsume(TokenKind.Identifier, out metadataName))
            {
                result = null;
                return false;
            }
        }

        Expect(TokenKind.RightParenthesis, out Token rightParen);

        // Validate metadata based on ParserOptions
        if (!ValidateMetadata(metadataName.Text, startPosition))
        {
            result = null;
            return false;
        }

        // Build the full text: %(...)
        int endPosition = rightParen.Position + rightParen.Text.Length;
        var fullText = _expression.AsMemory(startPosition, endPosition - startPosition);

        result = new StringExpressionNode(fullText, expandable: true);
        return true;
    }

    private bool ValidateMetadata(ReadOnlyMemory<char> metadataName, int startPosition)
    {
        // If all metadata is allowed, no validation needed
        if ((_options & ParserOptions.AllowItemMetadata) == ParserOptions.AllowItemMetadata)
        {
            return true;
        }

        string metadataNameString = metadataName.ToString();

        bool isBuiltIn = FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataNameString);

        if (isBuiltIn && (_options & ParserOptions.AllowBuiltInMetadata) == 0)
        {
            errorPosition = startPosition + 1; // Position for error reporting (1-based)
            ProjectErrorUtilities.ThrowInvalidProject(
                _elementLocation,
                "BuiltInMetadataNotAllowedInThisConditional",
                _expression,
                errorPosition,
                metadataNameString);
        }

        if (!isBuiltIn && (_options & ParserOptions.AllowCustomMetadata) == 0)
        {
            errorPosition = startPosition + 1; // Position for error reporting (1-based)
            ProjectErrorUtilities.ThrowInvalidProject(
                _elementLocation,
                "CustomMetadataNotAllowedInThisConditional",
                _expression,
                errorPosition,
                metadataNameString);
        }

        return true;
    }

    private bool TryParseItemListExpression(Token atSign, [NotNullWhen(true)] out GenericExpressionNode? result)
    {
        int startPosition = atSign.Position;

        // Validate that item lists are allowed
        if ((_options & ParserOptions.AllowItemLists) == 0)
        {
            errorPosition = startPosition + 1;
            ProjectErrorUtilities.ThrowInvalidProject(
                _elementLocation,
                "ItemListNotAllowedInThisConditional",
                _expression,
                errorPosition);
        }

        // Check if @ is followed by (
        if (!_lexer.IsCurrent(TokenKind.LeftParenthesis))
        {
            errorPosition = startPosition + 1;
            ProjectErrorUtilities.ThrowInvalidProject(
                _elementLocation,
                "IllFormedItemListOpenParenthesisInCondition",
                _expression,
                errorPosition,
                _lexer.Current.Text.ToString());
        }

        // Scan to find the matching closing parenthesis
        // This is complex because of transforms like @(Foo->'%(bar)', ',')
        if (!ScanItemListExpressionEnd(startPosition, out int endPosition))
        {
            result = null;
            return false;
        }

        // Build the full text: @(...)
        var fullText = _expression.AsMemory(startPosition, endPosition - startPosition + 1);

        result = new StringExpressionNode(fullText, expandable: true);
        return true;
    }

    private bool ScanItemListExpressionEnd(int atSignPosition, out int endPosition)
    {
        // TODO: Remove this try/catch once Scanner is fully token-based and doesn't throw errors
        try
        {
            // Skip the opening '('
            Advance();

            int nestLevel = 1;

            while (true)
            {
                Token current = _lexer.Current;

                if (current.Kind == TokenKind.EndOfInput)
                {
                    errorPosition = atSignPosition + 1;
                    ProjectErrorUtilities.ThrowInvalidProject(
                        _elementLocation,
                        "IllFormedItemListCloseParenthesisInCondition",
                        _expression,
                        errorPosition);
                }

                if (current.Kind == TokenKind.LeftParenthesis)
                {
                    nestLevel++;
                }
                else if (current.Kind == TokenKind.RightParenthesis)
                {
                    nestLevel--;

                    if (nestLevel == 0)
                    {
                        endPosition = current.Position;
                        Advance(); // Consume the closing parenthesis
                        return true;
                    }
                }

                if (!Advance())
                {
                    endPosition = -1;
                    return false;
                }
            }
        }
        catch (InvalidProjectFileException ex)
        {
            // Scanner throws "IllFormedQuotedStringInCondition" for unclosed quotes,
            // but in the context of item lists we want the more specific error
            if (ex.ErrorCode == "MSB4101") // IllFormedQuotedStringInCondition
            {
                errorPosition = atSignPosition + 1;
                ProjectErrorUtilities.ThrowInvalidProject(
                    _elementLocation,
                    "IllFormedItemListQuoteInCondition",
                    _expression,
                    errorPosition);
            }

            // Otherwise, rethrow as-is
            throw;
        }
    }

    private bool TryFunctionCall(Token identifier, [NotNullWhen(true)] out GenericExpressionNode? result)
    {
        // Function call
        if (!Same(TokenKind.LeftParenthesis))
        {
            result = null;
            return false; ;
        }

        if (!TryParseArgumentList(out var argumentList))
        {
            result = null;
            return false;
        }

        result = new FunctionCallExpressionNode(identifier.Text, argumentList);
        return true;
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
        // Property expression: $(PropertyName) or $([Type]::Method())
        if (TryConsume(TokenKind.DollarSign, out Token dollarSign))
        {
            return TryParsePropertyExpression(dollarSign, out result);
        }

        // Item metadata expression: %(MetadataName) or %(ItemType.MetadataName)
        if (TryConsume(TokenKind.PercentSign, out Token percentSign))
        {
            return TryParseItemMetadataExpression(percentSign, out result);
        }

        // Item list expression: @(ItemType) or @(ItemType->'transform', 'separator')
        if (TryConsume(TokenKind.AtSign, out Token atSign))
        {
            return TryParseItemListExpression(atSign, out result);
        }

        // Function calls and identifiers
        if (TryConsume(TokenKind.Identifier, out Token identifier))
        {
            // Function call
            if (TryFunctionCall(identifier, out result))
            {
                return true;
            }

            result = new StringExpressionNode(identifier.Text, expandable: false);
            return true;
        }

        // Literals from old scanner tokens (gradually being phased out)
        Token current = _lexer.Current;

        result = current.Kind switch
        {
            TokenKind.True or TokenKind.On or TokenKind.Yes => new BooleanLiteralNode(current.Text, value: true),
            TokenKind.False or TokenKind.Off or TokenKind.No => new BooleanLiteralNode(current.Text, value: false),
            TokenKind.String => CreateFromStringToken(current),
            TokenKind.Numeric => new NumericExpressionNode(current.Text),
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

    private void Expect(TokenKind kind, out Token result)
    {
        if (!TryConsume(kind, out result))
        {
            ThrowUnexpectedTokenInCondition();
        }
    }

    private bool TryConsume(TokenKind kind, out Token result)
    {
        if (_lexer.IsCurrent(kind))
        {
            result = _lexer.Current;
            return Advance();
        }

        result = default;
        return false;
    }

    private bool Same(TokenKind kind)
        => _lexer.IsCurrent(kind) && Advance();

    private bool Advance()
    {
        if (_lexer.Advance())
        {
            if (_lexer.IsCurrent(TokenKind.Unknown))
            {
                ThrowUnexpectedTokenInCondition();
            }

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
