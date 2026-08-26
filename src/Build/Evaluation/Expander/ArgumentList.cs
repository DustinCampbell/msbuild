// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides allocation-free access to packed argument ranges.
/// </summary>
internal readonly struct ArgumentList
{
    private readonly string? _buffer;
    private readonly OneOrMany<StringSegmentRange> _arguments;

    internal ArgumentList(string? buffer, OneOrMany<StringSegmentRange> arguments)
    {
        _buffer = buffer;
        _arguments = arguments;
    }

    /// <summary>
    ///  Gets the number of arguments.
    /// </summary>
    public int Count
        => _arguments.Count;

    /// <summary>
    ///  Gets the argument at the specified index.
    /// </summary>
    public StringSegment this[int index]
        => _arguments[index].ToSegment(_buffer);

    /// <summary>
    ///  Returns an allocation-free enumerator over the arguments.
    /// </summary>
    public Enumerator GetEnumerator()
        => new(_buffer, _arguments);

    /// <summary>
    ///  Enumerates the arguments.
    /// </summary>
    public struct Enumerator(string? buffer, OneOrMany<StringSegmentRange> arguments)
    {
        private OneOrMany<StringSegmentRange>.Enumerator _arguments = arguments.GetEnumerator();

        /// <summary>
        ///  Gets the current argument.
        /// </summary>
        public readonly StringSegment Current
            => _arguments.Current.ToSegment(buffer);

        /// <summary>
        ///  Advances to the next argument.
        /// </summary>
        public bool MoveNext()
            => _arguments.MoveNext();
    }
}
