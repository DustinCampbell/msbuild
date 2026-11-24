// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Identifies a token in an MSBuild condition expression.
/// </summary>
internal enum TokenKind
{
    /// <summary>
    ///  No token kind is specified.
    /// </summary>
    None = 0,

    /// <summary>
    ///  The end of the condition expression.
    /// </summary>
    EndOfInput,

    /// <summary>
    ///  The argument separator <c>,</c>.
    /// </summary>
    Comma,

    /// <summary>
    ///  The opening parenthesis <c>(</c>.
    /// </summary>
    LeftParenthesis,

    /// <summary>
    ///  The closing parenthesis <c>)</c>.
    /// </summary>
    RightParenthesis,

    /// <summary>
    ///  The less-than operator <c>&lt;</c>.
    /// </summary>
    LessThan,

    /// <summary>
    ///  The greater-than operator <c>&gt;</c>.
    /// </summary>
    GreaterThan,

    /// <summary>
    ///  The less-than-or-equal operator <c>&lt;=</c>.
    /// </summary>
    LessThanOrEqualTo,

    /// <summary>
    ///  The greater-than-or-equal operator <c>&gt;=</c>.
    /// </summary>
    GreaterThanOrEqualTo,

    /// <summary>
    ///  The case-insensitive logical operator <c>and</c>.
    /// </summary>
    And,

    /// <summary>
    ///  The case-insensitive logical operator <c>or</c>.
    /// </summary>
    Or,

    /// <summary>
    ///  The equality operator <c>==</c>.
    /// </summary>
    EqualTo,

    /// <summary>
    ///  The inequality operator <c>!=</c>.
    /// </summary>
    NotEqualTo,

    /// <summary>
    ///  The logical negation operator <c>!</c>.
    /// </summary>
    Not,

    /// <summary>
    ///  A simple unquoted string or the contents of a single-quoted string.
    /// </summary>
    String,

    /// <summary>
    ///  A number token, including decimal, hexadecimal, and dotted forms.
    /// </summary>
    Number,

    /// <summary>
    ///  An unquoted property reference, including its <c>$(</c> and <c>)</c> delimiters.
    /// </summary>
    Property,

    /// <summary>
    ///  An unquoted item-list expression, including its <c>@(</c> and <c>)</c> delimiters.
    /// </summary>
    ItemList,

    /// <summary>
    ///  An unquoted metadata reference, including its <c>%(</c> and <c>)</c> delimiters.
    /// </summary>
    ItemMetadata,

    /// <summary>
    ///  A condition-function name followed by optional whitespace and a separate opening-parenthesis token.
    /// </summary>
    FunctionName,
}
