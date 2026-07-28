// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a single transform (one <c>-&gt;</c> segment) applied to an item vector, such as a
///  quoted transform (<c>@(Foo-&gt;'%(Bar)')</c>) or a function transform (<c>@(Foo-&gt;Distinct())</c>).
/// </summary>
/// <remarks>
///  The transform's characters are held as <see cref="StringSegment"/> views over the original expression
///  string, so parsing a transform allocates no substrings. A single backing segment is stored and the
///  function name and arguments are sliced from it on demand.
/// </remarks>
internal readonly struct ItemTransform
{
    /// <summary>
    ///  The transform's text: for a function transform the whole <c>Name(args)</c> text (the name begins at
    ///  offset 0); for a quoted transform the quoted expression's inner text.
    /// </summary>
    public StringSegment Text { get; }

    /// <summary>
    ///  The length of the function name within <see cref="Text"/>, or <c>-1</c> for a quoted transform.
    /// </summary>
    private readonly int _nameLength;

    /// <summary>
    ///  The start of the function arguments relative to <see cref="Text"/>.
    /// </summary>
    private readonly int _argsStart;

    /// <summary>
    ///  The length of the function arguments, or <c>0</c> if there are none.
    /// </summary>
    private readonly int _argsLength;

    private ItemTransform(StringSegment text, int nameLength, int argsStart, int argsLength)
    {
        Text = text;
        _nameLength = nameLength;
        _argsStart = argsStart;
        _argsLength = argsLength;
    }

    /// <summary>
    ///  Creates a quoted transform (e.g. <c>@(Foo-&gt;'%(Bar)')</c>).
    /// </summary>
    /// <param name="text">The quoted expression's inner text.</param>
    /// <returns>
    ///  A quoted <see cref="ItemTransform"/> over <paramref name="text"/>.
    /// </returns>
    public static ItemTransform Quoted(StringSegment text)
        => new(text, nameLength: -1, argsStart: 0, argsLength: 0);

    /// <summary>
    ///  Creates a function transform (e.g. <c>@(Foo-&gt;Distinct())</c>).
    /// </summary>
    /// <param name="text">The whole transform text, beginning at the function name.</param>
    /// <param name="nameLength">The length of the function name within <paramref name="text"/>.</param>
    /// <param name="argsStart">The start of the arguments relative to <paramref name="text"/>.</param>
    /// <param name="argsLength">The length of the arguments, or <c>0</c> if there are none.</param>
    /// <returns>
    ///  A function <see cref="ItemTransform"/> over <paramref name="text"/>.
    /// </returns>
    public static ItemTransform Function(StringSegment text, int nameLength, int argsStart, int argsLength)
        => new(text, nameLength, argsStart, argsLength);

    /// <summary>
    ///  Gets the transform function name, or a null segment (one whose <see cref="StringSegment.HasValue"/>
    ///  is <see langword="false"/>) if this is a quoted transform.
    /// </summary>
    public StringSegment FunctionName
        => _nameLength >= 0 ? Text.Slice(0, _nameLength) : default;

    /// <summary>
    ///  Gets the transform function arguments, or a null segment if there are none.
    /// </summary>
    public StringSegment FunctionArguments
        => _argsLength > 0 ? Text.Slice(_argsStart, _argsLength) : default;

    /// <summary>
    ///  Returns the transform's text.
    /// </summary>
    /// <returns>
    ///  The transform's backing segment as a string.
    /// </returns>
    public override string ToString()
        => Text.ToString();
}
