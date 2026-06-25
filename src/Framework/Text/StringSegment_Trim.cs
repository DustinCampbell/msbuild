// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Returns a segment with all leading and trailing white-space characters removed.
    /// </summary>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment Trim()
        => TrimWhiteSpace(front: true, back: true);

    /// <summary>
    ///  Returns a segment with all leading white-space characters removed.
    /// </summary>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment TrimStart()
        => TrimWhiteSpace(front: true, back: false);

    /// <summary>
    ///  Returns a segment with all trailing white-space characters removed.
    /// </summary>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment TrimEnd()
        => TrimWhiteSpace(front: false, back: true);

    /// <summary>
    ///  Returns a segment with all leading and trailing occurrences of the specified character removed.
    /// </summary>
    /// <param name="value">The character to remove.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment Trim(char value)
        => TrimChar(value, front: true, back: true);

    /// <summary>
    ///  Returns a segment with all leading occurrences of the specified character removed.
    /// </summary>
    /// <param name="value">The character to remove.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment TrimStart(char value)
        => TrimChar(value, front: true, back: false);

    /// <summary>
    ///  Returns a segment with all trailing occurrences of the specified character removed.
    /// </summary>
    /// <param name="value">The character to remove.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment TrimEnd(char value)
        => TrimChar(value, front: false, back: true);

    /// <summary>
    ///  Returns a segment with all leading and trailing occurrences of the specified characters removed.
    /// </summary>
    /// <param name="trimChars">The set of characters to remove. An empty set removes nothing.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment Trim(params ReadOnlySpan<char> trimChars)
        => TrimAny(trimChars, front: true, back: true);

    /// <summary>
    ///  Returns a segment with all leading occurrences of the specified characters removed.
    /// </summary>
    /// <param name="trimChars">The set of characters to remove. An empty set removes nothing.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment TrimStart(params ReadOnlySpan<char> trimChars)
        => TrimAny(trimChars, front: true, back: false);

    /// <summary>
    ///  Returns a segment with all trailing occurrences of the specified characters removed.
    /// </summary>
    /// <param name="trimChars">The set of characters to remove. An empty set removes nothing.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    public StringSegment TrimEnd(params ReadOnlySpan<char> trimChars)
        => TrimAny(trimChars, front: false, back: true);

    /// <summary>
    ///  Removes leading and/or trailing white-space characters.
    /// </summary>
    /// <param name="front">Whether to trim leading characters.</param>
    /// <param name="back">Whether to trim trailing characters.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    private StringSegment TrimWhiteSpace(bool front, bool back)
    {
        if (!HasValue)
        {
            return this;
        }

        int start = 0;
        int end = Length;

        if (front)
        {
            while (start < end && char.IsWhiteSpace(Buffer[Offset + start]))
            {
                start++;
            }
        }

        if (back)
        {
            while (end > start && char.IsWhiteSpace(Buffer[Offset + end - 1]))
            {
                end--;
            }
        }

        return new StringSegment(Buffer, Offset + start, end - start);
    }

    /// <summary>
    ///  Removes leading and/or trailing occurrences of a single character.
    /// </summary>
    /// <param name="value">The character to remove.</param>
    /// <param name="front">Whether to trim leading characters.</param>
    /// <param name="back">Whether to trim trailing characters.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    private StringSegment TrimChar(char value, bool front, bool back)
    {
        if (!HasValue)
        {
            return this;
        }

        int start = 0;
        int end = Length;

        if (front)
        {
            while (start < end && Buffer[Offset + start] == value)
            {
                start++;
            }
        }

        if (back)
        {
            while (end > start && Buffer[Offset + end - 1] == value)
            {
                end--;
            }
        }

        return new StringSegment(Buffer, Offset + start, end - start);
    }

    /// <summary>
    ///  Removes leading and/or trailing occurrences of any character in the specified set.
    /// </summary>
    /// <param name="trimChars">The set of characters to remove. An empty set removes nothing.</param>
    /// <param name="front">Whether to trim leading characters.</param>
    /// <param name="back">Whether to trim trailing characters.</param>
    /// <returns>
    ///  The trimmed segment, which shares the same backing buffer as this segment.
    /// </returns>
    private StringSegment TrimAny(ReadOnlySpan<char> trimChars, bool front, bool back)
    {
        // An empty trim set has nothing to remove. Unlike string.Trim(char[]), this does not fall back
        // to trimming whitespace; callers that want whitespace trimming use the parameterless overloads.
        if (!HasValue || trimChars.IsEmpty)
        {
            return this;
        }

        int start = 0;
        int end = Length;

#if NET
        ReadOnlySpan<char> span = AsSpan();

        if (front)
        {
            int firstKept = span.IndexOfAnyExcept(trimChars);

            // The entire segment consists of trim characters, so nothing remains.
            if (firstKept < 0)
            {
                return new StringSegment(Buffer, Offset + Length, length: 0);
            }

            start = firstKept;
        }

        if (back)
        {
            // If front trimming ran it already proved a kept character exists, so this is never -1 in
            // that case; when only back trimming runs, an all-trim segment yields end == 0 == start.
            end = span.LastIndexOfAnyExcept(trimChars) + 1;
        }
#else
        if (front)
        {
            while (start < end && IsInSet(trimChars, Buffer[Offset + start]))
            {
                start++;
            }
        }

        if (back)
        {
            while (end > start && IsInSet(trimChars, Buffer[Offset + end - 1]))
            {
                end--;
            }
        }
#endif

        return new StringSegment(Buffer, Offset + start, end - start);

#if !NET
        static bool IsInSet(ReadOnlySpan<char> set, char value)
        {
            foreach (char c in set)
            {
                if (c == value)
                {
                    return true;
                }
            }

            return false;
        }
#endif
    }
}
