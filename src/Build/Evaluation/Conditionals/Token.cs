// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Represents a token in the MSBuild conditional expression grammar.
/// Contains the token type and the parsed text representation.
/// </summary>
internal sealed class Token
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
    public static readonly Token On = new(TokenKind.On, "on");
    public static readonly Token Off = new(TokenKind.Off, "off");
    public static readonly Token Yes = new(TokenKind.Yes, "yes");
    public static readonly Token No = new(TokenKind.No, "no");
    public static readonly Token EqualTo = new(TokenKind.EqualTo, "=");
    public static readonly Token NotEqualTo = new(TokenKind.NotEqualTo, "!=");
    public static readonly Token Not = new(TokenKind.Not, "not");
    public static readonly Token EndOfInput = new(TokenKind.EndOfInput);

    /// <summary>
    ///  Creates a numeric token.
    /// </summary>
    /// <param name="text">The numeric text.</param>
    /// <returns>
    ///  A new numeric token.
    /// </returns>
    public static Token Numeric(string text) => new(TokenKind.Numeric, text);

    /// <summary>
    ///  Creates a property reference token.
    /// </summary>
    /// <param name="text">The property reference text.</param>
    /// <returns>
    ///  A new property token.
    /// </returns>
    public static Token Property(string text) => new(TokenKind.Property, text);

    /// <summary>
    ///  Creates an item metadata reference token.
    /// </summary>
    /// <param name="text">The item metadata reference text.</param>
    /// <returns>
    ///  A new item metadata token.
    /// </returns>
    public static Token ItemMetadata(string text) => new(TokenKind.ItemMetadata, text);

    /// <summary>
    ///  Creates an item list reference token.
    /// </summary>
    /// <param name="text">The item list reference text.</param>
    /// <returns>
    ///  A new item list token.
    /// </returns>
    public static Token ItemList(string text) => new(TokenKind.ItemList, text);

    /// <summary>
    ///  Creates a function call token.
    /// </summary>
    /// <param name="text">The function name.</param>
    /// <returns>
    ///  A new function token.
    /// </returns>
    public static Token Function(string text) => new(TokenKind.Function, text);

    /// <summary>
    ///  Creates a string token.
    /// </summary>
    /// <param name="text">The string value.</param>
    /// <param name="expandable">
    ///  Whether the string contains expandable content such as property expressions or escaped characters.
    /// </param>
    /// <returns>
    ///  A new string token.
    /// </returns>
    public static Token String(string text, bool expandable = false)
        => new(TokenKind.String, text, expandable ? TokenFlags.IsExpandable : TokenFlags.None);

    /// <summary>
    ///  Gets the type of this token.
    /// </summary>
    public TokenKind Kind { get; }

    /// <summary>
    ///  Gets the text representation of this token.
    /// </summary>
    public string Text { get; }

    private readonly TokenFlags _flags;

    /// <summary>
    ///  Gets a value indicating whether the content potentially has expandable content,
    ///  such as a property expression or escaped character.
    /// </summary>
    public bool IsExpandable => (_flags & TokenFlags.IsExpandable) != 0;

    /// <summary>
    ///  Initializes a new instance of the <see cref="Token"/> class.
    /// </summary>
    /// <param name="kind">The token type.</param>
    /// <param name="text">The token text.</param>
    /// <param name="flags">Additional token flags.</param>
    private Token(TokenKind kind, string text = "", TokenFlags flags = TokenFlags.None)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        _flags = flags;
    }

    /// <summary>
    ///  Determines whether this token is of the specified kind.
    /// </summary>
    /// <param name="kind">The token kind to check.</param>
    /// <returns>
    ///  <see langword="true"/> if this token matches the specified kind; otherwise, <see langword="false"/>.
    /// </returns>
    public bool IsKind(TokenKind kind)
        => Kind == kind;
}
