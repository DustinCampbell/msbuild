// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.NET.StringTools;

#if !NET35
using System.Buffers;
#endif

#nullable disable

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace Microsoft.Build.Shared;

/// <summary>
/// This class implements static methods to assist with unescaping of %XX codes
/// in the MSBuild file format.
/// </summary>
/// <remarks>
///  This class has two platform-specific implementations to optimize for each target framework.
///  When making changes, ensure both implementations are updated consistently:
///  - Modern (.NET 6+): Uses SearchValues and modern span APIs with span slicing.
///  - NetFx (.NET Framework 3.5/4.7.2/.NET Standard 2.0): Uses unsafe pointers with 
///    stack-allocated buffers on modern frameworks and inline field storage on .NET Framework 3.5.
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

    /// <summary>
    /// Optional cache of escaped strings for use when needing to escape in performance-critical scenarios with significant
    /// expected string reuse.
    /// </summary>
    private static readonly Dictionary<string, string> s_unescapedToEscapedStrings = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Replaces all instances of %XX in the input string with the character represented
    /// by the hexadecimal number XX.
    /// </summary>
    /// <param name="value">The string to unescape.</param>
    /// <param name="trim">If the string should be trimmed before being unescaped.</param>
    /// <returns>unescaped string</returns>
    internal static string UnescapeAll(string value, bool trim = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!TryGetStartAndLength(value, trim, out int start, out int length))
        {
            return string.Empty;
        }

#if NET
        return Modern.DecodeString(value, start, length, trim);
#else
        return NetFx.DecodeString(value, start, length, trim);
#endif

        static bool TryGetStartAndLength(string value, bool trim, out int start, out int length)
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
    }

    private static bool TryDecodeHexDigits(char char1, char char2, out char result)
    {
        if (TryDecodeHexDigit(char1, out int digit1) &&
            TryDecodeHexDigit(char2, out int digit2))
        {
            result = (char)((digit1 << 4) + digit2);
            return true;
        }

        result = default;
        return false;

        static bool TryDecodeHexDigit(char c, out int digit)
        {
            switch (c)
            {
                case >= '0' and <= '9':
                    digit = c - '0';
                    return true;

                case >= 'A' and <= 'F':
                    digit = c - 'A' + 10;
                    return true;

                case >= 'a' and <= 'f':
                    digit = c - 'a' + 10;
                    return true;

                default:
                    digit = default;
                    return false;
            }
        }
    }

    /// <summary>
    /// Adds instances of %XX in the input string where the char to be escaped appears
    /// XX is the hex value of the ASCII code for the char.  Interns and caches the result.
    /// </summary>
    /// <comment>
    /// NOTE:  Only recommended for use in scenarios where there's expected to be significant
    /// repetition of the escaped string.  Cache currently grows unbounded.
    /// </comment>
    internal static string EscapeWithCaching(string unescapedString)
    {
        return EscapeWithOptionalCaching(unescapedString, cache: true);
    }

    /// <summary>
    /// Adds instances of %XX in the input string where the char to be escaped appears
    /// XX is the hex value of the ASCII code for the char.
    /// </summary>
    /// <param name="unescapedString">The string to escape.</param>
    /// <returns>escaped string</returns>
    internal static string Escape(string unescapedString)
    {
        return EscapeWithOptionalCaching(unescapedString, cache: false);
    }

    /// <summary>
    /// Adds instances of %XX in the input string where the char to be escaped appears
    /// XX is the hex value of the ASCII code for the char.  Caches if requested.
    /// </summary>
    /// <param name="value">The string to escape.</param>
    /// <param name="cache">
    /// True if the cache should be checked, and if the resultant string
    /// should be cached.
    /// </param>
    private static string EscapeWithOptionalCaching(string value, bool cache)
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
    /// Determines whether the string contains the escaped form of '*' or '?'.
    /// </summary>
    /// <param name="escapedString"></param>
    /// <returns></returns>
    internal static bool ContainsEscapedWildcards(string escapedString)
    {
        if (escapedString.Length < 3)
        {
            return false;
        }

#if NET
        return Modern.ContainsEscapedWildcards(escapedString);
#else
        return NetFx.ContainsEscapedWildcards(escapedString);
#endif
    }

