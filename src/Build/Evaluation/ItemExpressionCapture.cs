// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents one substring for a single successful capture.
/// </summary>
internal readonly struct ItemExpressionCapture
{
    public ItemExpressionCapture(int index, int length, string value, string itemType, string separator, int separatorStart, List<ItemTransform> transforms)
    {
        Index = index;
        Length = length;
        Value = value;
        ItemType = itemType;
        Separator = separator;
        SeparatorStart = separatorStart;
        Transforms = transforms;
    }

    /// <summary>
    ///  Gets the transforms within this capture.
    /// </summary>
    public List<ItemTransform> Transforms { get; }

    /// <summary>
    ///  The position in the original string where the first character of the captured
    ///  substring was found.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///  The length of the captured substring.
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///  Gets the captured substring from the input string.
    /// </summary>
    public string Value { get; }

    /// <summary>
    ///  Gets the captured itemtype.
    /// </summary>
    public string ItemType { get; }

    /// <summary>
    ///  Gets the captured itemtype.
    /// </summary>
    public string Separator { get; }

    /// <summary>
    ///  The starting character of the separator.
    /// </summary>
    public int SeparatorStart { get; }

    /// <summary>
    ///  Gets the captured substring from the input string.
    /// </summary>
    public override string ToString()
        => Value;
}
