// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// This class represents a token in the Complex Conditionals grammar.  It's
/// really just a bag that contains the type of the token and the string that
/// was parsed into the token.  This isn't very useful for operators, but
/// is useful for strings and such.
/// </summary>
internal sealed class Token
{
    public static readonly Token None = new(TokenKind.None);
    public static readonly Token Comma = new(TokenKind.Comma);
    public static readonly Token LeftParenthesis = new(TokenKind.LeftParenthesis);
    public static readonly Token RightParenthesis = new(TokenKind.RightParenthesis);
    public static readonly Token LessThan = new(TokenKind.LessThan);
    public static readonly Token GreaterThan = new(TokenKind.GreaterThan);
    public static readonly Token LessThanOrEqualTo = new(TokenKind.LessThanOrEqualTo);
    public static readonly Token GreaterThanOrEqualTo = new(TokenKind.GreaterThanOrEqualTo);
    public static readonly Token And = new(TokenKind.And);
    public static readonly Token Or = new(TokenKind.Or);
    public static readonly Token EqualTo = new(TokenKind.EqualTo);
    public static readonly Token NotEqualTo = new(TokenKind.NotEqualTo);
    public static readonly Token Not = new(TokenKind.Not);
    public static readonly Token EndOfInput = new(TokenKind.EndOfInput);

    public static Token Numeric(string text)
        => new(TokenKind.Numeric, text);

    public static Token String(string text, bool expandable = false)
        => new(TokenKind.String, text, expandable);

    public static Token Function(string text)
        => new(TokenKind.Function, text);

    public static Token ItemList(string text)
        => new(TokenKind.ItemList, text);

    public static Token ItemMetadata(string text)
        => new(TokenKind.ItemMetadata, text);

    public static Token Property(string text)
        => new(TokenKind.Property, text);

    public TokenKind Kind { get; }
    private readonly string? _text;

    /// <summary>
    /// Whether the content potentially has expandable content,
    /// such as a property expression or escaped character.
    /// </summary>
    public bool Expandable { get; }

    private Token(TokenKind kind, string? text = null, bool expandable = false)
    {
        Kind = kind;
        _text = text;
        Expandable = expandable;
    }

    public bool IsKind(TokenKind kind)
        => Kind == kind;

    public string Text
    {
        get
        {
            if (_text != null)
            {
                return _text;
            }

            // Return a token string for an error message.
            return Kind switch
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
                TokenKind.EndOfInput => string.Empty,

                _ => ErrorUtilities.ThrowInternalErrorUnreachable<string>(),
            };
        }
    }
}
