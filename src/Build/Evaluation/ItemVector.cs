// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single <c>@(...)</c> item vector expression shredded from a larger expression,
///  along with its position, item type, optional separator, and any transforms applied to it.
/// </summary>
/// <param name="text">The captured item vector expression, as a view over the original expression.</param>
/// <param name="itemType">The item type named inside the <c>@(...)</c>, or <see langword="null"/> if none.</param>
/// <param name="separator">The custom separator used to join the items (e.g. <c>@(Foo, ';')</c>), or <see langword="null"/> if none.</param>
/// <param name="separatorStart">
///  The position within <see cref="Text"/> where <paramref name="separator"/> begins, or <c>-1</c> if there is no separator.
/// </param>
/// <param name="transforms">The transforms applied to the item vector; empty if none.</param>
internal readonly struct ItemVector(
    StringSegment text,
    string? itemType = null,
    string? separator = null,
    int separatorStart = -1,
    ImmutableArray<ItemTransform> transforms = default)
{
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
    ///  Gets the item type named inside the <c>@(...)</c>, or <see langword="null"/> if none.
    /// </summary>
    public string? ItemType { get; } = itemType;

    /// <summary>
    ///  Gets a value indicating whether the item vector specifies a custom separator.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Separator))]
    public bool HasSeparator => Separator is not null;

    /// <summary>
    ///  Gets the custom separator used to join the items (e.g. <c>@(Foo, ';')</c>), or <see langword="null"/> if none.
    /// </summary>
    public string? Separator { get; } = separator;

    /// <summary>
    ///  Gets the position within <see cref="Text"/> where <see cref="Separator"/> begins, or <c>-1</c> if there is no separator.
    /// </summary>
    public int SeparatorStart { get; } = separatorStart;

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
