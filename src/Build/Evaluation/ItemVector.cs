// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single <c>@(...)</c> item vector expression shredded from a larger expression,
///  along with its position, item type, optional separator, and any transforms applied to it.
/// </summary>
/// <param name="text">The captured item vector expression, as a view over the original expression.</param>
/// <param name="itemTypeStart">
///  The start of the item type, relative to the beginning of <paramref name="text"/>.
/// </param>
/// <param name="itemTypeLength">
///  The length of the item type within <paramref name="text"/>, or <c>0</c> if there is no item type.
/// </param>
/// <param name="separatorStart">
///  The start of the separator, relative to the beginning of <paramref name="text"/>.
/// </param>
/// <param name="separatorLength">
///  The length of the separator within <paramref name="text"/>, or <c>0</c> if there is no separator.
/// </param>
/// <param name="transforms">The transforms applied to the item vector; empty if none.</param>
/// <remarks>
///  The item type and separator are held as offsets into <see cref="Text"/> and sliced on demand,
///  so the whole expression is backed by a single <see cref="StringSegment"/> rather than three
///  separate references to the same string. This mirrors how <see cref="ItemTransform"/> slices its
///  function name and arguments from a single backing segment.
/// </remarks>
internal readonly struct ItemVector(
    StringSegment text,
    int itemTypeStart,
    int itemTypeLength,
    int separatorStart,
    int separatorLength,
    ImmutableArray<ItemTransform> transforms = default)
{
    /// <summary>
    ///  The start of the item type relative to <see cref="Text"/>.
    /// </summary>
    private readonly int _itemTypeStart = itemTypeStart;

    /// <summary>
    ///  The length of the item type within <see cref="Text"/>, or <c>0</c> if there is no item type.
    /// </summary>
    private readonly int _itemTypeLength = itemTypeLength;

    /// <summary>
    ///  The start of the separator relative to <see cref="Text"/>.
    /// </summary>
    private readonly int _separatorStart = separatorStart;

    /// <summary>
    ///  The length of the separator within <see cref="Text"/>.
    /// </summary>
    private readonly int _separatorLength = separatorLength;

    /// <summary>
    ///  Gets the captured item vector expression text as a view over the original expression.
    /// </summary>
    public StringSegment Text { get; } = text;

    /// <summary>
    ///  Gets the position within the original expression where <see cref="Text"/> begins.
    /// </summary>
    public int Index => Text.Offset;

    /// <summary>
    ///  Gets the length of <see cref="Text"/>.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    ///  Gets the item type named inside the <c>@(...)</c>, or a default (no-value) segment if none.
    /// </summary>
    public StringSegment ItemType
        => _itemTypeLength > 0 ? Text.Slice(_itemTypeStart, _itemTypeLength) : default;

    /// <summary>
    ///  Gets the custom separator used to join the items (e.g. <c>@(Foo, ';')</c>), or a default (no-value) segment if none.
    /// </summary>
    public StringSegment Separator
        => _separatorStart >= 0 ? Text.Slice(_separatorStart, _separatorLength) : default;

    /// <summary>
    ///  Gets the transforms applied to the item vector. The array is empty (never default) if there are none.
    /// </summary>
    public ImmutableArray<ItemTransform> Transforms { get; } = transforms.IsDefault ? [] : transforms;

    /// <summary>
    ///  Returns the captured item vector expression text.
    /// </summary>
    /// <returns>
    ///  The value of <see cref="Text"/>.
    /// </returns>
    public override string ToString() => Text.ToString();
}
