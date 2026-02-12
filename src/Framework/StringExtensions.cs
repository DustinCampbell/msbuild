// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Text;
using Microsoft.Build.Framework;
#endif

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build;

internal static class StringExtensions
{
    /// <summary>
    ///  Indicates whether the specified string is <see langword="null"/> or an empty string ("").
    /// </summary>
    /// <param name="value">The string to test.</param>
    /// <returns>
    ///  <see langword="true"/> if the <paramref name="value"/> parameter is <see langword="null"/>
    ///  or an empty string (""); otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        => string.IsNullOrEmpty(value);

    /// <summary>
    ///  Indicates whether a specified string is <see langword="null"/>, empty, or consists only of white-space characters.
    /// </summary>
    /// <param name="value">The string to test.</param>
    /// <returns>
    ///  <see langword="true"/> if the <paramref name="value"/> parameter is <see langword="null"/> or empty,
    ///  or if <paramref name="value"/> consists exclusively of white-space characters.
    /// </returns>
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
        => string.IsNullOrWhiteSpace(value);

#if !NET
    public static string Replace(this string s, string oldValue, string? newValue, StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(oldValue);
        ArgumentException.ThrowIfNullOrEmpty(oldValue);

        newValue ??= string.Empty;

        int nextIndex = s.IndexOf(oldValue, comparisonType);
        if (nextIndex == -1)
        {
            return s;
        }

        int oldLength = oldValue.Length;
        int newLength = newValue.Length;

        // Assumes one match. Optimizes for replacing fallback property values (e.g. MSBuildExtensionsPath),
        // where an import usually references the fallback property once.
        // Reduces memory usage by half.
        StringBuilder builder = StringBuilderCache.Acquire(capacity: s.Length - oldLength + newLength);

        int startIndex = 0;

        do
        {
            int copyLength = nextIndex - startIndex;
            if (copyLength > 0)
            {
                builder.Append(s, startIndex, copyLength);
            }

            if (newLength > 0)
            {
                builder.Append(newValue);
            }

            startIndex = nextIndex + oldLength;
            nextIndex = s.IndexOf(oldValue, startIndex, comparisonType);
        }
        while (nextIndex >= 0);

        if (startIndex < s.Length)
        {
            builder.Append(s, startIndex, s.Length - startIndex);
        }

        return StringBuilderCache.GetStringAndRelease(builder);
    }
#endif
}
