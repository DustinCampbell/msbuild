// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Buffers;
#endif
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Indicates whether the specified character occurs within this segment.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool Contains(char value)
        => IndexOf(value) >= 0;

    /// <summary>
    ///  Indicates whether the specified substring occurs within this segment using the specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool Contains(string value, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, comparisonType) >= 0;

    /// <summary>
    ///  Indicates whether the characters in the specified span occur within this segment using an ordinal
    ///  comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool Contains(ReadOnlySpan<char> value)
        => AsSpan().IndexOf(value) >= 0;

    /// <summary>
    ///  Indicates whether the characters in the specified span occur within this segment using the specified
    ///  comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool Contains(ReadOnlySpan<char> value, StringComparison comparisonType)
        => IndexOf(value, comparisonType) >= 0;

    /// <summary>
    ///  Indicates whether the specified segment occurs within this segment using the specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="value"/> occurs within this segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool Contains(StringSegment value, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, comparisonType) >= 0;

    /// <summary>
    ///  Indicates whether any of the specified characters occurs within this segment.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <returns>
    ///  <see langword="true"/> if any of <paramref name="values"/> occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool ContainsAny(char[] values)
        => IndexOfAny(values) >= 0;

    /// <summary>
    ///  Indicates whether either of the two specified characters occurs within this segment.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <returns>
    ///  <see langword="true"/> if either character occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool ContainsAny(char value0, char value1)
        => IndexOfAny(value0, value1) >= 0;

    /// <summary>
    ///  Indicates whether any of the three specified characters occurs within this segment.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <param name="value2">The third character to find.</param>
    /// <returns>
    ///  <see langword="true"/> if any of the characters occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool ContainsAny(char value0, char value1, char value2)
        => IndexOfAny(value0, value1, value2) >= 0;

    /// <summary>
    ///  Indicates whether any of the characters in the specified span occurs within this segment. An empty
    ///  set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <returns>
    ///  <see langword="true"/> if any of <paramref name="values"/> occurs within the segment; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool ContainsAny(ReadOnlySpan<char> values)
        => IndexOfAny(values) >= 0;

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of a character.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(char value, int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        int result = -1;

        if (!IsNullOrEmpty)
        {
            result = Buffer.IndexOf(value, Offset + start, length);

            if (result >= 0)
            {
                result -= Offset;
            }
        }

        return result;
    }

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of a character.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(char value, int start)
        => IndexOf(value, start, Length - start);

    /// <summary>
    ///  Searches this segment for the first occurrence of a character.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(char value)
        => IndexOf(value, start: 0, Length);

    /// <summary>
    ///  Searches this segment for the first occurrence of a substring using the specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(string value, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, start: 0, Length, comparisonType);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of a substring
    ///  using the specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(string value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, start, Length - start, comparisonType);

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of a substring using the
    ///  specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(string value, int start, int length, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.NotNull(value);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        int result = -1;

        if (HasValue)
        {
            if (value.Length == 2 && comparisonType == StringComparison.Ordinal)
            {
                return IndexOfTwoCharacters(value[0], value[1], start, length);
            }

            result = Buffer.IndexOf(value, Offset + start, length, comparisonType);

            if (result >= 0)
            {
                result -= Offset;
            }
        }
        else
        {
            ValidateComparisonType(comparisonType);
        }

        return result;
    }

    /// <summary>
    ///  Searches this segment for the first occurrence of the characters in the specified span using an
    ///  ordinal comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(ReadOnlySpan<char> value)
        => AsSpan().IndexOf(value);

    /// <summary>
    ///  Searches this segment for the first occurrence of the characters in the specified span using the
    ///  specified comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(ReadOnlySpan<char> value, StringComparison comparisonType)
        => IndexOf(value, start: 0, Length, comparisonType);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of the characters
    ///  in the specified span using the specified comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(ReadOnlySpan<char> value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, start, Length - start, comparisonType);

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of the characters in the
    ///  specified span using the specified comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(
        ReadOnlySpan<char> value,
        int start,
        int length,
        StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        ReadOnlySpan<char> searchSpace = AsSpan(start, length);
        int result = searchSpace.IndexOf(value, comparisonType);

        return result < 0 ? -1 : result + start;
    }

    /// <summary>
    ///  Searches this segment for the first occurrence of another segment using the specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of this segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(StringSegment value, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, start: 0, Length, comparisonType);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of another segment
    ///  using the specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="start">The zero-based index, relative to the start of this segment, at which the search begins.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of this segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(StringSegment value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        => IndexOf(value, start, Length - start, comparisonType);

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of another segment using the
    ///  specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="start">The zero-based index, relative to the start of this segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence, relative to the start of this segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int IndexOf(StringSegment value, int start, int length, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.True(value.HasValue);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        if (value.Length == 2 && comparisonType == StringComparison.Ordinal)
        {
            return IndexOfTwoCharacters(value[0], value[1], start, length);
        }

        return IndexOf(value.Value, start, length, comparisonType);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int IndexOfTwoCharacters(char first, char second, int start, int length)
    {
        if (length < 2 || IsNullOrEmpty)
        {
            return -1;
        }

        int searchIndex = Offset + start;
        int remainingStarts = length - 1;

        while (remainingStarts > 0)
        {
            int firstIndex = Buffer.IndexOf(first, searchIndex, remainingStarts);
            if (firstIndex < 0)
            {
                return -1;
            }

            if (Buffer[firstIndex + 1] == second)
            {
                return firstIndex - Offset;
            }

            int consumed = firstIndex - searchIndex + 1;
            searchIndex = firstIndex + 1;
            remainingStarts -= consumed;
        }

        return -1;
    }

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of any of the specified
    ///  characters.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOfAny(char[] values, int start, int length)
    {
        Assumed.NotNull(values);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        int result = -1;

        if (!IsNullOrEmpty)
        {
            result = Buffer.IndexOfAny(values, Offset + start, length);

            if (result >= 0)
            {
                result -= Offset;
            }
        }

        return result;
    }

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of any of the
    ///  specified characters.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(char[] values, int start)
        => IndexOfAny(values, start, Length - start);

    /// <summary>
    ///  Searches this segment for the first occurrence of any of the specified characters.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(char[] values)
        => IndexOfAny(values, start: 0, Length);

    /// <summary>
    ///  Searches this segment for the first occurrence of either of the two specified characters.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of either character, relative to the start of the
    ///  segment, or <c>-1</c> if neither is found.
    /// </returns>
    public int IndexOfAny(char value0, char value1)
