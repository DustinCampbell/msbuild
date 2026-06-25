// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Splits this segment on <paramref name="separator"/>, yielding each piece as a re-windowed
    ///  <see cref="StringSegment"/> without allocating an array or copying any characters.
    /// </summary>
    /// <param name="separator">The character that delimits the pieces.</param>
    /// <param name="options">Options that control whether empty entries are removed and whether entries are trimmed.</param>
    /// <returns>
    ///  An enumerator over the pieces of this segment.
    /// </returns>
    public SplitEnumerator Split(char separator, StringSplitOptions options = StringSplitOptions.None)
        => new(this, separator, options);

    /// <summary>
    ///  Splits this segment on any of <paramref name="separators"/>, yielding each piece as a re-windowed
    ///  <see cref="StringSegment"/> without allocating an array or copying any characters. An empty set
    ///  yields the whole segment as a single entry.
    /// </summary>
    /// <param name="separators">The set of characters that delimit the pieces.</param>
    /// <param name="options">Options that control whether empty entries are removed and whether entries are trimmed.</param>
    /// <returns>
    ///  An enumerator over the pieces of this segment.
    /// </returns>
    public SplitEnumerator Split(ReadOnlySpan<char> separators, StringSplitOptions options = StringSplitOptions.None)
        => new(this, separators, options);
}
