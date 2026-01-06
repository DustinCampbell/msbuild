// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
///  Represents a span of text within a source string, tracking both the text content
///  and its position in the original source.
/// </summary>
internal readonly struct SourceSpan(int start, StringSegment text)
{
    private readonly int _start = start;
    private readonly StringSegment _text = text;

    /// <summary>
    ///  Gets the text content of this span.
    /// </summary>
    public StringSegment Text => _text;

    /// <summary>
    ///  Gets the starting position of this span in the original source.
    /// </summary>
    public int Start => _start;

    /// <summary>
    ///  Gets the ending position of this span in the original source.
    /// </summary>
    public int End => _start + _text.Length;

    /// <summary>
    ///  Gets the length of this span.
    /// </summary>
    public int Length => _text.Length;

    /// <summary>
    ///  Creates a new <see cref="SourceSpan"/> that is a slice of the current span
    ///  starting at the specified offset.
    /// </summary>
    /// <param name="start">The starting position within this span.</param>
    /// <returns>
    ///  A new <see cref="SourceSpan"/> representing the sliced portion.
    /// </returns>
    public SourceSpan Slice(int start)
        => new(_start + start, _text.Slice(start));

    /// <summary>
    ///  Creates a new <see cref="SourceSpan"/> that is a slice of the current span
    ///  with the specified start and length.
    /// </summary>
    /// <param name="start">The starting position within this span.</param>
    /// <param name="length">The length of the slice.</param>
    /// <returns>
    ///  A new <see cref="SourceSpan"/> representing the sliced portion.
    /// </returns>
    public SourceSpan Slice(int start, int length)
        => new(_start + start, _text.Slice(start, length));
}
