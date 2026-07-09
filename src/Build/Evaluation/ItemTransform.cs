// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single transform step applied to an item vector via the <c>-&gt;</c> operator,
///  such as <c>@(Foo-&gt;'%(Filename)')</c> or <c>@(Foo-&gt;Distinct())</c>. A transform is either a
///  quoted expression (created via <see cref="QuotedExpression"/>) or an item function call
///  (created via <see cref="FunctionCall"/>); <see cref="FunctionName"/> is <see langword="null"/>
///  for the former and non-<see langword="null"/> for the latter.
/// </summary>
internal readonly struct ItemTransform
{
    private readonly int _functionArgumentsIndex;
    private readonly int _functionArgumentsLength;

    /// <summary>
    ///  Gets the full text of the transform as it appeared in the source expression: the content of the
    ///  quoted expression for a quoted transform, or the <c>Function(arguments)</c> text for a function call.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///  Gets the function name for a function-call transform, or <see langword="null"/> if this transform
    ///  is a quoted expression.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    ///  Gets the unparsed function argument text (a slice of <see cref="Text"/>) for a function-call transform.
    ///  The result is empty for a quoted expression or for a function call with no arguments (for example,
    ///  <c>Distinct()</c>).
    /// </summary>
    public ReadOnlyMemory<char> FunctionArguments => Text.AsMemory(_functionArgumentsIndex, _functionArgumentsLength);

    /// <summary>
    ///  Gets a value indicating whether this transform is a quoted expression (as opposed to a function call).
    /// </summary>
    public bool IsQuotedExpression => FunctionName is null;

    /// <summary>
    ///  Gets a value indicating whether this transform is a function call (as opposed to a quoted expression).
    /// </summary>
    public bool IsFunctionCall => FunctionName is not null;

    private ItemTransform(string text, string? functionName, int functionArgumentsIndex, int functionArgumentsLength)
    {
        Text = text;
        FunctionName = functionName;
        _functionArgumentsIndex = functionArgumentsIndex;
        _functionArgumentsLength = functionArgumentsLength;
    }

    /// <summary>
    ///  Creates a transform representing a quoted expression, such as the <c>'%(Filename)'</c> in
    ///  <c>@(Foo-&gt;'%(Filename)')</c>.
    /// </summary>
    /// <param name="text">The content of the quoted expression.</param>
    public static ItemTransform QuotedExpression(string text)
        => new(text, functionName: null, functionArgumentsIndex: 0, functionArgumentsLength: 0);

    /// <summary>
    ///  Creates a transform representing an item function call, such as the <c>Substring('0')</c> in
    ///  <c>@(Foo-&gt;Substring('0'))</c>.
    /// </summary>
    /// <param name="text">The full <c>Function(arguments)</c> text of the transform.</param>
    /// <param name="functionName">The name of the function being called.</param>
    /// <param name="functionArgumentsIndex">The start index of the argument text within <paramref name="text"/>.</param>
    /// <param name="functionArgumentsLength">The length of the argument text within <paramref name="text"/>; zero if the function has no arguments.</param>
    public static ItemTransform FunctionCall(string text, string functionName, int functionArgumentsIndex, int functionArgumentsLength)
        => new(text, functionName, functionArgumentsIndex, functionArgumentsLength);
}
