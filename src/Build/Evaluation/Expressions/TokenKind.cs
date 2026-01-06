// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expressions;

internal enum TokenKind
{
    None = 0,
    Unknown,

    // End of input
    EndOfInput,

    // Delimiters
    LeftParenthesis,       // (
    RightParenthesis,      // )
    LeftBracket,           // [
    RightBracket,          // ]
    Comma,                 // ,
    Semicolon,             // ;
    Dot,                   // .
    Arrow,                 // ->
    DoubleColon,           // ::

    // Reference markers
    DollarSign,           // $ (property reference marker)
    AtSign,               // @ (item reference marker)
    PercentSign,          // % (metadata reference marker)

    EqualTo,              // ==
    NotEqualTo,           // !=
    LessThan,             // <
    LessThanOrEqualTo,    // <=
    GreaterThan,          // >
    GreaterThanOrEqualTo, // >=

    Not,                  // !
    And,                  // and (keyword)
    Or,                   // or (keyword)
    String,               // 'a', "b", or `c`
    Number,               // 42, 3.14, 0xFF
    Identifier            // PropertyName, FunctionName, etc.
}

internal static class TokenKindExtensions
{
    public static bool IsRelationalOperator(this TokenKind kind)
        => kind is >= TokenKind.EqualTo and <= TokenKind.GreaterThanOrEqualTo;
}
