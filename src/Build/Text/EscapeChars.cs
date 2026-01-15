// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Text;

internal static class EscapeChars
{
    // MSBuild's escape characters all fall within the ASCII range 36 ('$') to 64 ('@'), allowing us to small lookup tables.
    // Note that that we need index 64, so the table size needs to be 65.
    private const int TableSize = 65;

    private static readonly char[] s_escapeChars =
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

    private static readonly bool[] s_escapeCharTable = BuildEscapeCharTable();

    private static readonly ReadOnlyMemory<char>[] s_escapeSequenceTable = BuildEscapeSequenceTable();

    private static bool[] BuildEscapeCharTable()
    {
        bool[] result = new bool[TableSize];

        foreach (char c in s_escapeChars)
        {
            result[c] = true;
        }

        return result;
    }

    private static ReadOnlyMemory<char>[] BuildEscapeSequenceTable()
    {
        ReadOnlyMemory<char>[] result = new ReadOnlyMemory<char>[TableSize];

        foreach (char c in s_escapeChars)
        {
            result[c] = CreateEscapeSequence(c);
        }

        return result;

        static ReadOnlyMemory<char> CreateEscapeSequence(char c)
            => $"%{HexDigitChar(c / 0x10)}{HexDigitChar(c & 0x0F)}".AsMemory();

        static char HexDigitChar(int x) => (char)(x + (x < 10 ? '0' : ('a' - 10)));
    }

    public static bool ShouldEscape(char c)
        => c < 64 && s_escapeCharTable[c];

    public static bool TryGetEscapeSequence(char c, out ReadOnlySpan<char> escapeSequence)
    {
        if (c < 64 && s_escapeCharTable[c])
        {
            escapeSequence = s_escapeSequenceTable[c].Span;
            return escapeSequence.Length > 0;
        }

        escapeSequence = default;
        return false;
    }
}
