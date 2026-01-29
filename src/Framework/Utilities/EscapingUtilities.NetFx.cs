// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Diagnostics;

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace Microsoft.Build.Framework.Utilities;

internal static partial class EscapingUtilities
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

    private static unsafe string Decode(string value, ReadOnlySpan<EscapeSequence> escapes, bool trim)
    {
        Debug.Assert(!escapes.IsEmpty, "Should have at least one escape sequence to decode.");

        fixed (char* srcPtr = value)
        {
            int start = 0;
            int length = value.Length;

            if (trim)
            {
                AdjustStartAndLength(srcPtr, ref start, ref length);

                if (start == length)
                {
                    return string.Empty;
                }
            }

            int resultLength = length - (escapes.Length * 2);
            string result = new('\0', resultLength);

            fixed (char* dstPtr = result)
            {
                int srcPos = start;
                int end = start + length;
                int dstPos = 0;

                foreach ((int index, char decoded) in escapes)
                {
                    // Bulk copy characters before the next '%'
                    int copyLength = index - srcPos;
                    if (copyLength > 0)
                    {
                        CopyChars(srcPtr + srcPos, dstPtr + dstPos, copyLength);
                        dstPos += copyLength;
                        srcPos += copyLength;
                    }

                    // Copy the decoded character
                    dstPtr[dstPos++] = decoded;
                    srcPos += 3;
                }

                if (srcPos < end)
                {
                    // No more '%', bulk copy remaining
                    int remaining = end - srcPos;
                    CopyChars(srcPtr + srcPos, dstPtr + dstPos, remaining);
                    dstPos += remaining;
                }

                return result;
            }
        }

        static void AdjustStartAndLength(char* srcPtr, ref int start, ref int length)
        {
            while (start < length && char.IsWhiteSpace(srcPtr[start]))
            {
                start++;
            }

            while (length > start && char.IsWhiteSpace(srcPtr[length - 1]))
            {
                length--;
            }

            length -= start;
        }
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

    private static int CountEscapes(string value)
    {
        int count = 0;
        int startIndex = 0;
        int end = value.Length - 2;

        while (startIndex < end)
        {
            int percentIndex = value.IndexOf('%', startIndex);
            if (percentIndex < 0)
            {
                break;
            }

            startIndex = percentIndex + 1;

            if (startIndex <= end &&
                HexConverter.IsHexChar(value[startIndex]) &&
                HexConverter.IsHexChar(value[startIndex + 1]))
            {
                count++;
                startIndex += 2;
            }
        }

        return count;
    }

    private static unsafe void Decode(string source, int start, int length, string destination, ReadOnlySpan<int> indices)
    {
        fixed (char* srcPtr = source)
        fixed (char* dstPtr = destination)
        {
            int srcPos = start;
            int dstPos = 0;

            foreach (int index in indices)
            {
                // Copy characters before the escape sequence.
                int copyLength = index - srcPos;
                if (copyLength > 0)
                {
                    CopyChars(srcPtr + srcPos, dstPtr + dstPos, copyLength);
                    dstPos += copyLength;
                }

                // Write decoded character.
                int hi = HexConverter.FromChar(source[index + 1]);
                int lo = HexConverter.FromChar(source[index + 2]);

                dstPtr[dstPos] = (char)((hi << 4) + lo);
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

    private static unsafe bool CollectEncodeIndices(string value, ref RefArrayBuilder<int> builder)
    {
        fixed (char* ptr = value)
        {
            int length = value.Length;

            for (int i = 0; i < length; i++)
            {
                char c = ptr[i];

                if ((uint)(c - '$') <= ('@' - '$') && s_specialCharTable[c])
                {
                    builder.Add(i);
                }
            }
        }

        return builder.Count > 0;
    }

    private static unsafe void Encode(string source, string destination, ReadOnlySpan<int> indices)
    {
        fixed (char* srcPtr = source)
        fixed (char* dstPtr = destination)
        {
            int length = source.Length;

            int srcPos = 0;
            int dstPos = 0;

            foreach (int index in indices)
            {
                // Copy characters before the escape sequence.
                int copyLength = index - srcPos;
                if (copyLength > 0)
                {
                    CopyChars(srcPtr + srcPos, dstPtr + dstPos, copyLength);
                    dstPos += copyLength;
                }

                // Write the escape sequence.
                fixed (char* escapePtr = s_escapeSequenceTable[srcPtr[index]])
                {
                    CopyChars(escapePtr, dstPtr + dstPos, 3);
                    dstPos += 3;
                }

                srcPos = index + 1;
            }

            // Copy any remaining characters after the last escape sequence.
            int remainingLength = length - srcPos;
            if (remainingLength > 0)
            {
                CopyChars(srcPtr + srcPos, dstPtr + dstPos, remainingLength);
            }
        }
    }

    private static unsafe bool ContainsEscapedWildcardsCore(string value)
    {
        int length = value.Length - 2;

        fixed (char* ptr = value)
        {
            for (int i = 0; i < length; i++)
            {
                if (ptr[i] == '%' &&
                    ((ptr[i + 1] is '2' && ptr[i + 2] is 'a' or 'A') ||
                     (ptr[i + 1] is '3' && ptr[i + 2] is 'f' or 'F')))
                {
                    return true;
                }
            }
        }

        return false;
    }


    private static unsafe void CopyChars(char* srcPtr, char* dstPtr, int length)
        => Buffer.MemoryCopy(srcPtr, dstPtr, length * sizeof(char), length * sizeof(char));
}
#endif
