// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a top-level item-vector expression.
/// </summary>
/// <param name="text">The complete item-vector expression.</param>
/// <param name="index">The vector's index in the containing expression.</param>
/// <param name="itemType">The captured item type.</param>
/// <param name="separator">The optional captured separator.</param>
/// <param name="transforms">The item transforms, or <see langword="null"/> for a bare vector.</param>
internal readonly struct ItemVector(
    StringSegment text,
    int index,
    StringSegment itemType,
    StringSegment separator,
    List<ItemTransform>? transforms)
{
    private readonly Range _itemTypeRange = text.GetRelativeRange(itemType);
    private readonly Range _separatorRange = text.GetRelativeRange(separator);
    private readonly bool _hasSeparator = separator.HasValue;

    /// <summary>
    ///  Gets the position of this vector in its containing expression.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    ///  Gets the complete item-vector expression.
    /// </summary>
    public StringSegment Text { get; } = text;

    /// <summary>
    ///  Gets the item transforms, or <see langword="null"/> for a bare vector.
    /// </summary>
    public List<ItemTransform>? Transforms { get; } = transforms;

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

    /// <inheritdoc />
    public override string ToString()
        => Text.ValueOrEmpty;
}
