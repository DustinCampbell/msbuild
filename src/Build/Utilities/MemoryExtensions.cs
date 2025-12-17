// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET

using System;
using System.Diagnostics;

namespace Microsoft.Build.Utilities;

internal static class MemoryExtensions
{
    /// <summary>
    /// Removes all leading and trailing white-space characters from the memory.
    /// </summary>
    /// <param name="memory">The source memory from which the characters are removed.</param>
    public static Memory<char> Trim(this Memory<char> memory)
    {
        ReadOnlySpan<char> span = memory.Span;
        int start = ClampStart(span);
        int length = ClampEnd(span, start);

        return memory.Slice(start, length);
    }

    /// <summary>
    /// Removes all leading white-space characters from the memory.
    /// </summary>
    /// <param name="memory">The source memory from which the characters are removed.</param>
    public static Memory<char> TrimStart(this Memory<char> memory)
        => memory.Slice(ClampStart(memory.Span));

    /// <summary>
    /// Removes all trailing white-space characters from the memory.
    /// </summary>
    /// <param name="memory">The source memory from which the characters are removed.</param>
    public static Memory<char> TrimEnd(this Memory<char> memory)
        => memory.Slice(0, ClampEnd(memory.Span, 0));

    /// <summary>
    /// Removes all leading and trailing white-space characters from the memory.
    /// </summary>
    /// <param name="memory">The source memory from which the characters are removed.</param>
    public static ReadOnlyMemory<char> Trim(this ReadOnlyMemory<char> memory)
    {
        ReadOnlySpan<char> span = memory.Span;
        int start = ClampStart(span);
        int length = ClampEnd(span, start);
        return memory.Slice(start, length);
    }

    /// <summary>
    /// Removes all leading white-space characters from the memory.
    /// </summary>
    /// <param name="memory">The source memory from which the characters are removed.</param>
    public static ReadOnlyMemory<char> TrimStart(this ReadOnlyMemory<char> memory)
        => memory.Slice(ClampStart(memory.Span));

    /// <summary>
    /// Removes all trailing white-space characters from the memory.
    /// </summary>
    /// <param name="memory">The source memory from which the characters are removed.</param>
    public static ReadOnlyMemory<char> TrimEnd(this ReadOnlyMemory<char> memory)
        => memory.Slice(0, ClampEnd(memory.Span, 0));

    /// <summary>
    /// Delimits all leading occurrences of whitespace characters from the span.
    /// </summary>
    /// <param name="span">The source span from which the characters are removed.</param>
    private static int ClampStart(ReadOnlySpan<char> span)
    {
        int start = 0;

        for (; start < span.Length; start++)
        {
            if (!char.IsWhiteSpace(span[start]))
            {
                break;
            }
        }

        return start;
    }

    /// <summary>
    /// Delimits all trailing occurrences of whitespace characters from the span.
    /// </summary>
    /// <param name="span">The source span from which the characters are removed.</param>
    /// <param name="start">The start index from which to being searching.</param>
    private static int ClampEnd(ReadOnlySpan<char> span, int start)
    {
        // Initially, start==len==0. If ClampStart trims all, start==len
        Debug.Assert((uint)start <= span.Length);

        int end = span.Length - 1;

        for (; end >= start; end--)
        {
            if (!char.IsWhiteSpace(span[end]))
            {
                break;
            }
        }

        return end - start + 1;
    }
}

#endif
