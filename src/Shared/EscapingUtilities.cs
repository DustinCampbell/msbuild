// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.NET.StringTools;

#if !NET35
using System.Buffers;
using System.Runtime.CompilerServices;
#endif

#nullable disable

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace Microsoft.Build.Shared;

/// <summary>
/// This class implements static methods to assist with unescaping of %XX codes
/// in the MSBuild file format.
/// </summary>
/// <remarks>
///  <para>
///   This class has two platform-specific implementations to optimize for each target framework.
///  </para>
///  <para>
///   When making changes, ensure both implementations are updated consistently:
///  </para>
///  <list type="bullet">
///   <item>Modern (.NET 6+): Uses SearchValues and modern span APIs with span slicing.</item>
///   <item>
///    NetFx (.NET Framework 3.5/4.7.2/.NET Standard 2.0): Uses unsafe pointers with 
///    stack-allocated buffers on modern frameworks and inline field storage on .NET Framework 3.5.
///   </item>
///  </list>
/// </remarks>
internal static class EscapingUtilities
{
    // MSBuild's special characters all fall within the ASCII range 36 ('$') to 64 ('@'), allowing us to use small lookup tables.
    // Note that we need index 64, so the table size needs to be 65.
    private const int TableSize = 65;

    private static readonly char[] s_specialChars =
    [
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

    private static readonly char[][] s_escapeSequenceTable = BuildEscapeSequenceTable();

    private static char[][] BuildEscapeSequenceTable()
    {
        char[][] result = new char[TableSize][];

        foreach (char c in s_specialChars)
        {
            result[c] = [
                HexDigitChar(c / 0x10),
                HexDigitChar(c & 0x0F)
            ];
        }

        return result;

        static char HexDigitChar(int x)
            => (char)(x + (x < 10 ? '0' : ('a' - 10)));
    }

    private static readonly byte[] s_hexDecodeTable =
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
        0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
        0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
        0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 255
    ];

    /// <summary>
    ///  Optional cache of escaped strings for use when needing to escape in performance-critical scenarios with significant
    ///  expected string reuse.
    /// </summary>
    private static readonly Dictionary<string, string> s_unescapedToEscapedStrings = new(StringComparer.Ordinal);

    private static bool TryGetCachedEscapedString(string unescapedString, out string cachedString)
    {
        lock (s_unescapedToEscapedStrings)
        {
            return s_unescapedToEscapedStrings.TryGetValue(unescapedString, out cachedString);
        }
    }

    private static string CacheIfRequested(string result, string unescapedString, bool cache)
    {
        if (!cache)
        {
            return result;
        }

        string escapedString = Strings.WeakIntern(result);

        lock (s_unescapedToEscapedStrings)
        {
            s_unescapedToEscapedStrings[unescapedString] = escapedString;
        }

        return escapedString;
    }

    /// <summary>
    ///  Replaces all instances of %XX in the input string with the character represented
    ///  by the hexadecimal number XX.
    /// </summary>
    /// <param name="value">The string to unescape.</param>
    /// <param name="trim">
    ///  If <see langword="true"/>, trims leading and trailing whitespace before unescaping.
    /// </param>
    /// <returns>
    ///  The unescaped string, or an empty string if the trimmed input is empty.
    /// </returns>
    internal static string UnescapeAll(string value, bool trim = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        int firstPercentIndex = value.IndexOf('%');
        if (firstPercentIndex < 0)
        {
            // No escape sequences found. Just return the original string (trimmed if necessary).
            return trim ? value.Trim() : value;
        }

#if NET
        return Modern.DecodeString(value, firstPercentIndex, trim);
#else
        return NetFx.DecodeString(value, firstPercentIndex, trim);
#endif
    }

#if !NET35
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
    private static bool TryDecodeHexDigits(char char1, char char2, out char result)
    {
        // Bounds check both chars at once using bitwise OR
        if ((char1 | char2) >= s_hexDecodeTable.Length)
        {
            result = default;
            return false;
        }

        int digit1 = s_hexDecodeTable[char1];
        int digit2 = s_hexDecodeTable[char2];

        // Check if both lookups were valid (not 0xFF) using bitwise OR
        if ((digit1 | digit2) == 0xFF)
        {
            result = default;
            return false;
        }

        result = (char)((digit1 << 4) | digit2);
        return true;
    }

