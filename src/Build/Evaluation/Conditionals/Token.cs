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
        internal static readonly Token Comma = new Token(TokenKind.Comma);
        internal static readonly Token LeftParenthesis = new Token(TokenKind.LeftParenthesis);
        internal static readonly Token RightParenthesis = new Token(TokenKind.RightParenthesis);
        internal static readonly Token LessThan = new Token(TokenKind.LessThan);
        internal static readonly Token GreaterThan = new Token(TokenKind.GreaterThan);
        internal static readonly Token LessThanOrEqualTo = new Token(TokenKind.LessThanOrEqualTo);
        internal static readonly Token GreaterThanOrEqualTo = new Token(TokenKind.GreaterThanOrEqualTo);
        internal static readonly Token And = new Token(TokenKind.And);
        internal static readonly Token Or = new Token(TokenKind.Or);
        internal static readonly Token EqualTo = new Token(TokenKind.EqualTo);
        internal static readonly Token NotEqualTo = new Token(TokenKind.NotEqualTo);
        internal static readonly Token Not = new Token(TokenKind.Not);
        internal static readonly Token EndOfInput = new Token(TokenKind.EndOfInput);

        public TokenKind Kind { get; }
        private string _tokenString;

        /// <summary>
        /// Constructor for types that don't have values
        /// </summary>
        /// <param name="kind"></param>
        private Token(TokenKind kind)
        {
            Kind = kind;
            _tokenString = null;
        }

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="tokenString"></param>
        internal Token(TokenKind kind, string tokenString)
            : this(kind, tokenString, false /* not expandable */)
        { }

        /// <summary>
        /// Constructor takes the token type and the string that
        /// represents the token.
        /// If the string may contain content that needs expansion, expandable is set.
        /// </summary>
        internal Token(TokenKind kind, string tokenString, bool expandable)
        {
            ErrorUtilities.VerifyThrow(
                kind == TokenKind.Property ||
                kind == TokenKind.String ||
                kind == TokenKind.Numeric ||
                kind == TokenKind.ItemList ||
                kind == TokenKind.ItemMetadata ||
                kind == TokenKind.Function,
                "Unexpected token type");

            ErrorUtilities.VerifyThrowInternalNull(tokenString);

            Kind = kind;
            _tokenString = tokenString;
            this.Expandable = expandable;
        }

        /// <summary>
        /// Whether the content potentially has expandable content,
        /// such as a property expression or escaped character.
        /// </summary>
        internal bool Expandable
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="kind"></param>
        /// <returns></returns>
        internal bool IsKind(TokenKind kind)
        {
            return Kind == kind;
        }

        internal string String
        {
            get
            {
                if (_tokenString != null)
                {
                    return _tokenString;
                }

                // Return a token string for
                // an error message.
                switch (Kind)
                {
                    case TokenKind.Comma:
                        return ",";
                    case TokenKind.LeftParenthesis:
                        return "(";
                    case TokenKind.RightParenthesis:
                        return ")";
                    case TokenKind.LessThan:
                        return "<";
                    case TokenKind.GreaterThan:
                        return ">";
                    case TokenKind.LessThanOrEqualTo:
                        return "<=";
                    case TokenKind.GreaterThanOrEqualTo:
                        return ">=";
                    case TokenKind.And:
                        return "and";
                    case TokenKind.Or:
                        return "or";
                    case TokenKind.EqualTo:
                        return "==";
                    case TokenKind.NotEqualTo:
                        return "!=";
                    case TokenKind.Not:
                        return "!";
                    case TokenKind.EndOfInput:
                        return null;
                    default:
                        ErrorUtilities.ThrowInternalErrorUnreachable();
                        return null;
                }
            }
        }
    }
}
