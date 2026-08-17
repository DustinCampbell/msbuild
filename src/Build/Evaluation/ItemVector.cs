// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a parsed item-vector expression.
/// </summary>
/// <param name="text">The complete item-vector expression.</param>
/// <param name="index">The zero-based position of <paramref name="text"/> in the original string.</param>
/// <param name="itemType">The referenced item type.</param>
/// <param name="separator">The explicit separator, or <see langword="null"/> if none was specified.</param>
/// <param name="separatorStart">
///  The offset of <paramref name="separator"/> within <paramref name="text"/>, or <c>-1</c> if none was specified.
/// </param>
/// <param name="transforms">The ordered item transforms.</param>
internal readonly struct ItemVector(
    string text,
    int index,
    string itemType,
    string? separator,
    int separatorStart,
    ImmutableArray<ItemTransform> transforms)
{
    /// <summary>
    ///  Gets the expression text represented by this instance.
    /// </summary>
    public string Text => text;

    /// <summary>
    ///  Gets the zero-based position of <see cref="Text"/> in the original string.
    /// </summary>
    public int Index => index;

    /// <summary>
    ///  Gets the length of <see cref="Text"/>.
    /// </summary>
    public int Length => text.Length;

    /// <summary>
    ///  Gets the referenced item type.
    /// </summary>
    public string ItemType => itemType;

    /// <summary>
    ///  Gets the explicit separator without its enclosing quotes, or <see langword="null"/> if none was specified.
    /// </summary>
    public string? Separator => separator;

    /// <summary>
    ///  Gets the zero-based offset of <see cref="Separator"/> within <see cref="Text"/>, or <c>-1</c> if no
    ///  separator was specified.
    /// </summary>
    public int SeparatorStart => separatorStart;

    /// <summary>
    ///  Gets the ordered item transforms, or an empty array if none were specified.
    /// </summary>
    /// <remarks>
    ///  The returned array is always initialized, even when this instance has its default value.
    /// </remarks>
    public ImmutableArray<ItemTransform> Transforms => transforms.IsDefault ? [] : transforms;

    /// <summary>
    ///  Returns <see cref="Text"/>.
    /// </summary>
    /// <returns>
    ///  The expression text represented by this instance.
    /// </returns>
    public override string ToString()
        => Text;
}