#if NET

    private static class Modern
    {
        private static readonly SearchValues<char> s_searchValues = SearchValues.Create(s_specialChars);

        public static unsafe string DecodeString(string value, int start, int length, bool trim)
        {
            var items = new Buffer<DecodeData>(stackalloc DecodeData[32]);
            try
            {
                ReadOnlySpan<char> span = value.AsSpan(start, length);

                // Try to find any decode items in the string.
                if (!TryFindDecodeItems(span, ref items))
                {
                    // No decode items found. Just return the original string (trimmed if necessary).
                    return trim ? value.Substring(start, length) : value;
                }

                // OK. We have to make a new string. We can pre-compute its length because
                // we know how many escape sequences we need to decode.
                // Escape sequences are always 3 characters long, so the length of the new string is:
                //
                // original length - (number of escape sequences * 2).
                int resultLength = length - (items.Count * 2);
                string result = new('\0', resultLength);

                fixed (char* dstPtr = result)
                {
                    var destination = new Span<char>(dstPtr, resultLength);

                    DecodeItems(span, destination, ref items);
                }

                return result;
            }
            finally
            {
                items.Dispose();
            }
        }

        private static bool TryFindDecodeItems(ReadOnlySpan<char> source, ref Buffer<DecodeData> items)
        {
            int start = 0;

            while (!source.IsEmpty)
            {
                int percentIndex = source.IndexOf('%');
                if (percentIndex < 0)
                {
                    // No more percent characters found.
                    break;
                }

                // We found a percent character. Move past it.
                int index = start + percentIndex;
                start += percentIndex + 1;
                source = source[(percentIndex + 1)..];

                if (source is [char c1, char c2, ..] && TryDecodeHexDigits(c1, c2, out char decodedChar))
                {
                    // We found an escape sequence. And it and move past the hex digits.
                    items.Add(new(index, decodedChar));

                    start += 2;
                    source = source[2..];
                }
            }

            return items.Count > 0;
        }

        private static void DecodeItems(ReadOnlySpan<char> source, Span<char> destination, ref readonly Buffer<DecodeData> items)
        {
            int sourcePos = 0;

            foreach (var (index, decodedChar) in items)
            {
                // Copy characters before the escape sequence.
                int copyLength = index - sourcePos;
                if (copyLength > 0)
                {
                    source[sourcePos..index].CopyTo(destination);
                    destination = destination[copyLength..];
                }

                // Write decoded character.
                destination[0] = decodedChar;
                destination = destination[1..];

                sourcePos = index + 3;
            }

            // Copy remaining characters
            if (sourcePos < source.Length)
            {
                source[sourcePos..].CopyTo(destination);
            }
        }

        public static string EncodeString(string value, bool cache)
        {
            var items = new Buffer<EncodeData>(stackalloc EncodeData[32]);

            try
            {
                // Try to find any decode items in the string.
                if (!TryFindEncodeItems(value, ref items))
                {
                    // No encode items found. Just return the original string.
                    return value;
                }

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

        private static bool TryFindEncodeItems(string value, ref Buffer<EncodeData> items)
        {
            ReadOnlySpan<char> source = value.AsSpan();
            int start = 0;

            while (!source.IsEmpty)
            {
                int charIndex = source.IndexOfAny(s_searchValues);
                if (charIndex < 0)
                {
                    // No more special characters found
                    break;
                }

                int index = start + charIndex;
                char c = source[charIndex];
                char[] escapeSequence = s_escapeSequenceTable[c];

                items.Add(new(index, escapeSequence[0], escapeSequence[1]));

                // Move past the special character
                start += charIndex + 1;
                source = source[(charIndex + 1)..];
            }

            return items.Count > 0;
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

        public static string DecodeString(string value, int start, int length, bool trim)
        {
#if !NET35
            var items = new Buffer<DecodeData>(stackalloc DecodeData[32]);
#else
            var items = new Buffer<DecodeData>();
#endif

            try
            {
                // Try to find any decode items in the string.
                if (!TryFindDecodeItems(value, start, length, ref items))
                {
                    // No decode items found. Just return the original string (trimmed if necessary).
                    return trim ? value.Substring(start, length) : value;
                }

                // OK. We have to make a new string. We can pre-compute its length because
                // we know how many escape sequences we need to decode.
                // Escape sequences are always 3 characters long, so the length of the new string is:
                //
                // original length - (number of escape sequences * 2).
                int resultLength = length - (items.Count * 2);
                string result = new('\0', resultLength);

                DecodeItems(value, start, length, result, ref items);

                return result;
            }
            finally
            {
                items.Dispose();
            }
        }

        private static unsafe bool TryFindDecodeItems(string value, int start, int length, ref Buffer<DecodeData> items)
        {
            fixed (char* ptr = value)
            {
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

        private static unsafe bool TryFindEncodeItems(string value, ref Buffer<EncodeData> items)
        {
            fixed (char* ptr = value)
            {
                int length = value.Length;

                for (int i = 0; i < length; i++)
                {
                    char c = ptr[i];

                    if ((uint)(c - '$') <= ('@' - '$') && s_specialCharTable[c])
                    {
                        char[] escapeSequence = s_escapeSequenceTable[c];
                        items.Add(new(i, escapeSequence[0], escapeSequence[1]));
                    }
                }
            }

            return items.Count > 0;
        }

        public static string EncodeString(string value, bool cache)
        {
#if !NET35
            var items = new Buffer<EncodeData>(stackalloc EncodeData[32]);
#else
            var items = new Buffer<EncodeData>();
#endif

            try
            {
                // Try to find any decode items in the string.
                if (!TryFindEncodeItems(value, ref items))
                {
                    // No encode items found. Just return the original string.
                    return value;
                }

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
