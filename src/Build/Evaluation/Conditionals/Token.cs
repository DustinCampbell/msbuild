// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ErrorUtilities = Microsoft.Build.Shared.ErrorUtilities;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This class represents a token in the Complex Conditionals grammar.  It's
    /// really just a bag that contains the type of the token and the string that
    /// was parsed into the token.  This isn't very useful for operators, but
    /// is useful for strings and such.
    /// </summary>
    internal sealed class Token
    {
        internal static readonly Token Comma = new Token(TokenKind.Comma, ",");
        internal static readonly Token LeftParenthesis = new Token(TokenKind.LeftParenthesis, "(");
        internal static readonly Token RightParenthesis = new Token(TokenKind.RightParenthesis, ")");
        internal static readonly Token LessThan = new Token(TokenKind.LessThan, "<");
        internal static readonly Token GreaterThan = new Token(TokenKind.GreaterThan, ">");
        internal static readonly Token LessThanOrEqualTo = new Token(TokenKind.LessThanOrEqualTo, "<=");
        internal static readonly Token GreaterThanOrEqualTo = new Token(TokenKind.GreaterThanOrEqualTo, ">=");
        internal static readonly Token And = new Token(TokenKind.And, "and");
        internal static readonly Token Or = new Token(TokenKind.Or, "or");
        internal static readonly Token True = new Token(TokenKind.True, "true");
        internal static readonly Token False = new Token(TokenKind.False, "false");
        internal static readonly Token On = new Token(TokenKind.On, "on");
        internal static readonly Token Off = new Token(TokenKind.Off, "off");
        internal static readonly Token Yes = new Token(TokenKind.Yes, "yes");
        internal static readonly Token No = new Token(TokenKind.No, "no");
        internal static readonly Token EqualTo = new Token(TokenKind.EqualTo, "=");
        internal static readonly Token NotEqualTo = new Token(TokenKind.NotEqualTo, "!=");
        internal static readonly Token Not = new Token(TokenKind.Not, "not");
        internal static readonly Token EndOfInput = new Token(TokenKind.EndOfInput);

        /// <summary>
        /// Valid tokens
        /// </summary>
        internal enum TokenKind
        {
            Comma,
            LeftParenthesis,
            RightParenthesis,

            LessThan,
            GreaterThan,
            LessThanOrEqualTo,
            GreaterThanOrEqualTo,

            And,
            Or,

            True,
            False,
            On,
            Off,
            Yes,
            No,

            EqualTo,
            NotEqualTo,
            Not,

            Property,
            String,
            Numeric,
            ItemList,
            ItemMetadata,
            Function,

            EndOfInput,
        }

        private TokenKind _kind;
        private string _text;

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="text"></param>
        internal Token(TokenKind kind, string text = "")
        {
            _kind = kind;
            _text = text ?? string.Empty;
        }

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token.
        /// If the string may contain content that needs expansion, expandable is set.
        /// </summary>
        internal Token(TokenKind kind, string text, bool expandable)
        {
            ErrorUtilities.VerifyThrow(
                kind == TokenKind.Property ||
                kind == TokenKind.String ||
                kind == TokenKind.Numeric ||
                kind == TokenKind.ItemList ||
                kind == TokenKind.ItemMetadata ||
                kind == TokenKind.Function,
                "Unexpected token type");

            ErrorUtilities.VerifyThrowInternalNull(text);

            _kind = kind;
            _text = text;
            Expandable = expandable;
        }

        /// <summary>
        /// Whether the content potentially has expandable content,
        /// such as a property expression or escaped character.
        /// </summary>
        internal bool Expandable { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        internal bool IsToken(TokenKind kind)
        {
            return _kind == kind;
        }

        internal string Text => _text;
    }
}
