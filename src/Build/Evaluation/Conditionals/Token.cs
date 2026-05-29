// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a token in the conditional expression grammar.
/// </summary>
internal readonly struct Token
{
    public static readonly Token Comma = new(TokenKind.Comma, ",");
    public static readonly Token LeftParenthesis = new(TokenKind.LeftParenthesis, "(");
    public static readonly Token RightParenthesis = new(TokenKind.RightParenthesis, ")");
    public static readonly Token LessThan = new(TokenKind.LessThan, "<");
    public static readonly Token GreaterThan = new(TokenKind.GreaterThan, ">");
    public static readonly Token LessThanOrEqualTo = new(TokenKind.LessThanOrEqualTo, "<=");
    public static readonly Token GreaterThanOrEqualTo = new(TokenKind.GreaterThanOrEqualTo, ">=");
    public static readonly Token And = new(TokenKind.And, "and");
    public static readonly Token Or = new(TokenKind.Or, "or");
    public static readonly Token True = new(TokenKind.True, "true");
    public static readonly Token False = new(TokenKind.False, "false");
    public static readonly Token EqualTo = new(TokenKind.EqualTo, "==");
    public static readonly Token NotEqualTo = new(TokenKind.NotEqualTo, "!=");
    public static readonly Token Not = new(TokenKind.Not, "!");
    public static readonly Token EndOfInput = new(TokenKind.EndOfInput, ReadOnlyMemory<char>.Empty);

    public TokenKind Kind { get; }

    /// <summary>
    ///  Whether the content potentially has expandable content,
    ///  such as a property expression or escaped character.
    /// </summary>
    public bool Expandable { get; }

    public ReadOnlyMemory<char> Text { get; }

    private Token(TokenKind kind, string text, bool expandable = false)
        : this(kind, text.AsMemory(), expandable)
    {
    }

    private Token(TokenKind kind, ReadOnlyMemory<char> text, bool expandable = false)
    {
        Kind = kind;
        Text = text;
        Expandable = expandable;
    }

    public static Token Number(ReadOnlyMemory<char> text)
        => new(TokenKind.Number, text);

    public static Token FunctionName(ReadOnlyMemory<char> text)
        => new(TokenKind.FunctionName, text);

    public static Token Property(ReadOnlyMemory<char> text)
        => new(TokenKind.Property, text);

    public static Token ItemMetadata(ReadOnlyMemory<char> text)
        => new(TokenKind.ItemMetadata, text);

    public static Token ItemList(ReadOnlyMemory<char> text)
        => new(TokenKind.ItemList, text);

    public static Token String(ReadOnlyMemory<char> text, bool expandable = false)
        => new(TokenKind.String, text, expandable);

    public bool IsKind(TokenKind kind)
        => Kind == kind;

    public override string ToString()
        => Text.ToString();
}