    /// <summary>
    ///  Adds instances of %XX in the input string where special characters appear.
    ///  XX is the hex value of the ASCII code for the character.
    /// </summary>
    /// <param name="value">The string to escape.</param>
    /// <param name="cache">
    ///  If <see langword="true"/>, interns and caches the result for performance when the same
    ///  strings are escaped repeatedly. Default is <see langword="false"/>.
    /// </param>
    /// <returns>
    ///  The escaped string, or the original string if no escaping is needed.
    /// </returns>
    /// <remarks>
    ///  Special characters that are escaped: $ % ' ( ) * ; ? @
    ///  When <paramref name="cache"/> is true, the result is weakly interned and cached for reuse.
    ///  Caching is only recommended when the same strings are escaped repeatedly, as the cache
    ///  grows unbounded.
    /// </remarks>
    internal static string Escape(string value, bool cache = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

#if NET
        return Modern.EncodeString(value, cache);
#else
        return NetFx.EncodeString(value, cache);
#endif
    }

    /// <summary>
    ///  Determines whether the string contains the escaped form of wildcard characters ('*' or '?').
    /// </summary>
    /// <param name="value">The string to check for escaped wildcards.</param>
    /// <returns>
    ///  <see langword="true"/> if the string contains %2a, %2A, %3f, or %3F; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  This method looks for the escape sequences %2a, %2A (asterisk) and %3f, %3F (question mark).
    /// </remarks>
    internal static bool ContainsEscapedWildcards(string value)
    {
        if (value.Length < 3)
        {
            return false;
        }

#if NET
        return Modern.ContainsEscapedWildcards(value);
#else
        return NetFx.ContainsEscapedWildcards(value);
#endif
    }

#if NET

    private static class Modern
    {
        private static readonly SearchValues<char> s_searchValues = SearchValues.Create(s_specialChars);

        public static string DecodeString(string value, int firstPercentIndex, bool trim)
        {
            ReadOnlySpan<char> span = value.AsSpan();
            int start = 0;
            int length = span.Length;

            if (trim)
            {
                ReadOnlySpan<char> startTrimmed = span.TrimStart();
                start = span.Length - startTrimmed.Length;
                span = startTrimmed.TrimEnd();
                length = span.Length;

                if (span.IsEmpty)
                {
                    return string.Empty;
                }

                // Adjust firstPercentIndex to be relative to the trimmed span
                firstPercentIndex -= start;
            }

            // Allocate a buffer the same size as the input (decoded string will be smaller or equal)
            const int MaxStackAlloc = 256;
            char[] rentedArray = null;
            Span<char> destination = length <= MaxStackAlloc
                ? stackalloc char[length]
                : (rentedArray = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);

            try
            {
                // Single pass: decode and copy characters
                int destPos = 0;
                int percentIndex = firstPercentIndex;

                do
                {
                    // Copy characters before the next '%'.
                    span[..percentIndex].CopyTo(destination[destPos..]);
                    span = span[percentIndex..];
                    destPos += percentIndex;

                    if (span is ['%', char c1, char c2, ..] &&
                        TryDecodeHexDigits(c1, c2, out char decodedChar))
                    {
                        // Valid escape sequence found, decode it
                        destination[destPos++] = decodedChar;
                        span = span[3..]; // Move past the escape sequence
                    }
                    else
                    {
                        // Not a valid escape sequence, copy the '%'
                        destination[destPos++] = '%';
                        span = span[1..];
                    }

                    percentIndex = span.IndexOf('%');
                }
                while (percentIndex >= 0);

                if (!span.IsEmpty)
                {
                    // Copy remaining characters.
                    span.CopyTo(destination[destPos..]);
                    destPos += span.Length;
                }

                // If no decoding happened (destPos == length), return original string (potentially trimmed)
                if (destPos == length)
                {
                    return trim ? value.Substring(start, length) : value;
                }

                // Create result string from buffer
                return new string(destination[..destPos]);
            }
            finally
            {
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
            }
        }

