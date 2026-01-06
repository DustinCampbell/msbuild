// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
///  Recursive descent parser for MSBuild expressions.
///  Parses a token stream from <see cref="ExpressionLexer"/> into an abstract syntax tree.
/// </summary>
/// <remarks>
///  The parser implements a recursive descent strategy with the following grammar:
///  <code>
///    ConditionalExpr := OrExpr
///    OrExpr          := AndExpr ( 'or' AndExpr )*
///    AndExpr         := RelationalExpr ( 'and' RelationalExpr )*
///    RelationalExpr  := UnaryExpr ( CompOp UnaryExpr )?
///    CompOp          := '==' | '!=' | '&lt;' | '&lt;=' | '&gt;' | '&gt;='
///    UnaryExpr       := '!' UnaryExpr | PostfixExpr
///    PostfixExpr     := PrimaryExpr ( '.' Identifier | '(' ArgumentList ')' )*
///    PrimaryExpr     := Literal | PropertyRef | ItemVector | MetadataRef | '(' Expr ')' | StaticFunctionCall | Identifier
///  </code>
/// </remarks>
internal ref partial struct ExpressionParser
{
    private ExpressionLexer _lexer;
    private readonly int _offset;
    private Token _current;

    private ExpressionParser(StringSegment expression, int offset = 0)
    {
        _lexer = new ExpressionLexer(expression);
        _offset = offset;
        _current = default;

        // Prime the parser with the first token
        Advance();
    }

    /// <summary>
    ///  Attempts to parse the expression into an abstract syntax tree.
    /// </summary>
    /// <param name="expression">The expression string to parse.</param>
    /// <param name="root">
    ///  When this method returns, contains the root <see cref="ExpressionNode"/> if parsing succeeded,
    ///  or <see langword="null"/> if parsing failed.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the expression was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParse(StringSegment expression, [NotNullWhen(true)] out ExpressionNode? root)
    {
        var parser = new ExpressionParser(expression);

        return parser.TryParseConditionalExpression(out root);
    }

    /// <summary>
    ///  Advances to the next token in the token stream.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance()
    {
        _lexer.MoveNext();
        _current = AdjustToken(_lexer.Current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly Token AdjustToken(Token token) => _offset == 0
        ? token
        : new Token(token.Kind, AdjustSourceSpan(token.Source), token.Flags);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly SourceSpan AdjustSourceSpan(SourceSpan source) => _offset == 0
        ? source
        : new SourceSpan(source.Start + _offset, source.Text);

    /// <summary>
    ///  Advances past a token of the expected kind without returning it.
    /// </summary>
    /// <param name="expected">The expected token kind.</param>
    /// <returns>
    ///  <see langword="true"/> if the current token matches and was consumed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Use this for delimiters and punctuation you don't need to store.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryAdvancePast(TokenKind expected)
    {
        if (At(expected))
        {
            Advance();
            return true;
        }

        return false;
    }

    /// <summary>
    ///  Consumes the current token and advances to the next token.
    /// </summary>
    /// <returns>
    ///  The consumed token.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Token Consume()
    {
        Token result = _current;
        Advance();

        return result;
    }

    /// <summary>
    ///  Attempts to consume a token of the expected kind.
    /// </summary>
    /// <param name="expected">The expected token kind.</param>
    /// <param name="token">
    ///  When this method returns, contains the consumed token if successful;
    ///  otherwise, the default token value.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the current token matches and was consumed; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryConsume(TokenKind expected, out Token token)
    {
        if (At(expected))
        {
            token = Consume();
            return true;
        }

        token = default;
        return false;
    }

    /// <summary>
    ///  Checks if the current token is of the expected kind.
    /// </summary>
    /// <param name="expected">The expected token kind.</param>
    /// <returns>
    ///  <see langword="true"/> if the current token matches the expected kind; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly bool At(TokenKind expected)
        => _current.Kind == expected;

    /// <summary>
    ///  Gets a source span from two tokens.
    /// </summary>
    /// <param name="from">The starting token.</param>
    /// <param name="to">The ending token.</param>
    /// <returns>
    ///  A <see cref="SourceSpan"/> covering the range from the start of <paramref name="from"/>
    ///  to the end of <paramref name="to"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly SourceSpan GetSourceSpan(Token from, Token to)
        => GetSourceSpan(from.Start, to.End);

    /// <summary>
    ///  Gets a source span from two expression nodes.
    /// </summary>
    /// <param name="from">The starting node.</param>
    /// <param name="to">The ending node.</param>
    /// <returns>
    ///  A <see cref="SourceSpan"/> covering the range from the start of <paramref name="from"/>
    ///  to the end of <paramref name="to"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly SourceSpan GetSourceSpan(ExpressionNode from, ExpressionNode to)
        => GetSourceSpan(from.Start, to.End);

    /// <summary>
    ///  Gets a source span from a token to an expression node.
    /// </summary>
    /// <param name="from">The starting token.</param>
    /// <param name="to">The ending node.</param>
    /// <returns>
    ///  A <see cref="SourceSpan"/> covering the range from the start of <paramref name="from"/>
    ///  to the end of <paramref name="to"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly SourceSpan GetSourceSpan(Token from, ExpressionNode to)
        => GetSourceSpan(from.Start, to.End);

    /// <summary>
    ///  Gets a source span from an expression node to a token.
    /// </summary>
    /// <param name="from">The starting node.</param>
    /// <param name="to">The ending token.</param>
    /// <returns>
    ///  A <see cref="SourceSpan"/> covering the range from the start of <paramref name="from"/>
    ///  to the end of <paramref name="to"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private readonly SourceSpan GetSourceSpan(ExpressionNode from, Token to)
        => GetSourceSpan(from.Start, to.End);

    private readonly SourceSpan GetSourceSpan(int start, int end)
    {
        if (_offset == 0)
        {
            return _lexer.GetSourceSpan(start, end);
        }

        SourceSpan span = _lexer.GetSourceSpan(start - _offset, end - _offset);
        return new(span.Start + _offset, span.Text);
    }

    /// <summary>
    ///  Parses a conditional expression (the top-level grammar rule).
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   ConditionalExpr := OrExpr
    ///  </code>
    /// </remarks>
    private bool TryParseConditionalExpression([NotNullWhen(true)] out ExpressionNode? result)
        => TryParseOrExpression(out result);

    /// <summary>
    ///  Parses a logical OR expression.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   OrExpr := AndExpr ( 'or' AndExpr )*
    ///  </code>
    /// </remarks>
    private bool TryParseOrExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseAndExpression(out var left))
        {
            result = null;
            return false;
        }

        while (TryConsume(TokenKind.Or, out Token op))
        {
            if (!TryParseAndExpression(out var right))
            {
                result = null;
                return false;
            }

            left = new BinaryOperatorNode(left, op, right, GetSourceSpan(left, right));
        }

        result = left;
        return true;
    }

    /// <summary>
    ///  Parses a logical AND expression.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   AndExpr := RelationalExpr ( 'and' RelationalExpr )*
    ///  </code>
    /// </remarks>
    private bool TryParseAndExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseRelationalExpression(out var left))
        {
            result = null;
            return false;
        }

        while (TryConsume(TokenKind.And, out Token op))
        {
            if (!TryParseRelationalExpression(out var right))
            {
                result = null;
                return false;
            }

            left = new BinaryOperatorNode(left, op, right, GetSourceSpan(left, right));
        }

        result = left;
        return true;
    }

    /// <summary>
    ///  Parses a relational (comparison) expression.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   RelationalExpr := UnaryExpr ( CompOp UnaryExpr )?
    ///   CompOp := '==' | '!=' | '&lt;' | '&lt;=' | '&gt;' | '&gt;='
    ///  </code>
    /// </remarks>
    private bool TryParseRelationalExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryParseUnaryExpression(out var left))
        {
            result = null;
            return false;
        }

        if (!_current.Kind.IsRelationalOperator())
        {
            result = left;
            return true;
        }

        Token op = Consume();

        if (!TryParseUnaryExpression(out var right))
        {
            result = null;
            return false;
        }

        result = new BinaryOperatorNode(left, op, right, GetSourceSpan(left, right));
        return true;
    }

    /// <summary>
    ///  Parses a unary expression (logical NOT).
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   UnaryExpr := '!' UnaryExpr | PostfixExpr
    ///  </code>
    /// </remarks>
    private bool TryParseUnaryExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        if (!TryConsume(TokenKind.Not, out Token op))
        {
            return TryParsePostfixExpression(out result);
        }

        if (!TryParseUnaryExpression(out var operand))
        {
            result = null;
            return false;
        }

        result = new UnaryOperatorNode(op, operand, GetSourceSpan(op, operand));
        return true;
    }

    /// <summary>
    ///  Parses postfix operators: member access (.) and function calls.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   PostfixExpr := PrimaryExpr ( '.' Identifier | '(' ArgumentList ')' )*
    ///  </code>
    /// </remarks>
    private bool TryParsePostfixExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        result = null;

        if (!TryParsePrimaryExpression(out var expression))
        {
            return false;
        }

        while (expression is ReceiverExpressionNode receiver)
        {
            if (TryAdvancePast(TokenKind.Dot))
            {
                if (!TryConsume(TokenKind.Identifier, out Token memberName))
                {
                    return false;
                }

                expression = new MemberAccessNode(receiver, memberName, GetSourceSpan(receiver, memberName));
            }
            else if (TryAdvancePast(TokenKind.LeftParenthesis))
            {
                if (!TryParseArgumentList(out var arguments) ||
                    !TryConsume(TokenKind.RightParenthesis, out Token rightParenthesis))
                {
                    return false;
                }

                expression = new FunctionCallNode(receiver, arguments, GetSourceSpan(receiver, rightParenthesis));
            }
            else
            {
                break;
            }
        }

        result = expression;
        return true;
    }

    /// <summary>
    ///  Parses primary expressions: literals, references, parenthesized expressions, and identifiers.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed expression node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   PrimaryExpr := Literal | PropertyRef | ItemVector | MetadataRef | '(' Expr ')' | StaticFunctionCall | Identifier
    ///  </code>
    /// </remarks>
    private bool TryParsePrimaryExpression([NotNullWhen(true)] out ExpressionNode? result)
    {
        switch (_current.Kind)
        {
            case TokenKind.String:
                if (TryParseString(out var stringNode))
                {
                    result = stringNode;
                    return true;
                }

                break;

            case TokenKind.Number:
                result = new NumericLiteralNode(_current);
                Advance();
                return true;

            case TokenKind.DollarSign:
                if (TryParsePropertyReference(out var propertyReference))
                {
                    result = propertyReference;
                    return true;
                }

                break;

            case TokenKind.AtSign:
                if (TryParseItemVector(out var itemVector))
                {
                    result = itemVector;
                    return true;
                }

                break;

            case TokenKind.PercentSign:
                if (TryParseMetadataReference(out var metadataReference))
                {
                    result = metadataReference;
                    return true;
                }

                break;

            case TokenKind.LeftParenthesis:
                if (TryAdvancePast(TokenKind.LeftParenthesis) &&
                    TryParseConditionalExpression(out var expression) &&
                    TryAdvancePast(TokenKind.RightParenthesis))
                {
                    result = expression;
                    return true;
                }

                break;

            case TokenKind.LeftBracket:
                if (TryParseStaticFunctionCall(out var staticFunctionCall))
                {
                    result = staticFunctionCall;
                    return true;
                }

                break;

            case TokenKind.Identifier:
                Token name = Consume();

                ReceiverExpressionNode identifier = new IdentifierNode(name);

                if (!At(TokenKind.LeftParenthesis))
                {
                    result = identifier;
                    return true;
                }

                if (!TryParseFunctionCall(identifier, out var funcCall))
                {
                    result = null;
                    return false;
                }

                result = funcCall;
                return true;
        }

        result = null;
        return false;
    }

    private bool TryParseString([NotNullWhen(true)] out ExpressionNode? result)
    {
        Debug.Assert(At(TokenKind.String), "Expected a string token.");
        Token token = Consume();

        if (token.Flags == TokenFlags.None)
        {
            result = new StringLiteralNode(token);
            return true;
        }

        return TryParseExpandableString(token, out result);
    }

    private bool TryParseExpandableString(Token token, out ExpressionNode? result)
    {
        using var parts = new RefArrayBuilder<ExpressionNode>();
        SourceSpan remaining = token.Source[1..^1];
        int start = token.Start + 1;

        while (!remaining.Text.IsEmpty)
        {
            int nextIndex = remaining.Text.IndexOfAny('$', '@', '%');

            // We're done! Add the final literal part.
            if (nextIndex < 0)
            {
                parts.Add(new SimpleTextNode(remaining));
                break;
            }

            // Add the literal part before this index.
            if (nextIndex > 0)
            {
                parts.Add(new SimpleTextNode(remaining[..nextIndex]));

                remaining = remaining[nextIndex..];
                start += nextIndex;
            }

            if (TryGetUnescapedCharacter(remaining.Text, out char unescapedChar))
            {
                SourceSpan escapedText = remaining;
                int escapedTextLength = 3;

                remaining = remaining[3..];
                start += 3;

                using var builder = new RefArrayBuilder<char>();
                builder.Add(unescapedChar);

                while (TryGetUnescapedCharacter(remaining.Text, out unescapedChar))
                {
                    escapedTextLength += 3;
                    remaining = remaining[3..];
                    start += 3;

                    builder.Add(unescapedChar);
                }

                string unescapedString = builder.AsSpan().ToString();

                parts.Add(new EscapedTextNode(unescapedString, escapedText[..escapedTextLength]));
                continue;
            }

            // Handle special cases for $(), @(), %() by creating a new parser at this position.
            var parser = new ExpressionParser(remaining.Text, offset: start);
            int length = 0;

            if (parser.At(TokenKind.DollarSign) && parser.TryParsePropertyReference(out var propertyReference))
            {
                parts.Add(propertyReference);
                length = propertyReference.Length;
            }
            else if (parser.At(TokenKind.AtSign) && parser.TryParseItemVector(out var itemVector))
            {
                parts.Add(itemVector);
                length = itemVector.Length;
            }
            else if (parser.At(TokenKind.PercentSign) && parser.TryParseMetadataReference(out var metadataReference))
            {
                parts.Add(metadataReference);
                length = metadataReference.Length;
            }

            if (length == 0)
            {
                // If we got here, we likely encountered a % character that was not an escape sequence
                // or a metadata reference. In this case, we'll add a simple text node for this character
                // and move on.
                parts.Add(new SimpleTextNode(remaining[..1]));
                length = 1;
            }

            remaining = remaining[length..];
            start += length;
        }

        if (parts.Count >= 1)
        {
            // Did we only create SimpleTextNodes? If so, we can consolidate them into a StringLiteralNode.
            if (parts.All(p => p is SimpleTextNode))
            {
                result = new StringLiteralNode(token);
                return true;
            }

            result = new CompositeStringNode(token, parts.ToImmutable());
            return true;
        }

        result = null;
        return false;
    }

    private bool TryGetUnescapedCharacter(StringSegment text, out char result)
    {
        if (text is ['%', char ch1, char ch2, ..] &&
            CharacterUtilities.TryDecodeHexDigit(ch1, out int digit1) &&
            CharacterUtilities.TryDecodeHexDigit(ch2, out int digit2))
        {
            result = (char)((digit1 << 4) + digit2);
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Parses a property reference expression.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed property reference node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   PropertyRef := '$(' Expr ')'
    ///  </code>
    /// </remarks>
    private bool TryParsePropertyReference([NotNullWhen(true)] out ExpressionNode? result)
    {
        Debug.Assert(At(TokenKind.DollarSign), "Expected a $ at the start of a property reference.");
        Token firstToken = Consume();

        if (!TryAdvancePast(TokenKind.LeftParenthesis) ||
            !TryParsePostfixExpression(out var expression) ||
            !TryConsume(TokenKind.RightParenthesis, out Token lastToken))
        {
            result = null;
            return false;
        }

        result = new PropertyReferenceNode(expression, GetSourceSpan(firstToken, lastToken));
        return true;
    }

    /// <summary>
    ///  Parses an item vector expression with optional transforms and separator.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed item vector node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   ItemVector := '@(' Identifier Transform* (',' Expr)? ')'
    ///  </code>
    ///
    ///  Example:
    ///  <code>
    ///   @(Compile->'%(Filename)') or @(Compile, ';')
    ///  </code>
    /// </remarks>
    private bool TryParseItemVector([NotNullWhen(true)] out ItemVectorNode? result)
    {
        Debug.Assert(At(TokenKind.AtSign), "Expected an @ at the start of an item vector.");
        Token firstToken = Consume();

        if (!TryAdvancePast(TokenKind.LeftParenthesis) ||
            !TryConsume(TokenKind.Identifier, out Token itemType) ||
            !TryParseTransforms(out var transforms))
        {
            result = null;
            return false;
        }

        // Optional separator expression
        ExpressionNode? separator = null;

        if (TryAdvancePast(TokenKind.Comma) && !TryParsePrimaryExpression(out separator))
        {
            result = null;
            return false;
        }

        if (!TryConsume(TokenKind.RightParenthesis, out Token lastToken))
        {
            result = null;
            return false;
        }

        result = new ItemVectorNode(itemType, transforms, separator, GetSourceSpan(firstToken, lastToken));
        return true;
    }

    /// <summary>
    ///  Parses zero or more item transforms.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains an immutable array of transform nodes.
    ///  Returns an empty array if no transforms are present.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   Transform := '->' Expr
    ///  </code>
    ///
    ///  Example:
    ///  <code>
    ///   @(Compile->'%(Filename)'->'%(Identity).obj')
    ///  </code>
    /// </remarks>
    private bool TryParseTransforms(out ImmutableArray<TransformNode> result)
    {
        // Optional transforms
        if (!At(TokenKind.Arrow))
        {
            result = [];
            return true;
        }

        using var transforms = new RefArrayBuilder<TransformNode>();

        while (TryConsume(TokenKind.Arrow, out Token arrow))
        {
            if (!TryParsePrimaryExpression(out var transform))
            {
                result = default;
                return false;
            }

            transforms.Add(new TransformNode(transform, GetSourceSpan(arrow, transform)));
        }

        result = transforms.ToImmutable();
        return true;
    }

    /// <summary>
    ///  Parses a metadata reference expression.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed metadata reference node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   MetadataRef := '%(' Identifier ')' | '%(' Identifier '.' Identifier ')'
    ///  </code>
    ///
    ///  Example:
    ///  <code>
    ///   %(Filename) or %(Compile.Filename)
    ///  </code>
    /// </remarks>
    private bool TryParseMetadataReference([NotNullWhen(true)] out ExpressionNode? result)
    {
        Debug.Assert(At(TokenKind.PercentSign), "Expected a % at the start of a metadata reference.");
        Token firstToken = Consume();

        result = null;
        if (!TryAdvancePast(TokenKind.LeftParenthesis) ||
            !TryConsume(TokenKind.Identifier, out Token metadataName))
        {
            return false;
        }

        // Check for unqualified metadata: %(MetadataName)
        if (TryConsume(TokenKind.RightParenthesis, out Token lastToken))
        {
            result = new MetadataReferenceNode(metadataName, GetSourceSpan(firstToken, lastToken));
            return true;
        }

        // Qualified metadata: %(ItemType.MetadataName)
        if (!TryAdvancePast(TokenKind.Dot))
        {
            result = null;
            return false;
        }

        Token itemName = metadataName;

        if (!TryConsume(TokenKind.Identifier, out metadataName) ||
            !TryConsume(TokenKind.RightParenthesis, out lastToken))
        {
            result = null;
            return false;
        }

        result = new MetadataReferenceNode(itemName, metadataName, GetSourceSpan(firstToken, lastToken));
        return true;
    }

    /// <summary>
    ///  Parses a static function call expression.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed static function call node if successful;
    ///  otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   StaticFunctionCall := '[' TypeName ']::' Identifier '(' ArgumentList ')'
    ///  </code>
    ///
    ///  Example:
    ///  <code>
    ///   [System.IO.Path]::GetFileName('foo.txt')
    ///  </code>
    /// </remarks>
    private bool TryParseStaticFunctionCall([NotNullWhen(true)] out ExpressionNode? result)
    {
        Debug.Assert(At(TokenKind.LeftBracket), "Expected a [ at the start of a static function call.");
        Token firstToken = Consume();

        if (TryParseTypeName(out var typeName) &&
            TryAdvancePast(TokenKind.RightBracket) &&
            TryAdvancePast(TokenKind.DoubleColon) && TryConsume(TokenKind.Identifier, out Token methodName))
        {
            var staticMemberAccess = new StaticMemberAccessNode(
                typeName, methodName, GetSourceSpan(firstToken, methodName));

            if (TryParseFunctionCall(staticMemberAccess, out var funcCall))
            {
                result = funcCall;
                return true;
            }
        }

        result = null;
        return false;
    }

    /// <summary>
    ///  Parses a qualified type name (only used inside [brackets] for static calls).
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains the parsed type name node if successful; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Eagerly consumes dots to create a single type name node.
    ///  Examples: <c>System.IO.Path</c>, <c>MyNamespace.MyClass</c>, <c>MSBuild</c>.
    /// </remarks>
    private bool TryParseTypeName([NotNullWhen(true)] out TypeNameNode? result)
    {
        if (!TryConsume(TokenKind.Identifier, out Token firstToken))
        {
            result = null;
            return false;
        }

        // Check if this is a qualified name (has dots)
        if (!At(TokenKind.Dot))
        {
            result = new TypeNameNode(firstToken);
            return true;
        }

        Token lastNamespaceToken = firstToken;

        if (!TryAdvancePast(TokenKind.Dot) ||
            !TryConsume(TokenKind.Identifier, out Token memberName))
        {
            result = null;
            return false;
        }

        while (TryAdvancePast(TokenKind.Dot))
        {
            lastNamespaceToken = memberName;

            if (!TryConsume(TokenKind.Identifier, out memberName))
            {
                result = null;
                return false;
            }
        }

        result = new TypeNameNode(GetSourceSpan(firstToken, lastNamespaceToken), memberName, GetSourceSpan(firstToken, memberName));
        return true;
    }

    /// <summary>
    ///  Parses a function call expression with a receiver and argument list.
    /// </summary>
    /// <param name="receiver">The expression that receives the function call (e.g., object or static member).</param>
    /// <param name="result">
    ///  When this method returns, contains the parsed function call node if successful; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   FunctionCall := Receiver '(' ArgumentList ')'
    ///  </code>
    /// </remarks>
    private bool TryParseFunctionCall(ReceiverExpressionNode receiver, [NotNullWhen(true)] out FunctionCallNode? result)
    {
        if (!TryAdvancePast(TokenKind.LeftParenthesis) ||
            !TryParseArgumentList(out var arguments) ||
            !TryConsume(TokenKind.RightParenthesis, out Token rightParenthesis))
        {
            result = null;
            return false;
        }

        result = new FunctionCallNode(receiver, arguments, GetSourceSpan(receiver, rightParenthesis));
        return true;
    }

    /// <summary>
    ///  Parses a comma-separated list of arguments for a function call.
    /// </summary>
    /// <param name="result">
    ///  When this method returns, contains an immutable array of argument expression nodes.
    ///  Returns an empty array if there are no arguments.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if parsing succeeded; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  Grammar:
    ///  <code>
    ///   ArgumentList := (Expr (',' Expr)*)?
    ///  </code>
    /// </remarks>
    private bool TryParseArgumentList(out ImmutableArray<ExpressionNode> result)
    {
        if (At(TokenKind.RightParenthesis))
        {
            result = [];
            return true;
        }

        using var arguments = new RefArrayBuilder<ExpressionNode>();

        if (!TryParseConditionalExpression(out var firstArg))
        {
            result = default;
            return false;
        }

        arguments.Add(firstArg);

        while (TryAdvancePast(TokenKind.Comma))
        {
            if (!TryParseConditionalExpression(out var arg))
            {
                result = default;
                return false;
            }

            arguments.Add(arg);
        }

        result = arguments.ToImmutable();
        return true;
    }
}
