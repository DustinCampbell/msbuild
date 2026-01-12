// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// This struct represents a token in the Complex Conditionals grammar.  It's
/// really just a bag that contains the type of the token and the string that
/// was parsed into the token.  This isn't very useful for operators, but
/// is useful for strings and such.
/// </summary>
internal readonly struct Token
{
    // Special tokens
    public static Token Unknown(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Unknown, text, position);

    public static Token EndOfInput(int position)
        => new(TokenKind.EndOfInput, ReadOnlyMemory<char>.Empty, position);

    // Punctuation
    public static Token Comma(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Comma, text, position);

    public static Token Dot(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Dot, text, position);

    public static Token LeftParenthesis(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.LeftParenthesis, text, position);

    public static Token RightParenthesis(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.RightParenthesis, text, position);

    public static Token LeftBracket(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.LeftBracket, text, position);

    public static Token RightBracket(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.RightBracket, text, position);

    public static Token DoubleColon(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.DoubleColon, text, position);

    public static Token Arrow(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Arrow, text, position);

    // Expression signfiers ($, @, %) for properties, item lists, and metadata.
    public static Token DollarSign(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.DollarSign, text, position);

    public static Token AtSign(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.AtSign, text, position);

    public static Token PercentSign(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.PercentSign, text, position);

    // Comparison operators
    public static Token LessThan(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.LessThan, text, position);

    public static Token GreaterThan(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.GreaterThan, text, position);

    public static Token LessThanOrEqualTo(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.LessThanOrEqualTo, text, position);

    public static Token GreaterThanOrEqualTo(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.GreaterThanOrEqualTo, text, position);

    public static Token EqualTo(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.EqualTo, text, position);

    public static Token NotEqualTo(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.NotEqualTo, text, position);

    // Logical operators
    public static Token Not(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Not, text, position);

    public static Token And(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.And, text, position, flags: TokenFlags.None);

    public static Token Or(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Or, text, position, flags: TokenFlags.None);

    // Boolean keywords
    public static Token True(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.True, text, position, flags: TokenFlags.IsBooleanTrue);

    public static Token False(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.False, text, position, flags: TokenFlags.IsBooleanFalse);

    public static Token On(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.On, text, position, flags: TokenFlags.IsBooleanTrue);

    public static Token Off(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Off, text, position, flags: TokenFlags.IsBooleanFalse);

    public static Token Yes(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Yes, text, position, flags: TokenFlags.IsBooleanTrue);

    public static Token No(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.No, text, position, flags: TokenFlags.IsBooleanFalse);

    // Value tokens
    public static Token Identifier(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Identifier, text, position);

    public static Token Numeric(ReadOnlyMemory<char> text, int position)
        => new(TokenKind.Numeric, text, position);

    public static Token String(ReadOnlyMemory<char> text, TokenFlags flags, int position)
        => new(TokenKind.String, text, position, flags);

    public TokenKind Kind { get; }

    /// <summary>
    /// The text of this token from the original expression.
    /// </summary>
    public ReadOnlyMemory<char> Text { get; }

    /// <summary>
    /// The 0-based position in the expression where this token starts.
    /// </summary>
    public int Position { get; }

    private readonly TokenFlags _flags;

    /// <summary>
    /// Whether the content potentially has expandable content,
    /// such as a property expression or escaped character.
    /// </summary>
    public bool IsExpandable => (_flags & TokenFlags.Expandable) != 0;

    public bool IsBooleanTrue => (_flags & TokenFlags.IsBooleanTrue) != 0;

    public bool IsBooleanFalse => (_flags & TokenFlags.IsBooleanFalse) != 0;

    public Token(TokenKind kind, ReadOnlyMemory<char> text, int position, TokenFlags flags = 0)
    {
        Kind = kind;
        Text = text;
        Position = position;
        _flags = flags;
    }

    public bool IsKind(TokenKind kind)
        => Kind == kind;
}
