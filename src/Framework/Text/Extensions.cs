// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Text;

/// <summary>
///  Extension methods for working with <see cref="StringSegment"/> values.
/// </summary>
internal static partial class Extensions
{
    extension(int)
    {
        /// <summary>
        ///  Converts an invariant integer represented by a <see cref="StringSegment"/> to a 32-bit signed integer.
        /// </summary>
        public static bool TryParse(StringSegment value, out int result)
#if NET
            => int.TryParse(value.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#else
            => TryParseInvariantInteger(value, out result) ||
               int.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#endif

        /// <summary>
        ///  Converts a number represented by a <see cref="StringSegment"/> to a 32-bit signed integer.
        /// </summary>
        public static bool TryParse(StringSegment value, NumberStyles style, IFormatProvider? provider, out int result)
#if NET
            => int.TryParse(value.AsSpan(), style, provider, out result);
#else
            => (style is NumberStyles.Integer or NumberStyles.None) && IsInvariantProvider(provider)
                ? TryParseInvariantInteger(value, style, out result)
                : int.TryParse(value.Value, style, provider, out result);
#endif
    }

    extension(long)
    {
        /// <summary>
        ///  Converts an invariant integer represented by a <see cref="StringSegment"/> to a 64-bit signed integer.
        /// </summary>
        public static bool TryParse(StringSegment value, out long result)
#if NET
            => long.TryParse(value.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#else
            => TryParseInvariantInteger(value, out result) ||
               long.TryParse(value.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
#endif

        /// <summary>
        ///  Converts a number represented by a <see cref="StringSegment"/> to a 64-bit signed integer.
        /// </summary>
        public static bool TryParse(StringSegment value, NumberStyles style, IFormatProvider? provider, out long result)
#if NET
            => long.TryParse(value.AsSpan(), style, provider, out result);
#else
            => style == NumberStyles.Integer && IsInvariantProvider(provider)
                ? long.TryParse(value, out result)
                : long.TryParse(value.Value, style, provider, out result);
#endif
    }

    extension(uint)
    {
        /// <summary>
        ///  Converts a number represented by a <see cref="StringSegment"/> to a 32-bit unsigned integer.
        /// </summary>
        public static bool TryParse(StringSegment value, NumberStyles style, IFormatProvider? provider, out uint result)
#if NET
            => uint.TryParse(value.AsSpan(), style, provider, out result);
#else
            => (style == NumberStyles.Integer && TryParseUnsignedIntegerDigits(value, out result))
                || uint.TryParse(value.Value, style, provider, out result);
#endif
    }

    extension(double)
    {
        /// <summary>
        ///  Converts a number represented by a <see cref="StringSegment"/> to a double-precision floating-point
        ///  number.
        /// </summary>
        public static bool TryParse(StringSegment value, NumberStyles style, IFormatProvider? provider, out double result)
#if NET
            => double.TryParse(value.AsSpan(), style, provider, out result);
#else
            => (style == (NumberStyles.Number | NumberStyles.Float)
                    && IsInvariantProvider(provider)
                    && TryParseInvariantDouble(value, out result))
                || double.TryParse(value.Value, style, provider, out result);
#endif
    }

    extension(Version)
    {
        /// <summary>
        ///  Converts a version represented by a <see cref="StringSegment"/> to a <see cref="Version"/>.
        /// </summary>
        public static bool TryParse(StringSegment value, [NotNullWhen(true)] out Version? result)
#if NET
            => Version.TryParse(value.AsSpan(), out result);
#else
            => TryParseVersion(value, out result);
#endif
    }

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

    /// <summary>
    ///  Interns the characters of <paramref name="segment"/> into a <see cref="string"/>, or returns
    ///  <see langword="null"/> if the segment has no backing value.
    /// </summary>
    /// <param name="segment">The segment to realize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string? WeakInternOrNull(this StringSegment segment)
        => segment.HasValue ? Strings.WeakIntern(segment) : null;

    /// <summary>
    ///  Interns the characters of <paramref name="segment"/> into a <see cref="string"/>, returning
    ///  <see cref="string.Empty"/> if the segment has no backing value.
    /// </summary>
    /// <param name="segment">The segment to realize.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string WeakIntern(this StringSegment segment)
        => Strings.WeakIntern(segment);

    /// <summary>
    ///  Appends the contents of a <see cref="StringSegment"/> to a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="segment">The segment whose contents should be appended.</param>
    /// <returns>
    ///  The <paramref name="builder"/> instance after the append operation.
    /// </returns>
    public static StringBuilder AppendSegment(this StringBuilder builder, StringSegment segment)
        => builder.Append(segment.Buffer, segment.Offset, segment.Length);
}