#if NET
        => AsSpan().IndexOfAny(value0, value1);
#else
        => IndexOfAnyWithArray(value0, value1, value1);
#endif

    /// <summary>
    ///  Searches this segment for the first occurrence of any of the three specified characters.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <param name="value2">The third character to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of the characters, relative to the start of the
    ///  segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(char value0, char value1, char value2)
#if NET
        => AsSpan().IndexOfAny(value0, value1, value2);
#else
        => IndexOfAnyWithArray(value0, value1, value2);
#endif

    /// <summary>
    ///  Searches this segment for the first occurrence of any of the characters in the specified span. An
    ///  empty set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(ReadOnlySpan<char> values)
        // The dedicated two/three-char overloads are more specialized (and faster) than the general
        // span-of-values search, so decompose small sets and only fall back to the latter beyond three.
        => values.Length switch
        {
            0 => -1,
            1 => IndexOf(values[0]),
            2 => IndexOfAny(values[0], values[1]),
            3 => IndexOfAny(values[0], values[1], values[2]),
            _ => AsSpan().IndexOfAny(values),
        };

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of any of the
    ///  characters in the specified span. An empty set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(ReadOnlySpan<char> values, int start)
    {
        Assumed.PositiveOrZero(start);
        Assumed.LessThanOrEqual(start, Length);

        switch (values.Length)
        {
            // An empty set never matches. This case is handled explicitly because span.IndexOfAny over an
            // empty set of values does not consistently return -1 across target frameworks.
            case 0:
                return -1;

            // The single-character case already returns a segment-relative index, so return it directly.
            case 1:
                return IndexOf(values[0], start);

            // For larger sets, search the span from start and re-base the result onto the segment.
            default:
                int relativeIndex = AsSpan(start).IndexOfAny(values);
                return relativeIndex < 0 ? -1 : relativeIndex + start;
        }
    }

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of any of the characters in
    ///  the specified span. An empty set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(ReadOnlySpan<char> values, int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        if (values.IsEmpty)
        {
            return -1;
        }

        int relativeIndex = values.Length == 1
            ? AsSpan(start, length).IndexOf(values[0])
            : AsSpan(start, length).IndexOfAny(values);

        return relativeIndex < 0 ? -1 : relativeIndex + start;
    }

