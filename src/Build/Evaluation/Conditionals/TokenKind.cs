// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Valid tokens.
/// </summary>
internal enum TokenKind
{
    None,

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
    Numeric,
    ItemList,
    ItemMetadata,
    Function,

    EndOfInput,
}