        public static string EncodeString(string value, bool cache)
        {
            // Find the first special character to encode
            ReadOnlySpan<char> span = value.AsSpan();
            int firstSpecialIndex = span.IndexOfAny(s_searchValues);

            if (firstSpecialIndex < 0)
            {
                // No special characters found. Just return the original string.
                return value;
            }

            var items = new Buffer<EncodeData>(stackalloc EncodeData[32]);

            try
            {
                // We already found the first special character, pass it to TryFindEncodeItems
                TryFindEncodeItems(value, firstSpecialIndex, ref items);

                // Is there a cached version of this string? If so, return it!
                if (cache && TryGetCachedEscapedString(value, out string cachedString))
                {
                    return cachedString;
                }

                // OK. We have to make a new string. We can pre-compute it's length because
                // we know how many characters we need to encode. Escape sequences are always
                // 3 characters long, so we can calculate the new length as:
                // original length + (number of characters to encode * 2).
                int resultLength = value.Length + (items.Count * 2);
                string result = new('\0', resultLength);

                unsafe
                {
                    fixed (char* destPtr = result)
                    {
                        var destination = new Span<char>(destPtr, resultLength);

                        Encode(value, destination, in items);
                    }
                }

                return CacheIfRequested(result, value, cache);
            }
            finally
            {
                items.Dispose();
            }
        }

        private static void TryFindEncodeItems(string value, int firstSpecialIndex, ref Buffer<EncodeData> items)
        {
            ReadOnlySpan<char> source = value.AsSpan();
            int start = 0;
            int charIndex = firstSpecialIndex;

            while (charIndex >= 0)
            {
                int index = start + charIndex;
                char c = source[charIndex];
                char[] escapeSequence = s_escapeSequenceTable[c];

                items.Add(new(index, escapeSequence[0], escapeSequence[1]));

                // Move past the special character
                start += charIndex + 1;
                source = source[(charIndex + 1)..];

                // Find the next special character
                charIndex = source.IndexOfAny(s_searchValues);
            }
        }

        private static void Encode(ReadOnlySpan<char> source, Span<char> destination, ref readonly Buffer<EncodeData> items)
        {
            int sourcePos = 0;

            foreach (var (index, encodedDigit1, encodedDigit2) in items)
            {
                // Copy normal characters.
                int copyLength = index - sourcePos;
                if (copyLength > 0)
                {
                    source[sourcePos..index].CopyTo(destination);
                    destination = destination[copyLength..];
                }

                // Write escape sequence.
                destination[0] = '%';
                destination[1] = encodedDigit1;
                destination[2] = encodedDigit2;
                destination = destination[3..];

                // Move past the special character
                sourcePos = index + 1;
            }

            // Copy remaining characters
            if (sourcePos < source.Length)
            {
                source[sourcePos..].CopyTo(destination);
            }
        }

        public static bool ContainsEscapedWildcards(string value)
        {
            ReadOnlySpan<char> span = value;

            while (!span.IsEmpty)
            {
                int percentIndex = span.IndexOf('%');
                if (percentIndex < 0)
                {
                    return false;
                }

                span = span[(percentIndex + 1)..];

                switch (span)
                {
                    case ['2', ('a' or 'A'), ..]:
                        return true;

                    case ['3', ('f' or 'F'), ..]:
                        return true;
                }
            }

            return false;
        }
    }

#endif

#if !NET

    private static class NetFx
    {
        private static readonly bool[] s_specialCharTable = BuildSpecialCharTable();

        private static bool[] BuildSpecialCharTable()
        {
            bool[] result = new bool[TableSize];

            foreach (char c in s_specialChars)
            {
                result[c] = true;
            }

            return result;
        }

