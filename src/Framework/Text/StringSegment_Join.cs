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

        int total = values.Length - 1;
        foreach (StringSegment value in values)
        {
            total += value.Length;
        }

        if (total == 0)
        {
            return string.Empty;
        }

#if NET
        return JoinCore(new ReadOnlySpan<char>(in separator), values);
#else
        string result = string.FastAllocateString(total);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, total);
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

        switch (separator)
        {
            case [char c]:
                return Join(c, values);
            case null or []:
                return JoinCore_NoSeparator(values);
        }

        int total = separator.Length * (values.Length - 1);
        foreach (StringSegment value in values)
        {
            total += value.Length;
        }

        if (total == 0)
        {
            return string.Empty;
        }

#if NET
        return JoinCore(separator.AsSpan(), values);
#else
        string result = string.FastAllocateString(total);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, total);
                ReadOnlySpan<char> separatorSpan = separator.AsSpan();
                StringSegment first = values[0];
                first.CopyTo(destination);
                int position = first.Length;

                for (int i = 1; i < values.Length; i++)
                {
                    separatorSpan.CopyTo(destination[position..]);
                    position += separatorSpan.Length;

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
    /// <param name="separator">The string to insert between each segment.</param>
    /// <param name="values">The segments to concatenate.</param>
    /// <returns>
    ///  A string consisting of the segments separated by <paramref name="separator"/>, or
    ///  <see cref="string.Empty"/> if <paramref name="values"/> is empty.
    /// </returns>
    private static string JoinCore(ReadOnlySpan<char> separator, ReadOnlySpan<StringSegment> values)
    {
        int total = separator.Length * (values.Length - 1);
        foreach (StringSegment value in values)
        {
            total += value.Length;
        }

        if (total == 0)
        {
            return string.Empty;
        }

#if NET
        return string.Create(total, new JoinCoreState(separator, values), static (span, state) =>
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
        string result = string.FastAllocateString(total);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, total);
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
        int total = 0;
        foreach (StringSegment value in values)
        {
            total += value.Length;
        }

        if (total == 0)
        {
            return string.Empty;
        }

#if NET
        return string.Create(total, values, static (span, values) =>
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
        string result = string.FastAllocateString(total);

        unsafe
        {
            fixed (char* ptr = result)
            {
                Span<char> destination = new(ptr, total);
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
        StringBuilder builder = StringBuilderCache.Acquire();

        builder.AppendSegment(first);

        while (enumerator.MoveNext())
        {
            StringSegment value = enumerator.Current;
            builder.Append(separator);
            builder.AppendSegment(value);
        }

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

        StringBuilder builder = StringBuilderCache.Acquire();
        builder.AppendSegment(enumerator.Current);

        while (enumerator.MoveNext())
        {
            builder.Append(separator);
            builder.AppendSegment(enumerator.Current);
        }

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

        StringBuilder builder = StringBuilderCache.Acquire();
        builder.AppendSegment(enumerator.Current);

        while (enumerator.MoveNext())
        {
            builder.AppendSegment(enumerator.Current);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }
}
