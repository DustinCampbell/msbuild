// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents the type of token in the MSBuild conditional expression grammar.
/// </summary>
internal enum TokenKind
{
    /// <summary>
    ///  Comma separator.
    /// </summary>
    Comma,

    /// <summary>
    ///  Left parenthesis.
    /// </summary>
    LeftParenthesis,

    /// <summary>
    ///  Right parenthesis.
    /// </summary>
    RightParenthesis,

    /// <summary>
    ///  Less than operator.
    /// </summary>
    LessThan,

    /// <summary>
    ///  Greater than operator.
    /// </summary>
    GreaterThan,

    /// <summary>
    ///  Less than or equal to operator.
    /// </summary>
    LessThanOrEqualTo,

    /// <summary>
    ///  Greater than or equal to operator.
    /// </summary>
    GreaterThanOrEqualTo,

    /// <summary>
    ///  Logical AND operator.
    /// </summary>
    And,

    /// <summary>
    ///  Logical OR operator.
    /// </summary>
    Or,

    /// <summary>
    ///  Boolean true literal.
    /// </summary>
    True,

    /// <summary>
    ///  Boolean false literal.
    /// </summary>
    False,

    /// <summary>
    ///  Boolean on literal.
    /// </summary>
    On,

    /// <summary>
    ///  Boolean off literal.
    /// </summary>
    Off,

    /// <summary>
    ///  Boolean yes literal.
    /// </summary>
    Yes,

    /// <summary>
    ///  Boolean no literal.
    /// </summary>
    No,

    /// <summary>
    ///  Equality operator.
    /// </summary>
    EqualTo,

    /// <summary>
    ///  Inequality operator.
    /// </summary>
    NotEqualTo,

    /// <summary>
    ///  Logical NOT operator.
    /// </summary>
    Not,

    /// <summary>
    ///  Property reference expression.
    /// </summary>
    Property,

    /// <summary>
    ///  String literal.
    /// </summary>
    String,

    /// <summary>
    ///  Numeric literal.
    /// </summary>
    Numeric,

    /// <summary>
    ///  Item list reference expression.
    /// </summary>
    ItemList,

    /// <summary>
    ///  Item metadata reference expression.
    /// </summary>
    ItemMetadata,

    /// <summary>
    ///  Function call expression.
    /// </summary>
    Function,

    /// <summary>
    ///  End of input marker.
    /// </summary>
    EndOfInput,
}