#if NET
    /// <summary>
    ///  Searches this segment for the first occurrence of any of the characters in the specified search values.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(SearchValues<char> values)
        => AsSpan().IndexOfAny(values);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the first occurrence of any character
    ///  in the specified search values.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any character in <paramref name="values"/>, relative
    ///  to the start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(SearchValues<char> values, int start)
        => IndexOfAny(values, start, Length - start);

    /// <summary>
    ///  Searches the specified range of this segment for the first occurrence of any character in the
    ///  specified search values.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any character in <paramref name="values"/>, relative
    ///  to the start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int IndexOfAny(SearchValues<char> values, int start, int length)
    {
        Assumed.NotNull(values);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        int relativeIndex = AsSpan(start, length).IndexOfAny(values);
        return relativeIndex < 0 ? -1 : relativeIndex + start;
    }

#endif

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of a character.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if not found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOf(char value, int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);

        if (length == 0)
        {
            Assumed.LessThanOrEqual(start, Length);
            return -1;
        }

        Assumed.LessThan(start, Length);
        Assumed.LessThanOrEqual(length, start + 1);

        int result = -1;

        if (!IsNullOrEmpty)
        {
            result = Buffer.LastIndexOf(value, Offset + start, length);

            if (result >= 0)
            {
                result -= Offset;
            }
        }

        return result;
    }

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of a character.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if not found.
    /// </returns>
    public int LastIndexOf(char value, int start)
        => LastIndexOf(value, start, start + 1); // Include every character from start back to 0.

    /// <summary>
    ///  Searches this segment for the last occurrence of a character.
    /// </summary>
    /// <param name="value">The character to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if not found.
    /// </returns>
    public int LastIndexOf(char value)
        => Length > 0 ? LastIndexOf(value, Length - 1, Length) : -1;

    /// <summary>
    ///  Searches this segment for the last occurrence of a substring using the specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found. If <paramref name="value"/> is empty, returns <see cref="Length"/> for a valued segment.
    /// </returns>
    public int LastIndexOf(string value, StringComparison comparisonType = StringComparison.Ordinal)
        => LastIndexOf(value, Length, Length + 1, comparisonType);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of a substring
    ///  using the specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found. If <paramref name="value"/> is empty, returns the segment-relative insertion point
    ///  immediately after <paramref name="start"/>, capped at <see cref="Length"/>.
    /// </returns>
    public int LastIndexOf(string value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        => LastIndexOf(value, start, start + 1, comparisonType); // Include every character from start back to 0.

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of a substring using the
    ///  specified comparison.
    /// </summary>
    /// <param name="value">The string to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the strings are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found. If <paramref name="value"/> is empty, returns the segment-relative insertion point
    ///  immediately after <paramref name="start"/>, capped at <see cref="Length"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOf(string value, int start, int length, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.NotNull(value);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start, Length);
        Assumed.LessThanOrEqual(length, start + 1);

        if (value.Length == 0)
        {
            // .NET Framework returns start for an empty value, while modern string and span searches return
            // the insertion point after start. Keep the overloads consistent across target frameworks.
            ValidateComparisonType(comparisonType);
            return HasValue ? Math.Min(start + 1, Length) : -1;
        }

        if (IsNullOrEmpty || length == 0)
        {
            ValidateComparisonType(comparisonType);
            return -1;
        }

        int searchStart = start == Length ? Length - 1 : start;
        int searchLength = start == Length ? Math.Min(length - 1, Length) : length;
        int result = Buffer.LastIndexOf(value, Offset + searchStart, searchLength, comparisonType);

        return result < 0 ? -1 : result - Offset;
    }

    /// <summary>
    ///  Searches this segment for the last occurrence of the characters in the specified span using an
    ///  ordinal comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if not found.
    /// </returns>
    public int LastIndexOf(ReadOnlySpan<char> value)
        => AsSpan().LastIndexOf(value);

    /// <summary>
    ///  Searches this segment for the last occurrence of the characters in the specified span using the
    ///  specified comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int LastIndexOf(ReadOnlySpan<char> value, StringComparison comparisonType)
        => LastIndexOf(value, Length, Length + 1, comparisonType);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of the characters
    ///  in the specified span using the specified comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int LastIndexOf(ReadOnlySpan<char> value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        => LastIndexOf(value, start, start + 1, comparisonType);

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of the characters in the
    ///  specified span using the specified comparison.
    /// </summary>
    /// <param name="value">The sequence of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of the segment, or <c>-1</c> if
    ///  not found.
    /// </returns>
    public int LastIndexOf(ReadOnlySpan<char> value, int start, int length, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start, Length);
        Assumed.LessThanOrEqual(length, start + 1);

        if (value.IsEmpty)
        {
            ValidateComparisonType(comparisonType);
            return Math.Min(start + 1, Length);
        }

        if (Length == 0 || length == 0)
        {
            ValidateComparisonType(comparisonType);
            return -1;
        }

        int searchStart = start == Length ? Length - 1 : start;
        int searchLength = start == Length ? Math.Min(length - 1, Length) : length;
        int rangeStart = searchStart - searchLength + 1;
        ReadOnlySpan<char> searchSpace = AsSpan(rangeStart, searchLength);
