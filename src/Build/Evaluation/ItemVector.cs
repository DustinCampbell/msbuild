// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a parsed item-vector expression or one of its transform expressions.
/// </summary>
/// <remarks>
///  An item-vector expression provides an <see cref="ItemType"/>, an optional <see cref="Separator"/>, and
///  the ordered transform expressions in <see cref="Vectors"/>. A transform expression instead provides
///  either quoted expression text or a <see cref="FunctionName"/> and <see cref="FunctionArguments"/>.
/// </remarks>
internal readonly struct ItemVector
{
    /// <summary>
    ///  Initializes a quoted transform expression.
    /// </summary>
    /// <param name="text">The expression text without its enclosing quotes.</param>
    /// <param name="index">The zero-based position of <paramref name="text"/> in the original string.</param>
    public ItemVector(string text, int index)
        : this(text, index, null, null, -1, null, null, null)
    {
    }

    /// <summary>
    ///  Initializes an item-vector expression.
    /// </summary>
    /// <param name="text">The complete item-vector expression.</param>
    /// <param name="index">The zero-based position of <paramref name="text"/> in the original string.</param>
    /// <param name="itemType">The referenced item type.</param>
    /// <param name="separator">The explicit separator, or <see langword="null"/> if none was specified.</param>
    /// <param name="separatorStart">The offset of <paramref name="separator"/> within <paramref name="text"/>, or <c>-1</c> if none was specified.</param>
    /// <param name="vectors">The ordered transform expressions, or <see langword="null"/> if none were specified.</param>
    public ItemVector(string text, int index, string itemType, string separator, int separatorStart, List<ItemVector>? vectors)
        : this(text, index, itemType, separator, separatorStart, vectors, null, null)
    {
    }

    /// <summary>
    ///  Initializes an item-vector or function-transform expression.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="index">The zero-based position of <paramref name="text"/> in the original string.</param>
    /// <param name="itemType">The referenced item type, or <see langword="null"/> for a transform expression.</param>
    /// <param name="separator">The explicit separator, or <see langword="null"/> if none was specified.</param>
    /// <param name="separatorStart">The offset of <paramref name="separator"/> within <paramref name="text"/>, or <c>-1</c> if none was specified.</param>
    /// <param name="vectors">The ordered transform expressions, or <see langword="null"/> if none were specified.</param>
    /// <param name="functionName">The transform function name, or <see langword="null"/> for an item vector or quoted transform.</param>
    /// <param name="functionArguments">The unparsed function arguments, or <see langword="null"/> if none were specified.</param>
    public ItemVector(string text, int index, string? itemType, string? separator, int separatorStart, List<ItemVector>? vectors, string? functionName, string? functionArguments)
    {
        Index = index;
        Text = text;
        ItemType = itemType;
        Separator = separator;
        SeparatorStart = separatorStart;
        Vectors = vectors;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }

    /// <summary>
    ///  Gets the expression text represented by this instance.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///  Gets the zero-based position of <see cref="Text"/> in the original string.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///  Gets the length of <see cref="Text"/>.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    ///  Gets the referenced item type, or <see langword="null"/> when this instance represents a transform.
    /// </summary>
    public string? ItemType { get; }

    /// <summary>
    ///  Gets the explicit separator without its enclosing quotes, or <see langword="null"/> if none was specified.
    /// </summary>
    public string? Separator { get; }

    /// <summary>
    ///  Gets the zero-based offset of <see cref="Separator"/> within <see cref="Text"/>, or <c>-1</c> if no
    ///  separator was specified.
    /// </summary>
    public int SeparatorStart { get; }

    /// <summary>
    ///  Gets the ordered transform expressions, or <see langword="null"/> if none were specified.
    /// </summary>
    public List<ItemVector>? Vectors { get; }

    /// <summary>
    ///  Gets the transform function name, or <see langword="null"/> when this instance represents an item vector
    ///  or quoted transform.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    ///  Gets the unparsed transform function arguments without their enclosing parentheses, or
    ///  <see langword="null"/> if none were specified.
    /// </summary>
    public string? FunctionArguments { get; }

    /// <summary>
    ///  Returns <see cref="Text"/>.
    /// </summary>
    /// <returns>
    ///  The expression text represented by this instance.
    /// </returns>
    public override string ToString()
        => Text;
}
