// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a token in MSBuild conditional expressions.
///  A token is the smallest unit of syntax produced by the scanner, containing
///  the token type and any associated text value.
/// </summary>
internal readonly struct Token
{
    /// <summary>
    ///  Gets a comma token ','.
    /// </summary>
    public static Token Comma => new(TokenKind.Comma);

    /// <summary>
    ///  Gets a left parenthesis token '('.
    /// </summary>
    public static Token LeftParenthesis => new(TokenKind.LeftParenthesis);

    /// <summary>
    ///  Gets a right parenthesis token ')'.
    /// </summary>
    public static Token RightParenthesis => new(TokenKind.RightParenthesis);

    /// <summary>
    ///  Gets a less than operator token '&lt;'.
    /// </summary>
    public static Token LessThan => new(TokenKind.LessThan);

    /// <summary>
    ///  Gets a greater than operator token '&gt;'.
    /// </summary>
    public static Token GreaterThan => new(TokenKind.GreaterThan);

    /// <summary>
    ///  Gets a less than or equal to operator token '&lt;='.
    /// </summary>
    public static Token LessThanOrEqualTo => new(TokenKind.LessThanOrEqualTo);

    /// <summary>
    ///  Gets a greater than or equal to operator token '&gt;='.
    /// </summary>
    public static Token GreaterThanOrEqualTo => new(TokenKind.GreaterThanOrEqualTo);

    /// <summary>
    ///  Gets a logical AND operator token 'and' (case-insensitive).
    /// </summary>
    public static Token And => new(TokenKind.And);

    /// <summary>
    ///  Gets a logical OR operator token 'or' (case-insensitive).
    /// </summary>
    public static Token Or => new(TokenKind.Or);

    /// <summary>
    ///  Gets an equality operator token '=='.
    /// </summary>
    public static Token EqualTo
        => new(TokenKind.EqualTo);

    /// <summary>
    ///  Gets an inequality operator token '!='.
    /// </summary>
    public static Token NotEqualTo
        => new(TokenKind.NotEqualTo);

    /// <summary>
    ///  Gets a logical NOT operator token '!'.
    /// </summary>
    public static Token Not
        => new(TokenKind.Not);

    /// <summary>
    ///  Gets a token representing the end of input.
    /// </summary>
    public static Token EndOfInput
        => new(TokenKind.EndOfInput);

    /// <summary>
    ///  Creates a function token with the specified function name.
    /// </summary>
    /// <param name="text">The name of the function.</param>
    /// <returns>
    ///  A token representing a function call.
    /// </returns>
    public static Token Function(string text)
        => new(TokenKind.Function, text);

    /// <summary>
    ///  Creates a property reference token with the specified property expression.
    /// </summary>
    /// <param name="text">The property expression including $() syntax, e.g., "$(Configuration)".</param>
    /// <returns>
    ///  A token representing a property reference.
    /// </returns>
    public static Token Property(string text)
        => new(TokenKind.Property, text);

    /// <summary>
    ///  Creates an item list reference token with the specified item expression.
    /// </summary>
    /// <param name="text">The item list expression including @() syntax, e.g., "@(Compile)".</param>
    /// <returns>
    ///  A token representing an item list reference.
    /// </returns>
    public static Token ItemList(string text) => new(TokenKind.ItemList, text);

    /// <summary>
    ///  Creates an item metadata reference token with the specified metadata expression.
    /// </summary>
    /// <param name="text">The item metadata expression including %() syntax, e.g., "%(Identity)".</param>
    /// <returns>
    ///  A token representing an item metadata reference.
    /// </returns>
    public static Token ItemMetadata(string text) => new(TokenKind.ItemMetadata, text);

    /// <summary>
    ///  Creates a numeric literal token with the specified numeric text.
    /// </summary>
    /// <param name="text">The numeric value as a string, e.g., "42" or "0xFF".</param>
    /// <returns>
    ///  A token representing a numeric literal.
    /// </returns>
    public static Token Numeric(string text) => new(TokenKind.Numeric, text);

    /// <summary>
    ///  Creates a string literal token with the specified text.
    /// </summary>
    /// <param name="text">The string value.</param>
    /// <param name="expandable">
    ///  Whether the string contains expandable content such as property expressions or escaped characters.
    /// </param>
    /// <returns>
    ///  A token representing a string literal.
    /// </returns>
    public static Token String(string text, bool expandable = false) => new(TokenKind.String, text, expandable);

    private readonly string _text;

    /// <summary>
    ///  Gets the kind of this token.
    /// </summary>
    public TokenKind Kind { get; }

    /// <summary>
    ///  Gets a value indicating whether the token content potentially contains expandable content,
    ///  such as property expressions ($(Property)), item references (@(Item)), metadata (%(Meta)),
    ///  or escaped characters.
    /// </summary>
    /// <remarks>
    ///  This is used for string tokens to indicate whether expansion is needed during evaluation.
    /// </remarks>
    public bool Expandable { get; }

    private Token(TokenKind kind, string? text = null, bool expandable = false)
    {
        Kind = kind;
        _text = text ?? string.Empty;
        Expandable = expandable;
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

    /// <summary>
    ///  Gets the text representation of this token.
    ///  For operator tokens, returns the operator symbol.
    ///  For value tokens (string, numeric, property, etc.), returns the associated text.
    /// </summary>
    public string Text => Kind switch
    {
        TokenKind.Comma => ",",
        TokenKind.LeftParenthesis => "(",
        TokenKind.RightParenthesis => ")",
        TokenKind.LessThan => "<",
        TokenKind.GreaterThan => ">",
        TokenKind.LessThanOrEqualTo => "<=",
        TokenKind.GreaterThanOrEqualTo => ">=",
        TokenKind.And => "and",
        TokenKind.Or => "or",
        TokenKind.EqualTo => "==",
        TokenKind.NotEqualTo => "!=",
        TokenKind.Not => "!",

        TokenKind.Property or
        TokenKind.String or
        TokenKind.Numeric or
        TokenKind.ItemList or
        TokenKind.ItemMetadata or
        TokenKind.Function => _text,

        TokenKind.EndOfInput => string.Empty,

        _ => ErrorUtilities.ThrowInternalErrorUnreachable<string>(),
    };
}
