// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Text;

/// <summary>
///  Enumerates the pieces produced by <see cref="StringSegment.Split(char, StringSplitOptions)"/> and its overloads.
///  Each <see cref="Current"/> is a view over the original buffer, so iteration is allocation-free.
/// </summary>
internal ref struct SplitEnumerator
{
    // StringSplitOptions.TrimEntries was introduced in .NET 5 and is absent from the .NET Framework enum,
    // so bridge it by value to keep a single Split implementation that honors trimming on both targets.
#if NET
    private const StringSplitOptions TrimEntriesOption = StringSplitOptions.TrimEntries;
#else
    private const StringSplitOptions TrimEntriesOption = (StringSplitOptions)2;
#endif

    private readonly StringSegment _segment;
    private readonly ReadOnlySpan<char> _separators;
    private readonly char _separator;
    private readonly bool _hasSingleSeparator;
    private readonly StringSplitOptions _options;
    private int _nextStart;
    private bool _done;
    private StringSegment _current;

    /// <summary>
    ///  Initializes a new enumerator that splits a segment on a single separator character.
    /// </summary>
    /// <param name="segment">The segment to split.</param>
    /// <param name="separator">The character that delimits the pieces.</param>
    /// <param name="options">Options that control empty-entry removal and entry trimming.</param>
    internal SplitEnumerator(StringSegment segment, char separator, StringSplitOptions options)
    {
        _segment = segment;
        _separators = default;
        _separator = separator;
        _hasSingleSeparator = true;
        _options = options;
        _nextStart = 0;
        _done = false;
        _current = default;
    }

    /// <summary>
    ///  Initializes a new enumerator that splits a segment on any of a set of separator characters.
    /// </summary>
    /// <param name="segment">The segment to split.</param>
    /// <param name="separators">The set of characters that delimit the pieces.</param>
    /// <param name="options">Options that control empty-entry removal and entry trimming.</param>
    internal SplitEnumerator(StringSegment segment, ReadOnlySpan<char> separators, StringSplitOptions options)
    {
        _segment = segment;
        _separators = separators;
        _separator = default;
        _hasSingleSeparator = false;
        _options = options;
        _nextStart = 0;
        _done = false;
        _current = default;
    }

    /// <summary>
    ///  Gets the piece at the current position of the enumerator.
    /// </summary>
    public readonly StringSegment Current => _current;

    /// <summary>
    ///  Returns this enumerator. Enables <c>foreach</c> iteration directly over the enumerator.
    /// </summary>
    /// <returns>This enumerator.</returns>
    public readonly SplitEnumerator GetEnumerator() => this;

    /// <summary>
    ///  Advances the enumerator to the next piece, skipping empty entries when
    ///  <see cref="StringSplitOptions.RemoveEmptyEntries"/> is set.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the enumerator advanced to another piece; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool MoveNext()
    {
        while (!_done)
        {
            int start = _nextStart;
            int separatorIndex = FindSeparator(start);

            StringSegment piece;
            if (separatorIndex < 0)
            {
                piece = _segment.Slice(start);
                _done = true;
            }
            else
            {
                piece = _segment.Slice(start, separatorIndex - start);
                _nextStart = separatorIndex + 1;
            }

            if ((_options & TrimEntriesOption) != 0)
            {
                piece = piece.Trim();
            }

            if ((_options & StringSplitOptions.RemoveEmptyEntries) != 0 && piece.Length == 0)
            {
                continue;
            }

            _current = piece;
            return true;
        }

        return false;
    }

    /// <summary>
    ///  Finds the next separator at or after the specified position.
    /// </summary>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which to begin searching.</param>
    /// <returns>
    ///  The zero-based index of the next separator, relative to the start of the segment, or <c>-1</c> if
    ///  no further separator is found.
    /// </returns>
    private readonly int FindSeparator(int start)
    {
        StringSegment remaining = _segment.Slice(start);

        int relativeIndex = _hasSingleSeparator
            ? remaining.IndexOf(_separator)
            : remaining.IndexOfAny(_separators);

        return relativeIndex < 0 ? -1 : relativeIndex + start;
    }
}
