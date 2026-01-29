// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Buffers;

namespace Microsoft.Build.Framework.Utilities;

internal static partial class EscapingUtilities
{
    private static readonly SearchValues<char> s_searchValues = SearchValues.Create(s_specialChars);

    private static unsafe string Decode(string value, ReadOnlySpan<EscapeSequence> escapes, bool trim)
    {
        return string.Empty;
    }

    private static unsafe string Decode(string value, int firstPercentIndex, bool trim)
    {
        ReadOnlySpan<char> source = value.AsSpan();
        int start = 0;
        int length = source.Length;

        if (trim)
        {
            ReadOnlySpan<char> startTrimmed = source.TrimStart();
            start = source.Length - startTrimmed.Length;
            source = startTrimmed.TrimEnd();
            length = source.Length;

            if (source.IsEmpty)
            {
                return string.Empty;
            }

            // Adjust firstPercentIndex to be relative to the trimmed span
            firstPercentIndex -= start;
        }

        const int MaxStackAlloc = 256;

        using BufferScope<char> destination = length <= MaxStackAlloc
            ? new(new char[MaxStackAlloc])
            : new(length);

        // Single pass: decode and copy characters
        int destPos = 0;
        int percentIndex = firstPercentIndex;

        do
        {
            // Copy characters before the next '%'.
            source[..percentIndex].CopyTo(destination[destPos..]);
            source = source[percentIndex..];
            destPos += percentIndex;

            if (source is ['%', char c1, char c2, ..] &&
                TryDecodeHexDigits(c1, c2, out char decoded))
            {
                // Valid escape sequence found, decode it
                destination[destPos++] = decoded;
                source = source[3..]; // Move past the escape sequence
            }
            else
            {
                // Not a valid escape sequence, copy the '%'
                destination[destPos++] = '%';
                source = source[1..];
            }

            percentIndex = source.IndexOf('%');
        }
        while (percentIndex >= 0);

        if (!source.IsEmpty)
        {
            // Copy remaining characters.
            source.CopyTo(destination[destPos..]);
            destPos += source.Length;
        }

        // If no decoding happened (destPos == length), return original string (potentially trimmed)
        if (destPos == length)
        {
            return trim ? value.Substring(start, length) : value;
        }

        // Create result string from buffer
        return new string(destination[..destPos]);
    }

    private static int CountEscapes(string value)
    {
        ReadOnlySpan<char> source = value.AsSpan();
        int count = 0;

        while (!source.IsEmpty)
        {
            int percentIndex = source.IndexOf('%');
            if (percentIndex < 0)
            {
                break;
            }

            source = source[(percentIndex + 1)..];

            if (source is [char c1, char c2, ..] &&
                HexConverter.IsHexChar(c1) &&
                HexConverter.IsHexChar(c2))
            {
                count++;
                source = source[2..];
            }
        }

        return count;
    }

    private static bool TryFindFirstEscape(string value, out int index, out char decoded)
    {
        int startIndex = 0;
        int endIndex = value.Length - 2;
        int percentIndex;

        while ((percentIndex = value.IndexOf('%', startIndex)) >= 0)
        {
            if (percentIndex < endIndex &&
                TryDecodeHexDigits(value[percentIndex + 1], value[percentIndex + 2], out decoded))
            {
                index = percentIndex;
                return true;
            }

            startIndex = percentIndex + 1;
        }

        index = default;
        decoded = default;
        return false;
    }

    private static void CollectEscapes(string value, int startIndex, ref RefArrayBuilder<EscapeSequence> builder)
    {
        int endIndex = value.Length - 2;
        int percentIndex;

        while ((percentIndex = value.IndexOf('%', startIndex)) >= 0)
        {
            if (percentIndex < endIndex &&
                TryDecodeHexDigits(value[percentIndex + 1], value[percentIndex + 2], out char decoded))
            {
                builder.Add(new(percentIndex, decoded));
            }

            startIndex = percentIndex + 1;
        }
    }

    private static bool CollectEscapes(string value, ref RefArrayBuilder<EscapeSequence> builder)
    {
        int startIndex = 0;
        int endIndex = value.Length - 2;
        int percentIndex;

        while ((percentIndex = value.IndexOf('%', startIndex)) >= 0)
        {
            if (percentIndex < endIndex &&
                TryDecodeHexDigits(value[percentIndex + 1], value[percentIndex + 2], out char decoded))
            {
                builder.Add(new(percentIndex, decoded));
            }

            startIndex = percentIndex + 1;
        }

        return builder.Count > 0;
    }

    private static bool CollectEncodeIndices(string value, ref RefArrayBuilder<int> builder)
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

            builder.Add(index);

            // Move past the special character
            start = index + 1;
            source = source[(charIndex + 1)..];
        }

        return builder.Count > 0;
    }

    private static unsafe void Encode(string source, string destination, ReadOnlySpan<int> indices)
    {
        fixed (char* dstPtr = destination)
        {
            EncodeCore(source, new Span<char>(dstPtr, destination.Length), indices);
        }

        static void EncodeCore(ReadOnlySpan<char> source, Span<char> destination, ReadOnlySpan<int> indices)
        {
            int sourcePos = 0;

            foreach (int index in indices)
            {
                // Copy normal characters.
                int copyLength = index - sourcePos;
                if (copyLength > 0)
                {
                    source[sourcePos..index].CopyTo(destination);
                    destination = destination[copyLength..];
                }

                // Write escape sequence.
                s_escapeSequenceTable[source[index]].CopyTo(destination);
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
    }

    private static bool ContainsEscapedWildcardsCore(ReadOnlySpan<char> source)
    {
        while (!source.IsEmpty)
        {
            int percentIndex = source.IndexOf('%');
            if (percentIndex < 0)
            {
                return false;
            }

            source = source[(percentIndex + 1)..];

            if (source is ['2', ('a' or 'A'), ..] or ['3', ('f' or 'F'), ..])
            {
                return true;
            }
        }

        return false;
    }
}
#endif
