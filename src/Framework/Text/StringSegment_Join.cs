// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Concatenates the given segments, inserting <paramref name="separator"/> between each.
    /// </summary>
    /// <param name="separator">The character to insert between each segment.</param>
    /// <param name="values">The segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the segments separated by <paramref name="separator"/>, or
    ///  <see cref="string.Empty"/> if <paramref name="values"/> is empty.
    /// </returns>
    public static string Join(char separator, params ReadOnlySpan<StringSegment> values)
    {
        if (values.IsEmpty)
        {
            return string.Empty;
        }

        if (values.Length == 1)
        {
            // No separator or copy is needed; preserve the underlying string when the segment covers it entirely.
            return values[0].ValueOrEmpty;
        }

        // Compute the exact destination size once so JoinCore can allocate the result without another traversal.
        int length = values.Length - 1;
        foreach (StringSegment value in values)
        {
            length += value.Length;
        }

#if NET
        return JoinCore(new ReadOnlySpan<char>(in separator), values, length);
#else
        // Keep the character separator as a direct assignment on .NET Framework rather than using the
        // general span-copying path in JoinCore.
        string result = string.FastAllocateString(length);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, length);
                StringSegment first = values[0];
                first.CopyTo(destination);
                int position = first.Length;

                for (int i = 1; i < values.Length; i++)
                {
                    destination[position] = separator;
                    position++;

                    StringSegment value = values[i];
                    value.CopyTo(destination[position..]);
                    position += value.Length;
                }
            }
        }

        return result;
#endif
    }

    /// <summary>
    ///  Concatenates the given segments, inserting <paramref name="separator"/> between each.
    /// </summary>
    /// <param name="separator">The string to insert between each segment. May be <see langword="null"/> or empty.</param>
    /// <param name="values">The segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the segments separated by <paramref name="separator"/>, or
    ///  <see cref="string.Empty"/> if <paramref name="values"/> is empty.
    /// </returns>
    public static string Join(string? separator, params ReadOnlySpan<StringSegment> values)
    {
        if (values.IsEmpty)
        {
            return string.Empty;
        }

        if (values.Length == 1)
        {
            // No separator or copy is needed; preserve the underlying string when the segment covers it entirely.
            return values[0].ValueOrEmpty;
        }

        switch (separator)
        {
            case [char c]:
                return Join(c, values);
            case null or []:
                return JoinCore_NoSeparator(values);
        }

        // Compute the exact destination size once so JoinCore can allocate the result without another traversal.
        int length = separator.Length * (values.Length - 1);
        foreach (StringSegment value in values)
        {
            length += value.Length;
        }

        return JoinCore(separator.AsSpan(), values, length);
    }

    /// <summary>
    ///  Concatenates the given segments, inserting <paramref name="separator"/> between each.
    /// </summary>
    /// <param name="separator">The string to insert between each segment.</param>
    /// <param name="values">The segments to concatenate.</param>
    /// <param name="length">The total length of the resulting string.</param>
    /// <returns>
    ///  A string consisting of the segments separated by <paramref name="separator"/>, or
    ///  <see cref="string.Empty"/> if <paramref name="values"/> is empty.
    /// </returns>
    private static string JoinCore(ReadOnlySpan<char> separator, ReadOnlySpan<StringSegment> values, int length)
    {
        // Callers have already handled empty results and computed the exact destination length.
#if NET
        return string.Create(length, new JoinCoreState(separator, values), static (span, state) =>
        {
            var (separator, values) = state;

            StringSegment first = values[0];
            first.CopyTo(span);
            int position = first.Length;

            for (int i = 1; i < values.Length; i++)
            {
                separator.CopyTo(span[position..]);
                position += separator.Length;

                StringSegment value = values[i];
                value.CopyTo(span[position..]);
                position += value.Length;
            }
        });
#else
        // Note: The non-NET code path is needed because the string.Create polyfill can't use a ref struct as state.
        string result = string.FastAllocateString(length);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, length);
                StringSegment first = values[0];
                first.CopyTo(destination);
                int position = first.Length;

                for (int i = 1; i < values.Length; i++)
                {
                    separator.CopyTo(destination[position..]);
                    position += separator.Length;

                    StringSegment value = values[i];
                    value.CopyTo(destination[position..]);
                    position += value.Length;
                }
            }
        }

        return result;
#endif
    }

#if NET
    /// <summary>
    ///  Captures the separator and segments for the <see cref="string.Create{TState}"/> callback used by the
    ///  string-separated <see cref="Join(string, ReadOnlySpan{StringSegment})"/> overload.
    /// </summary>
    /// <param name="separator">The characters to insert between each segment.</param>
    /// <param name="values">The segments to concatenate.</param>
    private readonly ref struct JoinCoreState(ReadOnlySpan<char> separator, ReadOnlySpan<StringSegment> values)
    {
        private readonly ReadOnlySpan<char> _separator = separator;
        private readonly ReadOnlySpan<StringSegment> _values = values;

        /// <summary>
        ///  Deconstructs the captured state into its separator and segments.
        /// </summary>
        /// <param name="separator">When this method returns, contains the captured separator characters.</param>
        /// <param name="values">When this method returns, contains the captured segments.</param>
        public void Deconstruct(out ReadOnlySpan<char> separator, out ReadOnlySpan<StringSegment> values)
        {
            separator = _separator;
            values = _values;
        }
    }