        public static unsafe string DecodeString(string value, int firstPercentIndex, bool trim)
        {
            if (!TryGetStartAndLength(value, trim, out int start, out int length))
            {
                return string.Empty;
            }

            if (trim)
            {
                firstPercentIndex -= start;
            }

#if !NET35
            const int MaxStackAlloc = 256;
            char[] rentedArray = null;
            Span<char> destination = length <= MaxStackAlloc
                ? stackalloc char[length]
                : (rentedArray = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
#else
            char[] destination = new char[length];
#endif

            try
            {
                fixed (char* srcPtr = value)
                fixed (char* destPtr = destination)
                {
                    int srcPos = start;
                    int end = start + length;
                    int destPos = 0;
                    int percentIndex = firstPercentIndex;

                    do
                    {
                        // Bulk copy characters before the next '%'
                        if (percentIndex > 0)
                        {
                            CopyChars(srcPtr + srcPos, destPtr + destPos, percentIndex);
                            destPos += percentIndex;
                            srcPos += percentIndex;
                        }

                        // Process the '%' character
                        if (srcPos + 2 < end &&
                            TryDecodeHexDigits(srcPtr[srcPos + 1], srcPtr[srcPos + 2], out char decodedChar))
                        {
                            // Valid escape sequence
                            destPtr[destPos++] = decodedChar;
                            srcPos += 3;
                        }
                        else
                        {
                            // Invalid escape sequence, just copy the '%'
                            destPtr[destPos++] = srcPtr[srcPos++];
                        }

                        // Find next '%'
                        percentIndex = IndexOf('%', srcPtr, srcPos, end - srcPos);
                    }
                    while (percentIndex >= 0);

                    if (srcPos < end)
                    {
                        // No more '%', bulk copy remaining
                        int remaining = end - srcPos;
                        CopyChars(srcPtr + srcPos, destPtr + destPos, remaining);
                        destPos += remaining;
                    }

                    // If no decoding happened, return original string
                    if (destPos == length)
                    {
                        return trim ? value.Substring(start, length) : value;
                    }

#if !NET35
                    return destination[..destPos].ToString();
#else
                    return new string(destination, 0, destPos);
#endif
                }
            }
            finally
            {
#if !NET35
                if (rentedArray != null)
                {
                    ArrayPool<char>.Shared.Return(rentedArray);
                }
#endif
            }

            static int IndexOf(char c, char* ptr, int start, int length)
            {
                int end = start + length;

                for (int i = start; i < end; i++)
                {
                    if (ptr[i] == '%')
                    {
                        return i - start;
                    }
                }

                return -1;
            }
        }

        private static bool TryGetStartAndLength(string value, bool trim, out int start, out int length)
        {
            start = 0;
            length = value.Length;

            if (!trim)
            {
                return true;
            }

            while (start < length && char.IsWhiteSpace(value[start]))
            {
                start++;
            }

            while (length > start && char.IsWhiteSpace(value[length - 1]))
            {
                length--;
            }

            length -= start;

            return length != 0;
        }

        private static unsafe bool TryFindDecodeItems(string value, int start, int length, int percentIndex, ref Buffer<DecodeData> items)
        {
            fixed (char* ptr = value)
            {
                start += percentIndex;

                // We need at least 2 characters after '%' for a valid escape sequence (%XX),
                // so we stop the loop 2 characters before the end.
                int end = start + length - 2;

                for (int i = start; i < end; i++)
                {
                    if (ptr[i] == '%' && TryDecodeHexDigits(ptr[i + 1], ptr[i + 2], out char decodedChar))
                    {
                        items.Add(new(i, decodedChar));

                        i += 2; // Skip the two hex digits.
                    }
                }
            }

            return items.Count > 0;
        }

        private static unsafe void DecodeItems(string source, int start, int length, string destination, ref readonly Buffer<DecodeData> items)
        {
            fixed (char* srcPtr = source)
            fixed (char* dstPtr = destination)
            {
                int srcPos = start;
                int dstPos = 0;

                foreach (var (index, decodedChar) in items)
                {
                    // Copy characters before the escape sequence.
                    int copyLength = index - srcPos;
                    if (copyLength > 0)
                    {
                        CopyChars(srcPtr + srcPos, dstPtr + dstPos, copyLength);
                        dstPos += copyLength;
                    }

                    // Write decoded character.
                    dstPtr[dstPos] = decodedChar;
                    dstPos++;

                    // Escape sequence is 3 characters long.
                    srcPos = index + 3;
                }

                // Copy remaining characters
                if (srcPos < start + length)
                {
                    CopyChars(srcPtr + srcPos, dstPtr + dstPos, start + length - srcPos);
                }
            }
        }

        public static string EncodeString(string value, bool cache)
        {
            // Find the first special character to encode
            int firstSpecialIndex = FindFirstSpecialCharacter(value);

            if (firstSpecialIndex < 0)
            {
                // No special characters found. Just return the original string.
                return value;
            }

#if !NET35
            // Start with 32 items on the stack. This handles most real-world cases without
            // needing to rent from ArrayPool.
            var items = new Buffer<EncodeData>(stackalloc EncodeData[32]);
#else
            var items = new Buffer<EncodeData>();
#endif

            try
            {
                // We already found the first special character, pass it to TryFindEncodeItems
                TryFindEncodeItems(value, firstSpecialIndex, ref items);

                // Is there a cached version of this string? If so, return it!
                if (cache && TryGetCachedEscapedString(value, out string cachedString))
                {
                    return cachedString;
                }

                // OK. We have to make a new string. We can pre-compute it's length because
                // we know how many characters we need to encode. Escape sequences are always
                // 3 characters long, so we can calculate the new length as:
                // original length + (number of characters to encode * 2).
                int resultLength = value.Length + (items.Count * 2);
                string result = new('\0', resultLength);

                Encode(value, result, ref items);

                return CacheIfRequested(result, value, cache);
            }
            finally
            {
                items.Dispose();
            }
        }

        private static unsafe int FindFirstSpecialCharacter(string value)
        {
            fixed (char* ptr = value)
            {
                int length = value.Length;

                for (int i = 0; i < length; i++)
                {
                    char c = ptr[i];

                    // Fast range check: all special characters fall between '$' (0x24) and '@' (0x40).
                    // Using unsigned arithmetic avoids two comparisons (c >= '$' && c <= '@').
                    if ((uint)(c - '$') <= ('@' - '$') && s_specialCharTable[c])
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static unsafe void TryFindEncodeItems(string value, int firstSpecialIndex, ref Buffer<EncodeData> items)
        {
            fixed (char* ptr = value)
            {
                int length = value.Length;

                for (int i = firstSpecialIndex; i < length; i++)
                {
                    char c = ptr[i];

                    // Fast range check: all special characters fall between '$' (0x24) and '@' (0x40).
                    // Using unsigned arithmetic avoids two comparisons (c >= '$' && c <= '@').
                    if ((uint)(c - '$') <= ('@' - '$') && s_specialCharTable[c])
                    {
                        char[] escapeSequence = s_escapeSequenceTable[c];
                        items.Add(new(i, escapeSequence[0], escapeSequence[1]));
                    }
                }
            }
        }

        private static unsafe void Encode(string source, string destination, ref readonly Buffer<EncodeData> items)
        {
            int length = source.Length;

            fixed (char* srcPtr = source)
            fixed (char* dstPtr = destination)
            {
                int srcPos = 0;
                int dstPos = 0;

                foreach (var (index, encodedDigit1, encodedDigit2) in items)
                {
                    // Copy characters before the escape sequence.
                    int copyLength = index - srcPos;
                    if (copyLength > 0)
                    {
                        CopyChars(srcPtr + srcPos, dstPtr + dstPos, copyLength);
                        dstPos += copyLength;
                    }

                    // Write escape sequence.
                    dstPtr[dstPos] = '%';
                    dstPtr[dstPos + 1] = encodedDigit1;
                    dstPtr[dstPos + 2] = encodedDigit2;
                    dstPos += 3;

                    srcPos = index + 1;
                }

                // Copy remaining characters
                if (srcPos < length)
                {
                    CopyChars(srcPtr + srcPos, dstPtr + dstPos, length - srcPos);
                }
            }
        }

        private static unsafe void CopyChars(char* source, char* destination, int count)
        {
#if !NET35
            Buffer.MemoryCopy(source, destination, count * sizeof(char), count * sizeof(char));
#else
            // We don't have Buffer.MemoryCopy on .NET Framework 3.5, so we fall back to a simple loop.
            for (int i = 0; i < count; i++)
            {
                destination[i] = source[i];
            }
#endif
        }

        public static unsafe bool ContainsEscapedWildcards(string value)
        {
            int length = value.Length - 2;

            fixed (char* ptr = value)
            {
                for (int i = 0; i < length; i++)
                {
                    if (ptr[i] != '%')
                    {
                        continue;
                    }

                    if (ptr[i + 1] == '2' && ptr[i + 2] is 'a' or 'A')
                    {
                        // %2a or %2A
                        return true;
                    }

                    if (ptr[i + 1] == '3' && ptr[i + 2] is 'f' or 'F')
                    {
                        // %2a or %2A
                        return true;
                    }
                }
            }

            return false;
        }
    }

#endif

    private readonly record struct DecodeData(int Index, char EncodedChar);

    private readonly record struct EncodeData(int Index, char DecodedHexChar1, char DecodedHexChar2);

    /// <summary>
    ///  Provides a stack-allocated, growable buffer for storing a sequence of value types. Designed for efficient
    ///  temporary storage and enumeration of elements without heap allocations when possible.
    /// </summary>
    /// <remarks>
    ///  The buffer is implemented as a ref struct and must be used within the stack scope; it
    ///  cannot be stored on the heap or used across async boundaries. When the buffer exceeds its initial capacity,
    ///  it may rent arrays from the shared array pool to accommodate additional elements. Callers should dispose the
    ///  buffer when finished to return any rented arrays to the pool and avoid resource leaks. This type is intended
    ///  for performance-critical scenarios where minimizing allocations is important.
    /// </remarks>
    /// <typeparam name="T">The value type to store in the buffer.</typeparam>
    private ref struct Buffer<T>
        where T : struct
    {
#if !NET35
        private Span<T> _array;
        private T[] _rentedArray;
        private int _count;

        public Buffer(Span<T> initialBuffer)
        {
            _array = initialBuffer;
            _rentedArray = null;
            _count = 0;
        }

        public void Dispose()
        {
            if (_rentedArray != null)
            {
                ArrayPool<T>.Shared.Return(_rentedArray);
                _rentedArray = null;
            }
        }

        public readonly int Count => _count;

        public readonly T this[int index] => _array[index];

        public void Add(T value)
        {
            if (_count == _array.Length)
            {
                Grow();
            }

            _array[_count++] = value;
        }

        private void Grow()
        {
            int newSize = _array.Length * 2;
            T[] newArray = ArrayPool<T>.Shared.Rent(newSize);
            _array.CopyTo(newArray);

            if (_rentedArray != null)
            {
                ArrayPool<T>.Shared.Return(_rentedArray);
            }

            _rentedArray = newArray;
            _array = newArray.AsSpan(0, newSize);
        }

#else

        // For .NET Framework 3.5 compatibility, we can't use 'stackalloc' or
        // 'Span<T>' so we implement a simple hybrid storage mechanism that
        // uses fields for the first four items and an array for any additional items.

        // Store the first 4 items inline to avoid array allocation for common cases.
        // Most escaped/unescaped strings have fewer than 4 special characters.
        private T _item1;
        private T _item2;
        private T _item3;
        private T _item4;

        private T[] _array;
        private int _count;

        public readonly int Count => _count;

        public readonly T this[int index] => index switch
        {
            0 => _item1,
            1 => _item2,
            2 => _item3,
            3 => _item4,
            _ => _array[index - 4],
        };

        public void Dispose()
        {
            // Nothing to do here for .NET Framework 3.5.
        }

        public void Add(T item)
        {
            if (_count < 4)
            {
                switch (_count)
                {
                    case 0:
                        _item1 = item;
                        break;
                    case 1:
                        _item2 = item;
                        break;
                    case 2:
                        _item3 = item;
                        break;
                    case 3:
                        _item4 = item;
                        break;
                }
            }
            else
            {
                _array ??= new T[16];

                if (_count == _array.Length)
                {
                    Array.Resize(ref _array, _array.Length * 2);
                }

                _array[_count - 4] = item;
            }

            _count++;
        }
#endif

        public readonly Enumerator GetEnumerator()
            => new(this);

        public ref struct Enumerator
        {
            private readonly Buffer<T> _buffer;
            private int _index;

            internal Enumerator(Buffer<T> buffer)
            {
                _buffer = buffer;
                _index = -1;
            }

            public readonly T Current => _buffer[_index];

            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _buffer.Count)
                {
                    _index = index;
                    return true;
                }

                return false;
            }
        }
    }
}
