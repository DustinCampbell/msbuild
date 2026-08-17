// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a parsed quoted or function item transform.
/// </summary>
internal readonly struct ItemTransform
{
    /// <summary>
    ///  Initializes a quoted item transform.
    /// </summary>
    /// <param name="text">The quoted expression without its enclosing quotes.</param>
    /// <param name="index">The zero-based position of <paramref name="text"/> in the original string.</param>
    public ItemTransform(string text, int index)
    {
        Text = text;
        Index = index;
        FunctionName = null;
        FunctionArguments = null;
    }

    /// <summary>
    ///  Initializes a function item transform.
    /// </summary>
    /// <param name="text">The complete function expression.</param>
    /// <param name="index">The zero-based position of <paramref name="text"/> in the original string.</param>
    /// <param name="functionName">The transform function name.</param>
    /// <param name="functionArguments">The unparsed function arguments, or <see langword="null"/> if none were specified.</param>
    public ItemTransform(string text, int index, string functionName, string? functionArguments)
    {
        Text = text;
        Index = index;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }

    /// <summary>
    ///  Gets the transform expression text represented by this instance.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///  Gets the zero-based position of <see cref="Text"/> in the original string.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///  Gets the length of <see cref="Text"/>.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    ///  Gets the transform function name, or <see langword="null"/> for a quoted transform.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    ///  Gets the unparsed transform function arguments without their enclosing parentheses, or
    ///  <see langword="null"/> if none were specified.
    /// </summary>
    public string? FunctionArguments { get; }

    /// <summary>
    ///  Returns <see cref="Text"/>.
    /// </summary>
    /// <returns>
    ///  The transform expression text represented by this instance.
    /// </returns>
    public override string ToString()
        => Text;
}
