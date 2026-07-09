// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single item transform step (quoted transform or function call).
/// </summary>
internal readonly struct ItemTransform
{
    public ItemTransform(int index, string value)
        : this(index, value, null, null)
    {
    }

    public ItemTransform(int index, string value, string functionName, string functionArguments)
    {
        Index = index;
        Value = value;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }

    /// <summary>
    ///  Gets the position in the original string where the first character of the transform was found.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///  Gets the length of the transform substring.
    /// </summary>
    public int Length => Value.Length;

    /// <summary>
    ///  Gets the transform substring from the input string.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///  The function name, if this transform is a function call.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    ///  The function arguments, if this transform is a function call.
    /// </summary>
    public string FunctionArguments { get; }
}
