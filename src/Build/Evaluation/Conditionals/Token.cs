// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        public static Token Numeric(string text) => new(TokenKind.Numeric, text);

        public static Token Property(string text) => new(TokenKind.Property, text);

        public static Token ItemMetadata(string text) => new(TokenKind.ItemMetadata, text);

        public static Token ItemList(string text) => new(TokenKind.ItemList, text);

        public static Token Function(string text) => new(TokenKind.Function, text);

        public static Token String(string text, bool expandable = false)
            => new(TokenKind.String, text, expandable ? TokenFlags.IsExpandable : TokenFlags.None);

        public TokenKind Kind { get; }

        public string Text { get; }

        private readonly TokenFlags _flags;

        /// <summary>
        /// Whether the content potentially has expandable content,
        /// such as a property expression or escaped character.
        /// </summary>
        public bool IsExpandable => (_flags & TokenFlags.IsExpandable) != 0;

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="text"></param>
        /// <param name="flags"></param>
        private Token(TokenKind kind, string text = "", TokenFlags flags = TokenFlags.None)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            _flags = flags;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        internal bool IsToken(TokenKind kind)
        {
            return Kind == kind;
        }
    }
}
