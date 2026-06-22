// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Represents one substring for a single successful capture.
/// </summary>
internal struct ItemExpressionCapture
{
    /// <summary>
    /// Create an Expression Capture instance
    /// Represents a sub expression, shredded from a larger expression
    /// </summary>
    public ItemExpressionCapture(int index, string subExpression)
        : this(index, subExpression, null, null, -1, null, null, null)
    {
    }

    public ItemExpressionCapture(int index, string subExpression, string? itemType, string? separator, int separatorStart, List<ItemExpressionCapture>? captures)
        : this(index, subExpression, itemType, separator, separatorStart, captures, null, null)
    {
    }

    /// <summary>
    /// Create an Expression Capture instance
    /// Represents a sub expression, shredded from a larger expression
    /// </summary>
    public ItemExpressionCapture(int index, string subExpression, string? itemType, string? separator, int separatorStart, List<ItemExpressionCapture>? captures, string? functionName, string? functionArguments)
    {
        Index = index;
        Value = subExpression;
        ItemType = itemType;
        Separator = separator;
        SeparatorStart = separatorStart;
        Captures = captures;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }

    /// <summary>
    /// Captures within this capture
    /// </summary>
    public List<ItemExpressionCapture>? Captures { get; }

    /// <summary>
    /// The position in the original string where the first character of the captured
    /// substring was found.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The length of the captured substring.
    /// </summary>
    public int Length => Value?.Length ?? 0;

    /// <summary>
    /// Gets the captured substring from the input string.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the captured itemtype.
    /// </summary>
    public string? ItemType { get; }

    /// <summary>
    /// Gets the captured itemtype.
    /// </summary>
    public string? Separator { get; }

    /// <summary>
    /// The starting character of the separator.
    /// </summary>
    public int SeparatorStart { get; }

    /// <summary>
    /// The function name, if any, within this expression
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    /// The function arguments, if any, within this expression
    /// </summary>
    public string? FunctionArguments { get; }

    /// <summary>
    /// Gets the captured substring from the input string.
    /// </summary>
    public override string ToString()
    {
        return Value;
    }
}
