// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single top-level item vector expression parsed out of a larger string,
///  e.g. <c>@(Compile-&gt;'%(Filename)', ';')</c>.
/// </summary>
internal readonly struct ItemVectorExpression
{
    /// <summary>
    ///  Gets the text of the item vector expression, e.g. <c>@(Compile)</c>.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///  Gets the offset in the original string at which this expression begins.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///  Gets the length of the expression text. Always equal to <see cref="Text"/>'s length.
    /// </summary>
    public int Length => Text?.Length ?? 0;

    /// <summary>
    ///  Gets the referenced item type, e.g. <c>Compile</c> in <c>@(Compile)</c>.
    /// </summary>
    public string ItemType { get; }

    /// <summary>
    ///  Gets the literal string used to join the expanded items, e.g. <c>;</c> in <c>@(Compile, ';')</c>,
    ///  or <see langword="null"/> when no separator was specified.
    /// </summary>
    public string? Separator { get; }

    /// <summary>
    ///  Gets a value indicating whether the expression specifies a separator.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Separator))]
    public bool HasSeparator => Separator != null;

    /// <summary>
    ///  Gets the offset of the separator relative to the start of this expression,
    ///  or <c>-1</c> when there is no separator.
    /// </summary>
    public int SeparatorStart { get; }

    /// <summary>
    ///  Gets the transforms applied to the item vector, one per <c>-&gt;</c> link
    ///  (e.g. the transforms for <c>-&gt;'%(Filename)'</c> and <c>-&gt;Distinct()</c> in
    ///  <c>@(Compile-&gt;'%(Filename)'-&gt;Distinct())</c>), or <see langword="null"/> when the
    ///  expression has no transforms.
    /// </summary>
    public List<ItemTransform>? Transforms { get; }

    /// <summary>
    ///  Gets a value indicating whether the expression has any transforms.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Transforms))]
    public bool HasTransforms => Transforms != null;

    public ItemVectorExpression(
        string text,
        int index,
        string itemType,
        string? separator,
        int separatorStart,
        List<ItemTransform>? transforms)
    {
        Assumed.True(separator is null || separatorStart >= 0, "SeparatorStart must be non-negative when a separator is present.");
        Assumed.True(transforms is null or { Count: > 0 }, "Transforms must be null or non-empty.");

        Text = text;
        Index = index;
        ItemType = itemType;
        Separator = separator;
        SeparatorStart = separatorStart;
        Transforms = transforms;
    }

    /// <summary>
    ///  Returns the expression text (<see cref="Text"/>).
    /// </summary>
    public override string ToString()
        => Text;
}