#if NET
        int result = searchSpace.LastIndexOf(value, comparisonType);
#else
        int result;

        if (comparisonType == StringComparison.Ordinal)
        {
            result = searchSpace.LastIndexOf(value);
        }
        else
        {
            // System.Memory has no comparison-aware LastIndexOf. Repeated IndexOf calls preserve
            // culture-sensitive and overlapping-match semantics without materializing either span.
            result = -1;
            int searchOffset = 0;

            while (searchOffset <= searchSpace.Length)
            {
                int match = searchSpace[searchOffset..].IndexOf(value, comparisonType);

                if (match < 0)
                {
                    break;
                }

                result = searchOffset + match;

                if (result == searchSpace.Length)
                {
                    break;
                }

                searchOffset = result + 1;
            }
        }
#endif

        return result < 0 ? -1 : result + rangeStart;
    }

    /// <summary>
    ///  Searches this segment for the last occurrence of another segment using the specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of this segment, or <c>-1</c> if
    ///  not found. If <paramref name="value"/> is empty, returns <see cref="Length"/> for a valued segment.
    /// </returns>
    public int LastIndexOf(StringSegment value, StringComparison comparisonType = StringComparison.Ordinal)
        => LastIndexOf(value, Length, Length + 1, comparisonType);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of another segment
    ///  using the specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="start">The zero-based index, relative to the start of this segment, at which the search begins.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of this segment, or <c>-1</c> if
    ///  not found. If <paramref name="value"/> is empty, returns the segment-relative insertion point
    ///  immediately after <paramref name="start"/>, capped at <see cref="Length"/>.
    /// </returns>
    public int LastIndexOf(StringSegment value, int start, StringComparison comparisonType = StringComparison.Ordinal)
        => LastIndexOf(value, start, start + 1, comparisonType); // Include every character from start back to 0.

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of another segment using the
    ///  specified comparison.
    /// </summary>
    /// <param name="value">The segment to find.</param>
    /// <param name="start">The zero-based index, relative to the start of this segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence, relative to the start of this segment, or <c>-1</c> if
    ///  not found. If <paramref name="value"/> is empty, returns the segment-relative insertion point
    ///  immediately after <paramref name="start"/>, capped at <see cref="Length"/>.
    /// </returns>
    public int LastIndexOf(StringSegment value, int start, int length, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.True(value.HasValue);

        return LastIndexOf(value.Value, start, length, comparisonType);
    }

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of any of the specified
    ///  characters.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int LastIndexOfAny(char[] values, int start, int length)
    {
        Assumed.NotNull(values);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);

        if (length == 0)
        {
            Assumed.LessThanOrEqual(start, Length);
            return -1;
        }

        Assumed.LessThan(start, Length);
        Assumed.LessThanOrEqual(length, start + 1);

        int result = -1;

        if (!IsNullOrEmpty)
        {
            result = Buffer.LastIndexOfAny(values, Offset + start, length);

            if (result >= 0)
            {
                result -= Offset;
            }
        }

        return result;
    }

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of any of the
    ///  specified characters.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(char[] values, int start)
        => LastIndexOfAny(values, start, length: start + 1); // Include every character from start back to 0.

    /// <summary>
    ///  Searches this segment for the last occurrence of any of the specified characters.
    /// </summary>
    /// <param name="values">The characters to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(char[] values)
        => Length > 0 ? LastIndexOfAny(values, Length - 1, Length) : -1;

    /// <summary>
    ///  Searches this segment for the last occurrence of either of the two specified characters.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of either character, relative to the start of the
    ///  segment, or <c>-1</c> if neither is found.
    /// </returns>
    public int LastIndexOfAny(char value0, char value1)
