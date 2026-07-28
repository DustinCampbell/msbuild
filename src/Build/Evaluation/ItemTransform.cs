// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single transform (one <c>-&gt;</c> segment) applied to an item vector, such as a
///  quoted transform (<c>@(Foo-&gt;'%(Bar)')</c>) or a function transform (<c>@(Foo-&gt;Distinct())</c>).
/// </summary>
/// <param name="text">The captured transform text.</param>
/// <param name="functionName">The transform function name, or <see langword="null"/> if this is a quoted transform.</param>
/// <param name="functionArguments">The transform function arguments, or <see langword="null"/> if there are none.</param>
internal readonly struct ItemTransform(
    string text,
    string? functionName = null,
    string? functionArguments = null)
{
    /// <summary>
    ///  Gets the captured transform text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///  Gets the transform function name, or <see langword="null"/> if this is a quoted transform.
    /// </summary>
    public string? FunctionName { get; } = functionName;

    /// <summary>
    ///  Gets the transform function arguments, or <see langword="null"/> if there are none.
    /// </summary>
    public string? FunctionArguments { get; } = functionArguments;

    /// <summary>
    ///  Returns the captured transform text.
    /// </summary>
    /// <returns>
    ///  The value of <see cref="Text"/>.
    /// </returns>
    public override string ToString() => Text;
}
