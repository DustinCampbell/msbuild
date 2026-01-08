// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents the different kinds of tokens that can appear in MSBuild conditional expressions.
/// </summary>
internal enum TokenKind : byte
{
    /// <summary>
    ///  Comma separator, typically used in function arguments.
    /// </summary>
    Comma,

    /// <summary>
    ///  Left parenthesis '(' used for grouping expressions and function calls.
    /// </summary>
    LeftParenthesis,

    /// <summary>
    ///  Right parenthesis ')' used for grouping expressions and function calls.
    /// </summary>
    RightParenthesis,

    /// <summary>
    ///  Less than comparison operator '&lt;'.
    /// </summary>
    LessThan,

    /// <summary>
    ///  Greater than comparison operator '&gt;'.
    /// </summary>
    GreaterThan,

    /// <summary>
    ///  Less than or equal to comparison operator '&lt;='.
    /// </summary>
    LessThanOrEqualTo,

    /// <summary>
    ///  Greater than or equal to comparison operator '&gt;='.
    /// </summary>
    GreaterThanOrEqualTo,

    /// <summary>
    ///  Logical AND operator 'and' (case-insensitive).
    /// </summary>
    And,

    /// <summary>
    ///  Logical OR operator 'or' (case-insensitive).
    /// </summary>
    Or,

    /// <summary>
    ///  Equality comparison operator '=='.
    /// </summary>
    EqualTo,

    /// <summary>
    ///  Inequality comparison operator '!='.
    /// </summary>
    NotEqualTo,

    /// <summary>
    ///  Logical NOT operator '!'.
    /// </summary>
    Not,

    /// <summary>
    ///  MSBuild property reference, e.g., $(PropertyName).
    /// </summary>
    Property,

    /// <summary>
    ///  String literal value.
    /// </summary>
    String,

    /// <summary>
    ///  Numeric literal value.
    /// </summary>
    Numeric,

    /// <summary>
    ///  MSBuild item list reference, e.g., @(ItemName).
    /// </summary>
    ItemList,

    /// <summary>
    ///  MSBuild item metadata reference, e.g., %(ItemName.MetadataName).
    /// </summary>
    ItemMetadata,

    /// <summary>
    ///  Function call expression.
    /// </summary>
    Function,

    /// <summary>
    ///  Special token indicating the end of the input stream.
    /// </summary>
    EndOfInput,
}
