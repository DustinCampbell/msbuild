// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a top-level item-vector expression.
/// </summary>
/// <param name="text">The complete item-vector expression.</param>
/// <param name="index">The vector's index in the containing expression.</param>
/// <param name="itemTypeRange">The item type's range within <paramref name="text"/>.</param>
/// <param name="separatorRange">The optional separator's range within <paramref name="text"/>.</param>
/// <param name="transforms">The item transforms.</param>
internal readonly struct ItemVector(
    StringSegment text,
    int index,
    Range itemTypeRange,
    Range? separatorRange,
    ImmutableArray<ItemTransform> transforms)
{
    private readonly ImmutableArray<ItemTransform> _transforms = transforms;
    private readonly Range _itemTypeRange = itemTypeRange;
    private readonly Range _separatorRange = separatorRange.GetValueOrDefault();
    private readonly bool _hasSeparator = separatorRange.HasValue;

    /// <summary>
    ///  Gets the position of this vector in its containing expression.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    ///  Gets the complete item-vector expression.
    /// </summary>
    public StringSegment Text { get; } = text;

    /// <summary>
    ///  Gets the length of this vector.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    ///  Gets the item type.
    /// </summary>
    public StringSegment ItemType
        => Text[_itemTypeRange];

    /// <summary>
    ///  Gets a value indicating whether the vector has an explicit separator.
    /// </summary>
    public bool HasSeparator => _hasSeparator;

    /// <summary>
    ///  Gets the explicit separator, or a <see langword="null"/> segment when none was specified.
    /// </summary>
    public StringSegment Separator
        => HasSeparator
            ? Text[_separatorRange]
            : default;

    /// <summary>
    ///  Gets the separator's position in <see cref="Text"/>, or <c>-1</c> when none was specified.
    /// </summary>
    public int SeparatorStart => HasSeparator ? _separatorRange.Start.Value : -1;

    /// <summary>
    ///  Gets the item transforms as a non-default immutable array.
    /// </summary>
    public ImmutableArray<ItemTransform> Transforms
        => _transforms.IsDefault ? [] : _transforms;

    /// <inheritdoc />
    public override string ToString()
        => Text.ValueOrEmpty;
}
