// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a token in an MSBuild condition expression.
/// </summary>
internal sealed class Token
{
    /// <summary>
    ///  The shared <c>,</c> token.
    /// </summary>
    public static readonly Token Comma = new(TokenKind.Comma, ",");

    /// <summary>
    ///  The shared <c>(</c> token.
    /// </summary>
    public static readonly Token LeftParenthesis = new(TokenKind.LeftParenthesis, "(");

    /// <summary>
    ///  The shared <c>)</c> token.
    /// </summary>
    public static readonly Token RightParenthesis = new(TokenKind.RightParenthesis, ")");

    /// <summary>
    ///  The shared <c>&lt;</c> token.
    /// </summary>
    public static readonly Token LessThan = new(TokenKind.LessThan, "<");

    /// <summary>
    ///  The shared <c>&gt;</c> token.
    /// </summary>
    public static readonly Token GreaterThan = new(TokenKind.GreaterThan, ">");

    /// <summary>
    ///  The shared <c>&lt;=</c> token.
    /// </summary>
    public static readonly Token LessThanOrEqualTo = new(TokenKind.LessThanOrEqualTo, "<=");

    /// <summary>
    ///  The shared <c>&gt;=</c> token.
    /// </summary>
    public static readonly Token GreaterThanOrEqualTo = new(TokenKind.GreaterThanOrEqualTo, ">=");

    /// <summary>
    ///  The shared case-insensitive <c>and</c> token.
    /// </summary>
    public static readonly Token And = new(TokenKind.And, "and");

    /// <summary>
    ///  The shared case-insensitive <c>or</c> token.
    /// </summary>
    public static readonly Token Or = new(TokenKind.Or, "or");

    /// <summary>
    ///  The shared <c>==</c> token.
    /// </summary>
    public static readonly Token EqualTo = new(TokenKind.EqualTo, "==");

    /// <summary>
    ///  The shared <c>!=</c> token.
    /// </summary>
    public static readonly Token NotEqualTo = new(TokenKind.NotEqualTo, "!=");

    /// <summary>
    ///  The shared <c>!</c> token.
    /// </summary>
    public static readonly Token Not = new(TokenKind.Not, "!");

    /// <summary>
    ///  The shared token marking the end of the condition expression, with empty <see cref="Text"/>.
    /// </summary>
    public static readonly Token EndOfInput = new(TokenKind.EndOfInput, string.Empty);

    /// <summary>
    ///  Gets the token's kind.
    /// </summary>
    public TokenKind Kind { get; }

    /// <summary>
    ///  Gets the token text, or <see cref="string.Empty"/> for <see cref="TokenKind.EndOfInput"/>.
    /// </summary>
    /// <remarks>
    ///  Quoted strings exclude their delimiters. Punctuation and operators use fixed spellings,
    ///  including lowercase <c>and</c> and <c>or</c>, regardless of their spelling in the source.
    /// </remarks>
    public string Text { get; }

    /// <summary>
    ///  Gets whether the token is marked as potentially containing expandable content.
    /// </summary>
    /// <remarks>
    ///  <see cref="Scanner"/> sets this flag for quoted strings that may contain references or
    ///  escape sequences. <see cref="Parser"/> treats property, item-list, and metadata tokens as
    ///  expandable regardless of this flag.
    /// </remarks>
    public bool Expandable { get; }

    /// <summary>
    ///  Initializes a token with its kind, text, and expansion flag.
    /// </summary>
    /// <param name="type">The kind of token to create.</param>
    /// <param name="text">The token text, excluding any surrounding string quotes.</param>
    /// <param name="expandable">
    ///  <see langword="true"/> to mark the text as potentially requiring expansion; otherwise, <see langword="false"/>.
    /// </param>
    private Token(TokenKind type, string text, bool expandable = false)
    {
        Assumed.NotNull(text);

        Kind = type;
        Text = text;
        Expandable = expandable;
    }

    /// <summary>
    ///  Creates a condition-function name token.
    /// </summary>
    /// <param name="text">The function name, excluding whitespace and the opening parenthesis.</param>
    /// <returns>
    ///  A token with kind <see cref="TokenKind.FunctionName"/> and the specified <paramref name="text"/>.
    /// </returns>
    public static Token FunctionName(string text)
        => new(TokenKind.FunctionName, text);

    /// <summary>
    ///  Creates an item-list token.
    /// </summary>
    /// <param name="text">The item-list expression, including its <c>@(</c> and <c>)</c> delimiters.</param>
    /// <returns>
    ///  A token with kind <see cref="TokenKind.ItemList"/> and the specified <paramref name="text"/>.
    /// </returns>
    public static Token ItemList(string text)
        => new(TokenKind.ItemList, text);

    /// <summary>
    ///  Creates an item-metadata token.
    /// </summary>
    /// <param name="text">The metadata reference, including its <c>%(</c> and <c>)</c> delimiters.</param>
    /// <returns>
    ///  A token with kind <see cref="TokenKind.ItemMetadata"/> and the specified <paramref name="text"/>.
    /// </returns>
    public static Token ItemMetadata(string text)
        => new(TokenKind.ItemMetadata, text);

    /// <summary>
    ///  Creates a number token.
    /// </summary>
    /// <param name="text">The scanned number text.</param>
    /// <returns>
    ///  A token with kind <see cref="TokenKind.Number"/> and the specified <paramref name="text"/>.
    /// </returns>
    public static Token Number(string text)
        => new(TokenKind.Number, text);

    /// <summary>
    ///  Creates a property-reference token.
    /// </summary>
    /// <param name="text">The property reference, including its <c>$(</c> and <c>)</c> delimiters.</param>
    /// <returns>
    ///  A token with kind <see cref="TokenKind.Property"/> and the specified <paramref name="text"/>.
    /// </returns>
    public static Token Property(string text)
        => new(TokenKind.Property, text);

    /// <summary>
    ///  Creates a string token with an optional expansion flag.
    /// </summary>
    /// <param name="text">The string text, excluding any surrounding single quotes.</param>
    /// <param name="expandable">
    ///  <see langword="true"/> to mark the text as potentially requiring expansion; otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>
    ///  A token with kind <see cref="TokenKind.String"/> and the specified text and expansion flag.
    /// </returns>
    public static Token String(string text, bool expandable = false)
        => new(TokenKind.String, text, expandable);

    /// <summary>
    ///  Determines whether this token has the specified kind.
    /// </summary>
    /// <param name="kind">The token kind to compare against.</param>
    /// <returns>
    ///  <see langword="true"/> if this token has the specified <paramref name="kind"/>;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsKind(TokenKind kind)
        => Kind == kind;
}
