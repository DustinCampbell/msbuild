// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Polyfills for Span<T> APIs that were added in newer .NET versions:
//   - Span<char>.Replace(char, char) — .NET 8+
//   - ReadOnlySpan<T>.IndexOfAnyExcept<T>(T) — .NET 7+
//
// Lives in the System namespace alongside MemoryExtensions so callers can use
// the methods without an extra using.

#if !NET
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#endif

namespace System;

internal static partial class SpanExtensions
{
#if !NET
    /// <summary>
    ///  Replaces all occurrences of <paramref name="oldValue"/> with <paramref name="newValue"/> in
    ///  <paramref name="span"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Replace(this Span<char> span, char oldValue, char newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        fixed (char* p = span)
        {
            char* ptr = p;
            char* end = p + span.Length;

            while (ptr < end)
            {
                if (*ptr == oldValue)
                {
                    *ptr = newValue;
                }

                ptr++;
            }
        }
    }

    /// <summary>
    ///  Searches for the first index of any value other than the specified <paramref name="value"/>.
    /// </summary>
    /// <returns>
    ///  The index of the first element that is not equal to <paramref name="value"/>, or -1 if every element equals it.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfAnyExcept<T>(this ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < span.Length; i++)
        {
            if (!comparer.Equals(span[i], value))
            {
                return i;
            }
        }

        return -1;
    }
#endif

    /// <summary>
    ///  Returns the exact comparison of two spans as if they were strings, including embedded nulls.
    /// </summary>
    /// <remarks>
    ///  Use this method when you want the exact same result you would get from ordinally comparing two strings
    ///  that have the same content.
    /// </remarks>
    public static int CompareOrdinalAsString(this ReadOnlySpan<char> span1, ReadOnlySpan<char> span2)
    {
        int sharedLength = Math.Min(span1.Length, span2.Length);

        int result = span1[..sharedLength].SequenceCompareTo(span2[..sharedLength]);

        if (result != 0 || span1.Length == span2.Length)
        {
            // If the spans are equal and the same length, or we found a mismatch, return the result.
            return result;
        }

        // If we've fully matched the shared length, follow the logic string would do. If there is no shared length
        // or the shared length is odd, we return the next character in the longer span, inverted if it is from
        // the second span (effectively comparing to "null").
        return sharedLength != 0 && sharedLength % 2 == 0
            ? span1.Length - span2.Length
            : span1.Length > span2.Length ? span1[sharedLength] : -span2[sharedLength];
    }
}
