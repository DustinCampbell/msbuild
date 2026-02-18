// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
}