#endif

    /// <summary>
    ///  Concatenates the given segments with no separator between them.
    /// </summary>
    /// <param name="values">The segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the concatenated segments, or <see cref="string.Empty"/>
    ///  if <paramref name="values"/> is empty.
    /// </returns>
    private static string JoinCore_NoSeparator(ReadOnlySpan<StringSegment> values)
    {
        if (values.Length == 1)
        {
            // No copy is needed; preserve the underlying string when the segment covers it entirely.
            return values[0].ValueOrEmpty;
        }

        // Compute the exact destination size once so the result can be allocated at its final length.
        int length = 0;
        foreach (StringSegment value in values)
        {
            length += value.Length;
        }

        // Unlike the separator overloads, multiple empty segments still produce an empty result.
        if (length == 0)
        {
            return string.Empty;
        }

#if NET
        return string.Create(length, values, static (span, values) =>
        {
            int position = 0;
            for (int i = 0; i < values.Length; i++)
            {
                StringSegment value = values[i];
                value.CopyTo(span[position..]);
                position += value.Length;
            }
        });
#else
        // Note: The non-NET code path is needed because the string.Create polyfill can't use a ref struct as state.
        string result = string.FastAllocateString(length);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, length);
                int position = 0;

                for (int i = 0; i < values.Length; i++)
                {
                    StringSegment value = values[i];
                    value.CopyTo(destination[position..]);
                    position += value.Length;
                }
            }
        }

        return result;
#endif
    }

    /// <summary>
    ///  Concatenates the given segments, inserting <paramref name="separator"/> between each.
    /// </summary>
    /// <param name="separator">The character to insert between each segment.</param>
    /// <param name="values">The sequence of segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the segments separated by <paramref name="separator"/>, or
    ///  <see cref="string.Empty"/> if <paramref name="values"/> is empty.
    /// </returns>
    public static string Join(char separator, IEnumerable<StringSegment> values)
    {
        Assumed.NotNull(values);

        if (values is StringSegment[] array)
        {
            return Join(separator, array.AsSpan());
        }

        if (values is ImmutableArray<StringSegment> immutableArray)
        {
            return Join(separator, immutableArray.AsSpan());
        }

        if (values.TryGetCount(out int count) && count == 0)
        {
            return string.Empty;
        }

        using IEnumerator<StringSegment> enumerator = values.GetEnumerator();

        // Avoid acquiring a StringBuilder for an empty sequence.
        if (!enumerator.MoveNext())
        {
            return string.Empty;
        }

        StringSegment first = enumerator.Current;

        if (!enumerator.MoveNext())
        {
            // Avoid acquiring and populating a StringBuilder when the sequence contains only one segment.
            return first.ValueOrEmpty;
        }

        StringBuilder builder = StringBuilderCache.Acquire();

        builder.AppendSegment(first);

        do
        {
            StringSegment value = enumerator.Current;
            builder.Append(separator);
            builder.AppendSegment(value);
        }
        while (enumerator.MoveNext());

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    /// <summary>
    ///  Concatenates the given segments, inserting <paramref name="separator"/> between each.
    /// </summary>
    /// <param name="separator">The string to insert between each segment. May be <see langword="null"/> or empty.</param>
    /// <param name="values">The sequence of segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the segments separated by <paramref name="separator"/>, or
    ///  <see cref="string.Empty"/> if <paramref name="values"/> is empty.
    /// </returns>
    public static string Join(string? separator, IEnumerable<StringSegment> values)
    {
        Assumed.NotNull(values);

        if (values is StringSegment[] array)
        {
            return Join(separator, array.AsSpan());
        }

        if (values is ImmutableArray<StringSegment> immutableArray)
        {
            return Join(separator, immutableArray.AsSpan());
        }

        switch (separator)
        {
            case [char c]:
                return Join(c, values);
            case null or []:
                return JoinCore_NoSeparator(values);
        }

        if (values.TryGetCount(out int count) && count == 0)
        {
            return string.Empty;
        }

        using IEnumerator<StringSegment> enumerator = values.GetEnumerator();

        // Avoid acquiring a StringBuilder for an empty sequence.
        if (!enumerator.MoveNext())
        {
            return string.Empty;
        }

        StringSegment first = enumerator.Current;

        if (!enumerator.MoveNext())
        {
            // Avoid acquiring and populating a StringBuilder when the sequence contains only one segment.
            return first.ValueOrEmpty;
        }

        StringBuilder builder = StringBuilderCache.Acquire();
        builder.AppendSegment(first);

        do
        {
            builder.Append(separator);
            builder.AppendSegment(enumerator.Current);
        }
        while (enumerator.MoveNext());

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    /// <summary>
    ///  Concatenates the given segments with no separator between them.
    /// </summary>
    /// <param name="values">The sequence of segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the concatenated segments, or <see cref="string.Empty"/>
    ///  if <paramref name="values"/> is empty.
    /// </returns>
    private static string JoinCore_NoSeparator(IEnumerable<StringSegment> values)
    {
        Assumed.NotNull(values);

        if (values.TryGetCount(out int count) && count == 0)
        {
            return string.Empty;
        }

        using IEnumerator<StringSegment> enumerator = values.GetEnumerator();

        // Avoid acquiring a StringBuilder for an empty sequence.
        if (!enumerator.MoveNext())
        {
            return string.Empty;
        }

        StringSegment first = enumerator.Current;

        if (!enumerator.MoveNext())
        {
            // Avoid acquiring and populating a StringBuilder when the sequence contains only one segment.
            return first.ValueOrEmpty;
        }

        StringBuilder builder = StringBuilderCache.Acquire();
        builder.AppendSegment(first);

        do
        {
            builder.AppendSegment(enumerator.Current);
        }
        while (enumerator.MoveNext());

        return StringBuilderCache.GetStringAndRelease(builder);
    }
}
