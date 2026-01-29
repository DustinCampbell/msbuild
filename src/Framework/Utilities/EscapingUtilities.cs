// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Provides static methods for escaping and unescaping special characters in MSBuild strings
///  using %XX hexadecimal encoding.
/// </summary>
/// <remarks>
///  PERF: since we escape and unescape relatively frequently, it may be worth caching
///  the last N strings that were (un)escaped.
/// </remarks>
internal static partial class EscapingUtilities
{
    private static readonly char[] s_specialChars = [
        '$',  // %24
        '%',  // %25
        '\'', // %27
        '(',  // %28
        ')',  // %29
        '*',  // %2A
        ';',  // %3B
        '?',  // %3F
        '@',  // %40
    ];

    // The special character with the highest ASII value is '@' (64).
    // Any table based on special caharacters must be one more than that.
    private const int TableSize = 65;

    private static readonly string[] s_escapeSequenceTable = BuildEscapeSequenceTable();

    private static string[] BuildEscapeSequenceTable()
    {
        string[] result = new string[TableSize];

        foreach (char c in s_specialChars)
        {
            result[c] = $"%{HexDigitChar(c / 0x10)}{HexDigitChar(c & 0x0f)}";
        }

        return result;

        static char HexDigitChar(int x)
            => (char)(x + (x < 10 ? '0' : ('a' - 10)));
    }

    /// <summary>
    ///  Optional cache of escaped strings for use when needing to escape in performance-critical scenarios with significant
    ///  expected string reuse.
    /// </summary>
    private static readonly Dictionary<string, string> s_unescapedToEscapedValueCache = new(StringComparer.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryDecodeHexDigits(char ch1, char ch2, out char result)
    {
        int hi = HexConverter.FromChar(ch1);
        int lo = HexConverter.FromChar(ch2);

        if ((hi | lo) == 0xFF)
        {
            result = default;
            return false;
        }

        result = (char)((hi << 4) | lo);
        return true;
    }

    /// <summary>
    ///  Replaces all instances of %XX in the input string with the character represented
    ///  by the hexadecimal number XX.
    /// </summary>
    /// <param name="escapedValue">The string to unescape.</param>
    /// <param name="trim">If <see langword="true"/>, the string is trimmed before being unescaped.</param>
    /// <returns>
    ///  The unescaped string.
    /// </returns>
    public static string UnescapeAll(string escapedValue, bool trim = false)
    {
        if (string.IsNullOrEmpty(escapedValue))
        {
            return escapedValue;
        }

        if (!TryFindFirstEscape(escapedValue, out int index, out char decoded))
        {
            return trim ? escapedValue.Trim() : escapedValue;
        }

        return UnescapeAllCore(escapedValue, index, decoded, trim);

        static string UnescapeAllCore(string escapedValue, int index, char decoded, bool trim)
        {
            var builder = new RefArrayBuilder<EscapeSequence>(stackalloc EscapeSequence[32]);
            try
            {
                builder.Add(new(index, decoded));
                CollectEscapes(escapedValue, index + 1, ref builder);

                return Decode(escapedValue, builder.AsSpan(), trim);
            }
            finally
            {
                builder.Dispose();
            }
        }
    }

    private readonly record struct EscapeSequence(int Index, char Decoded);

    /// <summary>
    ///  Escapes special characters by replacing them with %XX hexadecimal sequences, where XX is the
    ///  ASCII code of the character.
    /// </summary>
    /// <param name="unescapedValue">The string to escape.</param>
    /// <param name="useCache">
    ///  If <see langword="true"/>, the cache is checked and the result is interned and cached for performance;
    ///  otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>
    ///  The escaped string.
    /// </returns>
    /// <remarks>
    ///  Caching is only recommended for scenarios where there's expected to be significant
    ///  repetition of the escaped string. The cache currently grows unbounded.
    /// </remarks>
    public static string Escape(string unescapedValue, bool useCache = false)
    {
        if (string.IsNullOrEmpty(unescapedValue))
        {
            return unescapedValue;
        }

        var builder = new RefArrayBuilder<int>(stackalloc int[32]);

        try
        {
            if (!CollectEncodeIndices(unescapedValue, ref builder))
            {
                return unescapedValue;
            }

            if (useCache && TryGetFromCache(unescapedValue, out string? cachedString))
            {
                return cachedString;
            }

            int resultLength = unescapedValue.Length + (builder.Count * 2);
            string result = new('\0', resultLength);

            Encode(unescapedValue, result, builder.AsSpan());

            return useCache
                ? AddToCache(result, unescapedValue)
                : result;
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static bool TryGetFromCache(string unescapedValue, [NotNullWhen(true)] out string? escapedValue)
    {
        lock (s_unescapedToEscapedValueCache)
        {
            return s_unescapedToEscapedValueCache.TryGetValue(unescapedValue, out escapedValue);
        }
    }

    private static string AddToCache(string escapedValue, string unescapedValue)
    {
        string result = Strings.WeakIntern(escapedValue);

        lock (s_unescapedToEscapedValueCache)
        {
            s_unescapedToEscapedValueCache[unescapedValue] = result;
        }

        return result;
    }

    /// <summary>
    ///  Determines whether the string contains the escaped form of wildcard characters ('*' or '?').
    /// </summary>
    /// <param name="escapedValue">The string to check.</param>
    /// <returns>
    ///  <see langword="true"/> if the string contains %2A (escaped '*') or %3F (escaped '?');
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public static bool ContainsEscapedWildcards(string escapedValue)
    {
        if (escapedValue.Length < 3)
        {
            return false;
        }

        return ContainsEscapedWildcardsCore(escapedValue);
    }
}
