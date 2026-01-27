// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal static class LexingUtilities
{
    public static bool IsDecimalNumberStartChar(char ch)
        => ch is '+' or '-' or '.' || char.IsDigit(ch);

    public static bool IsIdentifierStartChar(char ch)
        => ch == '_' || char.IsLetter(ch);

    public static bool IsIdentifierChar(char ch)
        => IsIdentifierStartChar(ch) || char.IsDigit(ch);

    public static bool IsHexDigit(char ch)
        => char.IsDigit(ch) || ((uint)((ch | 0x20) - 'a') <= 'f' - 'a');

    public static bool TryLexDecimalNumber(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
    {
        if (span.IsEmpty || !IsDecimalNumberStartChar(span[0]))
        {
            result = default;
            return false;
        }

        int index = 1;
        bool dotSeen = false;

        while (index < span.Length)
        {
            char ch = span[index];

            if (ch == '.')
            {
                // Don't allow more than one '.' in a row.
                if (dotSeen)
                {
                    break;
                }

                dotSeen = true;
            }
            else if (char.IsDigit(ch))
            {
                dotSeen = false;
            }
            else
            {
                // Not a '.' or digit, so we're done.
                break;
            }

            index++;
        }

        result = span[..index];
        return true;
    }

    public static bool TryLexHexNumber(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
    {
        if (span is not ['0', 'x' or 'X', char c, ..] || !IsHexDigit(c))
        {
            result = default;
            return false;
        }

        int index = 3;

        while (index < span.Length && IsHexDigit(span[index]))
        {
            index++;
        }

        result = span[..index];
        return true;
    }

    public static bool TryLexIdentifier(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
    {
        if (span.IsEmpty || !IsIdentifierStartChar(span[0]))
        {
            result = default;
            return false;
        }

        int index = 1;

        while (index < span.Length && IsIdentifierChar(span[index]))
        {
            index++;
        }

        result = span[..index];
        return true;
    }

    public static bool TryLexName(ReadOnlySpan<char> span, out ReadOnlySpan<char> result)
    {
        if (span.IsEmpty || !XmlUtilities.IsValidInitialElementNameCharacter(span[0]))
        {
            result = default;
            return false;
        }

        int index = 1;

        while (index < span.Length)
        {
            char ch = span[index];

            // Note: '-' is a valid character, but we shouldn't consume it if it
            // forms a '->'.
            if (!XmlUtilities.IsValidSubsequentElementNameCharacter(ch) ||
                (ch == '-' && index + 1 < span.Length && span[index + 1] == '>'))
            {
                break;
            }

            index++;
        }

        result = span[..index];
        return true;
    }
}