#if NET
        => AsSpan().LastIndexOfAny(value0, value1);
#else
        => LastIndexOfAnyWithArray(value0, value1, value1);
#endif

    /// <summary>
    ///  Searches this segment for the last occurrence of any of the three specified characters.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <param name="value2">The third character to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of the characters, relative to the start of the
    ///  segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(char value0, char value1, char value2)
#if NET
        => AsSpan().LastIndexOfAny(value0, value1, value2);
#else
        => LastIndexOfAnyWithArray(value0, value1, value2);
#endif

    /// <summary>
    ///  Searches this segment for the last occurrence of any of the characters in the specified span. An
    ///  empty set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(ReadOnlySpan<char> values)
        => values.Length switch
        {
            0 => -1,
            1 => LastIndexOf(values[0]),
            2 => LastIndexOfAny(values[0], values[1]),
            3 => LastIndexOfAny(values[0], values[1], values[2]),
            _ => AsSpan().LastIndexOfAny(values),
        };

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of any of the
    ///  characters in the specified span. An empty set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(ReadOnlySpan<char> values, int start)
        => LastIndexOfAny(values, start, start + 1);

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of any of the characters in
    ///  the specified span. An empty set never matches.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(ReadOnlySpan<char> values, int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);

        if (length == 0)
        {
            Assumed.LessThanOrEqual(start, Length);
            return -1;
        }

        Assumed.LessThan(start, Length);
        Assumed.LessThanOrEqual(length, start + 1);

        if (values.IsEmpty)
        {
            return -1;
        }

        int rangeStart = start - length + 1;
        ReadOnlySpan<char> searchSpace = AsSpan(rangeStart, length);
        int relativeIndex = values.Length == 1
            ? searchSpace.LastIndexOf(values[0])
            : searchSpace.LastIndexOfAny(values);

        return relativeIndex < 0 ? -1 : relativeIndex + rangeStart;
    }

