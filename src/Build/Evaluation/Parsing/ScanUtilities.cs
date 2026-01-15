// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Parsing;

internal static class ScanUtilities
{
    public static bool TryFindClosingParenthesis(ReadOnlySpan<char> span, out int result)
        => TryFindClosingParenthesis(span, out result, out _, out _);

    public static bool TryFindClosingParenthesis(
        ReadOnlySpan<char> span,
        out int result,
        out bool potentialPropertyFunction,
        out bool potentialRegistryFunction)
    {
        int nestLevel = 1;
        potentialPropertyFunction = false;
        potentialRegistryFunction = false;

        int index = 0;
        int length = span.Length;

        while (index < length && nestLevel > 0)
        {
            char ch = span[index];

            switch (ch)
            {
                case '(':
                    nestLevel++;
                    break;

                case ')':
                    nestLevel--;
                    break;

                case '\'' or '`' or '"':
                    index++;

                    int quoteIndex = span[index..].IndexOf(ch);
                    if (quoteIndex < 0)
                    {
                        result = 0;
                        return false;
                    }

                    index += quoteIndex;

                    break;

                case '.' or '[' or '$':
                    potentialPropertyFunction = true;
                    break;

                case ':':
                    potentialRegistryFunction = true;
                    break;
            }

            index++;
        }

        if (nestLevel == 0)
        {
            // We will have scanned past the last ')', so step back on character.
            result = index - 1;
            return true;
        }

        result = 0;
        return false;
    }
}
