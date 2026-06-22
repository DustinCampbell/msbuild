// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single transform applied to an item vector, i.e. one <c>-&gt;</c> link such as
///  <c>-&gt;'%(Filename)'</c> (a quoted transform) or <c>-&gt;Metadata('Filename')</c> (a function
///  transform), parsed out of an item vector expression.
/// </summary>
internal readonly struct ItemTransform
{
    /// <summary>
    ///  Gets the text of the transform, e.g. <c>%(Filename)</c> for a quoted transform or
    ///  <c>Metadata('Filename')</c> for a function transform.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///  Gets the item function name for a function transform, e.g. <c>Metadata</c> in
    ///  <c>-&gt;Metadata('Filename')</c>, or <see langword="null"/> for a quoted transform
    ///  (e.g. <c>-&gt;'%(Filename)'</c>).
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    ///  Gets the raw argument text for a function transform, e.g. <c>'Filename'</c> in
    ///  <c>-&gt;Metadata('Filename')</c>, or <see langword="null"/> when the function takes no
    ///  arguments or this is a quoted transform.
    /// </summary>
    public string? FunctionArguments { get; }

    public ItemTransform(string text)
    {
        Text = text;
    }

    public ItemTransform(string text, string functionName, string? functionArguments)
    {
        Text = text;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }

    /// <summary>
    ///  Returns the transform text (<see cref="Text"/>).
    /// </summary>
    public override string ToString()
        => Text;
}