#if NET
    /// <summary>
    ///  Searches this segment for the last occurrence of any of the characters in the specified search values.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of <paramref name="values"/>, relative to the
    ///  start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(SearchValues<char> values)
        => AsSpan().LastIndexOfAny(values);

    /// <summary>
    ///  Searches this segment, beginning at the specified index, for the last occurrence of any character in
    ///  the specified search values.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any character in <paramref name="values"/>, relative
    ///  to the start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(SearchValues<char> values, int start)
        => LastIndexOfAny(values, start, start + 1);

    /// <summary>
    ///  Searches the specified range of this segment for the last occurrence of any character in the
    ///  specified search values.
    /// </summary>
    /// <param name="values">The set of characters to find.</param>
    /// <param name="start">The zero-based index, relative to the start of the segment, at which the search begins.</param>
    /// <param name="length">The number of characters to search.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any character in <paramref name="values"/>, relative
    ///  to the start of the segment, or <c>-1</c> if none is found.
    /// </returns>
    public int LastIndexOfAny(SearchValues<char> values, int start, int length)
    {
        Assumed.NotNull(values);
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);

        if (length == 0)
        {
            Assumed.LessThanOrEqual(start, Length);
            return -1;
        }

        Assumed.LessThan(start, Length);
        Assumed.LessThanOrEqual(length, start + 1);

        int rangeStart = start - length + 1;
        int relativeIndex = AsSpan(rangeStart, length).LastIndexOfAny(values);
        return relativeIndex < 0 ? -1 : relativeIndex + rangeStart;
    }

#endif

#if !NET
    // Reused per-thread scratch buffer that lets the two/three-char search overloads call the native
    // string.IndexOfAny/LastIndexOfAny (faster than the portable span scan on .NET Framework) without
    // allocating a char[] per call. Two-char callers duplicate the final character, which is harmless
    // because searching for the same character twice yields the same result.
    [ThreadStatic]
    private static char[]? t_searchChars;

    /// <summary>
    ///  Searches this segment for the first occurrence of any of the three specified characters using the
    ///  native <see cref="string.IndexOfAny(char[], int, int)"/> and a reused per-thread scratch buffer.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <param name="value2">The third character to find.</param>
    /// <returns>
    ///  The zero-based index of the first occurrence of any of the characters, relative to the start of the
    ///  segment, or <c>-1</c> if none is found.
    /// </returns>
    private int IndexOfAnyWithArray(char value0, char value1, char value2)
    {
        int result = -1;

        if (!IsNullOrEmpty)
        {
            char[] searchChars = t_searchChars ??= new char[3];
            searchChars[0] = value0;
            searchChars[1] = value1;
            searchChars[2] = value2;

            result = Buffer.IndexOfAny(searchChars, Offset, Length);

            if (result >= 0)
            {
                result -= Offset;
            }
        }

        return result;
    }

    /// <summary>
    ///  Searches this segment for the last occurrence of any of the three specified characters using the
    ///  native <see cref="string.LastIndexOfAny(char[], int, int)"/> and a reused per-thread scratch buffer.
    /// </summary>
    /// <param name="value0">The first character to find.</param>
    /// <param name="value1">The second character to find.</param>
    /// <param name="value2">The third character to find.</param>
    /// <returns>
    ///  The zero-based index of the last occurrence of any of the characters, relative to the start of the
    ///  segment, or <c>-1</c> if none is found.
    /// </returns>
    private int LastIndexOfAnyWithArray(char value0, char value1, char value2)
    {
        int result = -1;

        if (!IsNullOrEmpty)
        {
            char[] searchChars = t_searchChars ??= new char[3];
            searchChars[0] = value0;
            searchChars[1] = value1;
            searchChars[2] = value2;

            result = Buffer.LastIndexOfAny(searchChars, Offset + Length - 1, Length);

            if (result >= 0)
            {
                result -= Offset;
            }
        }

        return result;
    }
#endif
}
