// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single item vector expression (or a transform sub-expression within one)
///  parsed out of a larger string, e.g. <c>@(Compile-&gt;'%(Filename)', ';')</c>.
/// </summary>
internal readonly struct ItemExpressionCapture
{
    /// <summary>
    ///  Gets the text of the parsed expression, e.g. <c>@(Compile)</c> for a top-level item vector
    ///  expression or <c>Metadata('Filename')</c> for a transform sub-expression.
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
    ///  Set only on a top-level item vector expression.
    /// </summary>
    public string? ItemType { get; }

    /// <summary>
    ///  Gets the literal string used to join the expanded items, e.g. <c>;</c> in <c>@(Compile, ';')</c>,
    ///  or <see langword="null"/> when no separator was specified.
    ///  Set only on a top-level item vector expression.
    /// </summary>
    public string? Separator { get; }

    /// <summary>
    ///  Gets the offset of the separator relative to the start of this expression,
    ///  or <c>-1</c> when there is no separator.
    /// </summary>
    public int SeparatorStart { get; }

    /// <summary>
    ///  Gets the transform sub-expressions applied to the item vector, one per <c>-&gt;</c> link
    ///  (e.g. the sub-expressions for <c>-&gt;'%(Filename)'</c> and <c>-&gt;Distinct()</c> in
    ///  <c>@(Compile-&gt;'%(Filename)'-&gt;Distinct())</c>), or <see langword="null"/> when the
    ///  expression has no transforms.
    /// </summary>
    public List<ItemExpressionCapture>? Captures { get; }

    /// <summary>
    ///  Gets the item function name for a function transform sub-expression, e.g. <c>Metadata</c> in
    ///  <c>-&gt;Metadata('Filename')</c>. <see langword="null"/> for a quoted transform (e.g.
    ///  <c>-&gt;'%(Filename)'</c>) or a top-level expression.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    ///  Gets the raw argument text for a function transform sub-expression, e.g. <c>'Filename'</c> in
    ///  <c>-&gt;Metadata('Filename')</c>, or <see langword="null"/> when the function takes no
    ///  arguments or this is not a function transform.
    /// </summary>
    public string? FunctionArguments { get; }

    public ItemExpressionCapture(string text, int index)
        : this(text, index, itemType: null, separator: null, separatorStart: -1, captures: null, functionName: null, functionArguments: null)
    {
    }

    public ItemExpressionCapture(string text, int index, string functionName, string? functionArguments)
        : this(text, index, itemType: null, separator: null, separatorStart: -1, captures: null, functionName, functionArguments)
    {
    }

    public ItemExpressionCapture(
        string text,
        int index,
        string? itemType,
        string? separator,
        int separatorStart,
        List<ItemExpressionCapture>? captures)
        : this(text, index, itemType, separator, separatorStart, captures, functionName: null, functionArguments: null)
    {
    }

    private ItemExpressionCapture(
        string text,
        int index,
        string? itemType,
        string? separator,
        int separatorStart,
        List<ItemExpressionCapture>? captures,
        string? functionName,
        string? functionArguments)
    {
        Text = text;
        Index = index;
        ItemType = itemType;
        Separator = separator;
        SeparatorStart = separatorStart;
        Captures = captures;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }

    /// <summary>
    ///  Returns the expression text (<see cref="Text"/>).
    /// </summary>
    public override string ToString()
        => Text;
}
