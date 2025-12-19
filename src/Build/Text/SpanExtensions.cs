// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Text;

/// <summary>
///  Span extensions for common operations.
/// </summary>
internal static partial class SpanExtensions
{
    /// <summary>
    ///  Replaces all occurrences of <paramref name="oldValue"/> with <paramref name="newValue"/>.
    /// </summary>
    /// <param name="span">The span in which the elements should be replaced.</param>
    /// <param name="oldValue">The value to be replaced with <paramref name="newValue"/>.</param>
    /// <param name="newValue">The value to replace all occurrences of <paramref name="oldValue"/>.</param>
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
    /// <typeparam name="T">The type of the span and values.</typeparam>
    /// <param name="span">The span to search.</param>
    /// <param name="value">A value to avoid.</param>
    /// <remarks>
    ///  .NET Framework extension to match .NET functionality.
    /// </remarks>
    /// <returns>
    ///  The index in the span of the first occurrence of any value other than <paramref name="value"/>.
    ///  If all of the values are <paramref name="value"/>, returns -1.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfAnyExcept<T>(this ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (!span[i].Equals(value))
            {
                return i;
            }
        }

        return -1;
    }

    extension(ReadOnlySpan<char> span)
    {
        /// <summary>
        ///  Slice the given span at null, if present.
        /// </summary>
        public ReadOnlySpan<char> SliceAtNull()
        {
            int index = span.IndexOf('\0');
            return index == -1 ? span : span[..index];
        }
    }

    extension(Span<char> span)
    {
        /// <summary>
        ///  Slice the given span at null, if present.
        /// </summary>
        public Span<char> SliceAtNull()
        {
            int index = span.IndexOf((char)0);
            return index == -1 ? span : span[..index];
        }
    }

    extension(ReadOnlySpan<char> span1)
    {
        /// <summary>
        ///  Returns the exact comparison of two spans as if they were strings, including embedded nulls.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Use this method when you want the exact same result you would get from ordinally comparing two strings
        ///   that have the same content.
        ///  </para>
        /// </remarks>
        public int CompareOrdinalAsString(ReadOnlySpan<char> span2)
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
}
