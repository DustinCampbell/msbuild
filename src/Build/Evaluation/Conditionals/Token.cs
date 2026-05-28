// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Valid token kinds in the conditional expression grammar.
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

        EqualTo,
        NotEqualTo,
        Not,

        Property,
        String,
        Number,
        ItemList,
        ItemMetadata,
        FunctionName,

        EndOfInput,
    }

    /// <summary>
    /// Represents a token in the conditional expression grammar.
    /// </summary>
    internal readonly struct Token
    {
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

        private readonly string _text;

        public TokenKind Kind { get; }

        /// <summary>
        ///  Whether the content potentially has expandable content,
        ///  such as a property expression or escaped character.
        /// </summary>
        public bool Expandable { get; }

        private Token(TokenKind kind)
        {
            Kind = kind;
            _text = null;
        }

        private Token(TokenKind kind, string text, bool expandable = false)
        {
            Assumed.NotNull(text);

            Kind = kind;
            _text = text;
            Expandable = expandable;
        }

        public static Token Number(string text)
            => new(TokenKind.Number, text);

        public static Token FunctionName(string text)
            => new(TokenKind.FunctionName, text);

        public static Token Property(string text)
            => new(TokenKind.Property, text);

        public static Token ItemMetadata(string text)
            => new(TokenKind.ItemMetadata, text);

        public static Token ItemList(string text)
            => new(TokenKind.ItemList, text);

        public static Token String(string text, bool expandable = false)
            => new(TokenKind.String, text, expandable);

        public bool IsKind(TokenKind kind)
            => Kind == kind;

        public override string ToString()
            => Text;

        internal string Text
            => _text ?? Kind switch
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
                TokenKind.EndOfInput => null,
                _ => Assumed.Unreachable<string>(),
            };
    }
}
