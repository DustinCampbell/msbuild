// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Expansion;

/// <summary>
///  An entry in the transform pipeline, carrying a value and optionally the item
///  it was derived from.
/// </summary>
/// <typeparam name="TItem">The type of item carried through the transform pipeline.</typeparam>
/// <param name="value">The escaped value at this point in the transform pipeline.</param>
/// <param name="item">The item from which <paramref name="value"/> was derived, or <see langword="null"/>.</param>
/// <remarks>
///  <para>
///   <see cref="Value"/> may be <see langword="null"/> when the entry represents a filtered-out item retained for
///   inclusion as a <see langword="null"/> entry.
///  </para>
///  <para>
///   <see cref="Item"/> is <see langword="null"/> when the entry was synthesized by a transform and no source item
///   is available to carry metadata.
///  </para>
/// </remarks>
internal readonly struct TransformEntry<TItem>(string? value, TItem? item)
    where TItem : class, IItem
{
    /// <summary>
    ///  Gets the current string value (escaped) at this point in the pipeline.
    /// </summary>
    public string? Value { get; } = value;

    /// <summary>
    ///  Gets the item this entry was derived from, used to carry metadata forward
    ///  through the pipeline.
    /// </summary>
    public TItem? Item { get; } = item;

    /// <summary>
    ///  Deconstructs the entry into its value and source item.
    /// </summary>
    /// <param name="value">Receives <see cref="Value"/>.</param>
    /// <param name="item">Receives <see cref="Item"/>.</param>
    public void Deconstruct(out string? value, out TItem? item)
    {
        value = Value;
        item = Item;
    }
}
