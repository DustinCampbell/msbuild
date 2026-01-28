// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.TaskHost.Utilities;

/// <summary>
///  Provides static methods for escaping and unescaping special characters in MSBuild strings
///  using %XX hexadecimal encoding.
/// </summary>
/// <remarks>
///  PERF: since we escape and unescape relatively frequently, it may be worth caching
///  the last N strings that were (un)escaped.
/// </remarks>
internal static class EscapingUtilities
{
    /// <summary>
    ///  Special characters that need escaping.
    /// </summary>
    /// <remarks>
    ///  It's VERY important that the percent character is the FIRST on the list - since it's both a character
    ///  we escape and use in escape sequences, we can unintentionally escape other escape sequences if we
    ///  don't process it first. Of course we'll have a similar problem if we ever decide to escape hex digits
    ///  (that would require rewriting the algorithm) but since it seems unlikely that we ever do, this should
    ///  be good enough to avoid complicating the algorithm at this point.
    /// </remarks>
    private static readonly char[] s_charsToEscape = { '%', '*', '?', '@', '$', '(', ')', ';', '\'' };

    /// <summary>
    ///  Optional cache of escaped strings for use when needing to escape in performance-critical scenarios with significant
    ///  expected string reuse.
    /// </summary>
    private static readonly Dictionary<string, string> s_unescapedToEscapedValueCache = new(StringComparer.Ordinal);

    /// <summary>
    ///  Attempts to decode a single hexadecimal digit character.
    /// </summary>
    /// <param name="ch">The character to decode (0-9, A-F, a-f).</param>
    /// <param name="value">The decoded numeric value (0-15) if successful.</param>
    /// <returns>
    ///  <see langword="true"/> if the character is a valid hex digit; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool TryDecodeHexDigit(char ch, out int value)
    {
        switch (ch)
        {
            case >= '0' and <= '9':
                value = ch - '0';
                return true;

            case >= 'A' and <= 'F':
                value = ch - 'A' + 10;
                return true;

            case >= 'a' and <= 'f':
                value = ch - 'a' + 10;
                return true;
        }

        value = default;
        return false;
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
        // If the string doesn't contain anything, then by definition it doesn't
        // need unescaping.
        if (string.IsNullOrEmpty(escapedValue))
        {
            return escapedValue;
        }

        // If there are no percent signs, just return the original string immediately.
        // Don't even instantiate the StringBuilder.
        int indexOfPercent = escapedValue.IndexOf('%');
        if (indexOfPercent == -1)
        {
            return trim ? escapedValue.Trim() : escapedValue;
        }

        // This is where we're going to build up the final string to return to the caller.
        StringBuilder builder = StringBuilderCache.Acquire(escapedValue.Length);

        int currentPosition = 0;
        int escapedStringLength = escapedValue.Length;

        if (trim)
        {
            while (currentPosition < escapedValue.Length && char.IsWhiteSpace(escapedValue[currentPosition]))
            {
                currentPosition++;
            }

            if (currentPosition == escapedValue.Length)
            {
                return string.Empty;
            }

            while (char.IsWhiteSpace(escapedValue[escapedStringLength - 1]))
            {
                escapedStringLength--;
            }
        }

        // Loop until there are no more percent signs in the input string.
        while (indexOfPercent != -1)
        {
            // There must be two hex characters following the percent sign
            // for us to even consider doing anything with this.
            if ((indexOfPercent <= (escapedStringLength - 3)) &&
                TryDecodeHexDigit(escapedValue[indexOfPercent + 1], out int digit1) &&
                TryDecodeHexDigit(escapedValue[indexOfPercent + 2], out int digit2))
            {
                // First copy all the characters up to the current percent sign into
                // the destination.
                builder.Append(escapedValue, currentPosition, indexOfPercent - currentPosition);

                // Convert the %XX to an actual real character.
                char unescapedCharacter = (char)((digit1 << 4) + digit2);

                // if the unescaped character is not on the exception list, append it
                builder.Append(unescapedCharacter);

                // Advance the current pointer to reflect the fact that the destination string
                // is up to date with everything up to and including this escape code we just found.
                currentPosition = indexOfPercent + 3;
            }

            // Find the next percent sign.
            indexOfPercent = escapedValue.IndexOf('%', indexOfPercent + 1);
        }

        // Okay, there are no more percent signs in the input string, so just copy the remaining
        // characters into the destination.
        builder.Append(escapedValue, currentPosition, escapedStringLength - currentPosition);

        return StringBuilderCache.GetStringAndRelease(builder);
    }

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
        // If there are no special chars, just return the original string immediately.
        // Don't even instantiate the StringBuilder.
        if (string.IsNullOrEmpty(unescapedValue) || !ContainsReservedCharacters(unescapedValue))
        {
            return unescapedValue;
        }

        // next, if we're caching, check to see if it's already there.
        if (useCache && TryGetFromCache(unescapedValue, out string? cachedValue))
        {
            return cachedValue;
        }

        // This is where we're going to build up the final string to return to the caller.
        StringBuilder builder = StringBuilderCache.Acquire(unescapedValue.Length * 2);

        AppendEscapedString(builder, unescapedValue);

        string escapedString = StringBuilderCache.GetStringAndRelease(builder);

        return useCache
            ? AddToCache(escapedString, unescapedValue)
            : escapedString;
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
    ///  Determines whether the string contains any reserved characters that require escaping.
    /// </summary>
    /// <param name="unescapedValue">The string to check.</param>
    /// <returns>
    ///  <see langword="true"/> if the string contains reserved characters; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  This method can be called before escaping to determine if escaping is necessary at all,
    ///  saving calls to copy around item metadata that is the same whether escaped or not.
    /// </remarks>
    private static bool ContainsReservedCharacters(string unescapedValue)
        => unescapedValue.IndexOfAny(s_charsToEscape) != -1;

    /// <summary>
    ///  Converts the given integer into its hexadecimal character representation.
    /// </summary>
    /// <param name="x">The number to convert, which must be non-negative and less than 16.</param>
    /// <returns>
    ///  The hexadecimal character representation of <paramref name="x"/>.
    /// </returns>
    private static char HexDigitChar(int x)
        => (char)(x + (x < 10 ? '0' : ('a' - 10)));

    /// <summary>
    ///  Appends the escaped version of the given string to a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to which to append.</param>
    /// <param name="unescapedValue">The unescaped string.</param>
    private static void AppendEscapedString(StringBuilder builder, string unescapedValue)
    {
        // Replace each unescaped special character with an escape sequence one
        for (int idx = 0; ;)
        {
            int nextIdx = unescapedValue.IndexOfAny(s_charsToEscape, idx);
            if (nextIdx == -1)
            {
                builder.Append(unescapedValue, idx, unescapedValue.Length - idx);
                break;
            }

            builder.Append(unescapedValue, idx, nextIdx - idx);

            AppendEscapeCharSequence(builder, unescapedValue[nextIdx]);

            idx = nextIdx + 1;
        }
    }

    /// <summary>
    ///  Appends the escaped version of the given character to a <see cref="StringBuilder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="StringBuilder"/> to which to append.</param>
    /// <param name="ch">The character to escape.</param>
    private static void AppendEscapeCharSequence(StringBuilder builder, char ch)
    {
        // Append the escaped version which is a percent sign followed by two hexadecimal digits
        builder.Append('%');
        builder.Append(HexDigitChar(ch / 0x10));
        builder.Append(HexDigitChar(ch & 0x0F));
    }
}
