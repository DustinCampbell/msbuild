// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.Text;

/// <summary>
///  Extension methods for creating <see cref="StringSegment"/> views over a <see cref="string"/>,
///  mirroring the <c>AsSpan</c> and <c>AsMemory</c> extension methods in the base class library.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    ///  Creates a <see cref="StringSegment"/> that views the entirety of <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The string to view, or <see langword="null"/> to create a null segment.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSegment AsSegment(this string text)
        => new(text);

    /// <summary>
    ///  Creates a <see cref="StringSegment"/> that views the portion of <paramref name="text"/> beginning at
    ///  <paramref name="start"/> and continuing to the end of the string.
    /// </summary>
    /// <param name="text">The string to view.</param>
    /// <param name="start">The index in <paramref name="text"/> at which the segment begins.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSegment AsSegment(this string text, int start)
        => new(text, start, text.Length - start);

    /// <summary>
    ///  Creates a <see cref="StringSegment"/> that views the region of <paramref name="text"/> beginning at
    ///  <paramref name="start"/> and spanning <paramref name="length"/> characters.
    /// </summary>
    /// <param name="text">The string to view.</param>
    /// <param name="start">The index in <paramref name="text"/> at which the segment begins.</param>
    /// <param name="length">The number of characters in the segment.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static StringSegment AsSegment(this string text, int start, int length)
        => new(text, start, length);
}
