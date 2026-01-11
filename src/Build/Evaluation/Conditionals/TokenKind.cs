// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Valid tokens.
/// </summary>
internal enum TokenKind
{
    None,
    EndOfInput,

    Comma,
    LeftParenthesis,
    RightParenthesis,

    LessThan,
    GreaterThan,
    LessThanOrEqualTo,
    GreaterThanOrEqualTo,
    EqualTo,
    NotEqualTo,

    Not,

    And,
    Or,
    True,
    False,
    On,
    Off,
    Yes,
    No,

    Numeric,
    String,

    Function,
    Property,
    ItemList,
    ItemMetadata,
}
