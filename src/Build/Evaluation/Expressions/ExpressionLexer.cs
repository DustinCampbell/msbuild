// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// High-performance lexer for MSBuild expressions that produces atomic tokens.
/// Unlike the legacy Scanner, this lexer does NOT parse nested structures - it only
/// tokenizes individual characters and character sequences into atomic tokens.
/// </summary>
internal ref struct ExpressionLexer
{
    private readonly StringSegment _expression;
    private int _position;
    private Token _current;

    public ExpressionLexer(StringSegment expression)
    {
        _expression = expression;
        _position = 0;
        _current = default;
    }

    /// <summary>
    /// Current token.
    /// </summary>
    public readonly Token Current => _current;

    /// <summary>
    /// Current position in the expression.
    /// </summary>
    public readonly int Position => _position;

    /// <summary>
    /// Whether we've reached the end of input.
    /// </summary>
    public readonly bool IsAtEnd => _position >= _expression.Length;

    public readonly SourceSpan GetSourceSpan(int start, int end)
        => new(start, _expression[start..end]);

    public readonly SourceSpan GetSourceSpan(int start)
        => GetSourceSpan(start, _position);

    private readonly Token EndOfInput(int start)
        => new(TokenKind.EndOfInput, new(start, StringSegment.Empty));

    private readonly Token NewToken(TokenKind kind, int start, TokenFlags flags = TokenFlags.None)
        => new(kind, GetSourceSpan(start), flags);

    /// <summary>
    /// Advances to the next token. Returns false if we've reached the end of input.
    /// </summary>
    public bool MoveNext()
    {
        SkipWhitespace();

        if (_position >= _expression.Length)
        {
            _current = EndOfInput(_position);
            return false;
        }

        int start = _position;
        ReadOnlySpan<char> span = _expression.AsSpan();
        char ch = span[_position];

        switch (ch)
        {
            case '(':
                _position++;
                _current = NewToken(TokenKind.LeftParenthesis, start);
                return true;

            case ')':
                _position++;
                _current = NewToken(TokenKind.RightParenthesis, start);
                return true;

            case '[':
                _position++;
                _current = NewToken(TokenKind.LeftBracket, start);
                return true;

            case ']':
                _position++;
                _current = NewToken(TokenKind.RightBracket, start);
                return true;

            case ',':
                _position++;
                _current = NewToken(TokenKind.Comma, start);
                return true;

            case ';':
                _position++;
                _current = NewToken(TokenKind.Semicolon, start);
                return true;

            case '.':
                _position++;
                _current = NewToken(TokenKind.Dot, start);
                return true;

            case '$':
                _position++;
                _current = NewToken(TokenKind.DollarSign, start);
                return true;

            case '@':
                _position++;
                _current = NewToken(TokenKind.AtSign, start);
                return true;

            case '%':
                _position++;
                _current = NewToken(TokenKind.PercentSign, start);
                return true;

            case ':':
                if (TryPeek(':'))
                {
                    _position += 2;
                    _current = NewToken(TokenKind.DoubleColon, start);
                    return true;
                }

                goto default;

            case '!':
                if (TryPeek('='))
                {
                    _position += 2;
                    _current = NewToken(TokenKind.NotEqualTo, start);
                }
                else
                {
                    _position++;
                    _current = NewToken(TokenKind.Not, start);
                }

                return true;

            case '=':
                if (TryPeek('='))
                {
                    _position += 2;
                    _current = NewToken(TokenKind.EqualTo, start);
                    return true;
                }

                // Single '=' is an error in MSBuild expressions
                // We'll let the parser handle this error
                _position++;
                _current = NewToken(TokenKind.Identifier, start);
                return true;

            case '<':
                if (TryPeek('='))
                {
                    _position += 2;
                    _current = NewToken(TokenKind.LessThanOrEqualTo, start);
                }
                else
                {
                    _position++;
                    _current = NewToken(TokenKind.LessThan, start);
                }

                return true;

            case '>':
                if (TryPeek('='))
                {
                    _position += 2;
                    _current = NewToken(TokenKind.GreaterThanOrEqualTo, start);
                }
                else
                {
                    _position++;
                    _current = NewToken(TokenKind.GreaterThan, start);
                }

                return true;

            case '-':
                if (TryPeek('>'))
                {
                    _position += 2;
                    _current = NewToken(TokenKind.Arrow, start);
                    return true;
                }

                // Check if this is a negative number
                if (_position + 1 < _expression.Length && char.IsDigit(span[_position + 1]))
                {
                    return LexNumber();
                }

                // Otherwise, it's part of an identifier
                return LexIdentifierOrKeyword();

            case '\'':
            case '"':
            case '`':
                return LexQuotedString(ch);

            default:
                if (CharacterUtilities.IsNumberStart(ch))
                {
                    return LexNumber();
                }

                if (CharacterUtilities.IsIdentifierStart(ch))
                {
                    return LexIdentifierOrKeyword();
                }

                // Unknown character - treat as single-char identifier and let parser error
                _position++;
                _current = NewToken(TokenKind.Unknown, start);
                return true;
        }
    }

    /// <summary>
    /// Peeks at the current character without consuming it.
    /// Returns '\0' if at end of input.
    /// </summary>
    public readonly char PeekChar()
        => _position < _expression.Length ? _expression[_position] : '\0';

    /// <summary>
    /// Peeks at the next character without advancing.
    /// </summary>
    private readonly bool TryPeek(char expected)
        => _position + 1 < _expression.Length && _expression[_position + 1] == expected;

    /// <summary>
    /// Skips whitespace characters.
    /// </summary>
    private void SkipWhitespace()
    {
        ReadOnlySpan<char> span = _expression.AsSpan();
        while (_position < span.Length && char.IsWhiteSpace(span[_position]))
        {
            _position++;
        }
    }

    /// <summary>
    /// Lexes a quoted string literal: 'foo', "bar", or `baz`.
    /// </summary>
    private bool LexQuotedString(char quoteChar)
    {
        int start = _position;
        _position++; // Skip opening quote

        ReadOnlySpan<char> span = _expression.AsSpan();

        TokenFlags flags = TokenFlags.None;

        // Scan to closing quote
        while (_position < span.Length && span[_position] != quoteChar)
        {
            char ch = span[_position];

            if (ch == '%')
            {
                flags |= TokenFlags.ContainsPercent;
            }
            else if (ch == '$')
            {
                flags |= TokenFlags.ContainsDollar;
            }
            else if (ch == '@')
            {
                flags |= TokenFlags.ContainsAtSign;
            }

            _position++;
        }

        if (_position >= span.Length)
        {
            // Unclosed string - return unknown token.
            _current = NewToken(TokenKind.Unknown, start);
            return false;
        }

        _position++; // Skip closing quote
        _current = NewToken(TokenKind.String, start, flags);
        return true;
    }

    /// <summary>
    /// Lexes a numeric literal: 42, 3.14, 0xFF, -5.
    /// </summary>
    private bool LexNumber()
    {
        int start = _position;
        ReadOnlySpan<char> span = _expression.AsSpan();

        // Handle optional sign
        if (span[_position] is '+' or '-')
        {
            _position++;
        }

        // Check for hex number (0x...)
        if (_position + 1 < span.Length &&
            span[_position] == '0' &&
            span[_position + 1] is 'x' or 'X')
        {
            _position += 2;

            if (_position >= span.Length || !CharacterUtilities.IsHexDigit(span[_position]))
            {
                // Invalid hex number - let parser handle error
                _current = NewToken(TokenKind.Number, start);
                return true;
            }

            while (_position < span.Length && CharacterUtilities.IsHexDigit(span[_position]))
            {
                _position++;
            }
        }
        else
        {
            // Decimal number (possibly with decimal point)
            while (_position < span.Length && char.IsDigit(span[_position]))
            {
                _position++;
            }

            // Check for decimal point
            if (_position < span.Length && span[_position] == '.')
            {
                _position++;

                // Digits after decimal point
                while (_position < span.Length && char.IsDigit(span[_position]))
                {
                    _position++;
                }
            }
        }

        _current = NewToken(TokenKind.Number, start);
        return true;
    }

    /// <summary>
    /// Lexes an identifier or keyword (and, or).
    /// Identifiers can contain: A-Z, a-z, 0-9, _, -
    /// </summary>
    private bool LexIdentifierOrKeyword()
    {
        int start = _position;
        ReadOnlySpan<char> span = _expression.AsSpan();

        // First character must be a letter or underscore
        if (!CharacterUtilities.IsIdentifierStart(span[_position]))
        {
            _position++;
            _current = NewToken(TokenKind.Identifier, start);
            return true;
        }

        _position++;

        // Subsequent characters can be letters, digits, underscores, or hyphens
        while (_position < span.Length)
        {
            char ch = span[_position];

            if (CharacterUtilities.IsIdentifierChar(ch))
            {
                if (_position + 1 < span.Length && span[_position + 1] == '>')
                {
                    // Don't consume the hyphen - it's part of an arrow
                    break;
                }

                _position++;
            }
            else
            {
                break;
            }
        }

        var text = _expression[start.._position];

        // Check for keywords (case-insensitive)
        _current = text.Equals("and", StringComparison.OrdinalIgnoreCase)
            ? NewToken(TokenKind.And, start)
            : text.Equals("or", StringComparison.OrdinalIgnoreCase)
                ? NewToken(TokenKind.Or, start)
                : NewToken(TokenKind.Identifier, start);

        return true;
    }

    /// <summary>
    /// Creates a diagnostic-friendly string representation of the current position.
    /// </summary>
    public override readonly string ToString()
    {
        if (_position >= _expression.Length)
        {
            return $"Position {_position} (EOF)";
        }

        int contextStart = Math.Max(0, _position - 10);
        int contextEnd = Math.Min(_expression.Length, _position + 10);

        string before = _expression[contextStart.._position].ToString();
        string after = _expression[_position..contextEnd].ToString();

        return $"Position {_position}: ...{before}^{after}...";
    }
}
