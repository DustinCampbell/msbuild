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
        internal static readonly Token Comma = new Token(TokenType.Comma, ",");
        internal static readonly Token LeftParenthesis = new Token(TokenType.LeftParenthesis, "(");
        internal static readonly Token RightParenthesis = new Token(TokenType.RightParenthesis, ")");
        internal static readonly Token LessThan = new Token(TokenType.LessThan, "<");
        internal static readonly Token GreaterThan = new Token(TokenType.GreaterThan, ">");
        internal static readonly Token LessThanOrEqualTo = new Token(TokenType.LessThanOrEqualTo, "<=");
        internal static readonly Token GreaterThanOrEqualTo = new Token(TokenType.GreaterThanOrEqualTo, ">=");
        internal static readonly Token And = new Token(TokenType.And, "and");
        internal static readonly Token Or = new Token(TokenType.Or, "or");
        internal static readonly Token True = new Token(TokenType.True, "true");
        internal static readonly Token False = new Token(TokenType.False, "false");
        internal static readonly Token On = new Token(TokenType.On, "on");
        internal static readonly Token Off = new Token(TokenType.Off, "off");
        internal static readonly Token Yes = new Token(TokenType.Yes, "yes");
        internal static readonly Token No = new Token(TokenType.No, "no");
        internal static readonly Token EqualTo = new Token(TokenType.EqualTo, "=");
        internal static readonly Token NotEqualTo = new Token(TokenType.NotEqualTo, "!=");
        internal static readonly Token Not = new Token(TokenType.Not, "not");
        internal static readonly Token EndOfInput = new Token(TokenType.EndOfInput);

        /// <summary>
        /// Valid tokens
        /// </summary>
        internal enum TokenType
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

        private TokenType _tokenType;
        private string _tokenString;


        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token
        /// </summary>
        /// <param name="type"></param>
        /// <param name="tokenString"></param>
        internal Token(TokenType type, string tokenString = null)
        {
            _tokenType = type;
            _tokenString = tokenString ?? string.Empty;
        }

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token.
        /// If the string may contain content that needs expansion, expandable is set.
        /// </summary>
        internal Token(TokenType type, string tokenString, bool expandable)
        {
            ErrorUtilities.VerifyThrow(
                type == TokenType.Property ||
                type == TokenType.String ||
                type == TokenType.Numeric ||
                type == TokenType.ItemList ||
                type == TokenType.ItemMetadata ||
                type == TokenType.Function,
                "Unexpected token type");

            ErrorUtilities.VerifyThrowInternalNull(tokenString);

            _tokenType = type;
            _tokenString = tokenString;
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
        /// <param name="type"></param>
        /// <returns></returns>
        internal bool IsToken(TokenType type)
        {
            return _tokenType == type;
        }

        internal string String => _tokenString;
    }
}
