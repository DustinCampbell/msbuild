// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// Character classification utilities for MSBuild expression parsing.
/// </summary>
internal static class CharacterUtilities
{
    /// <summary>
    ///  Returns <see langword="true"/> if the character can start an identifier (item name, property name, etc.).
    ///  Valid initial characters: "A-Z, a-z, _".
    /// </summary>
    public static bool IsIdentifierStart(char c)
        => XmlUtilities.IsValidInitialElementNameCharacter(c);

    /// <summary>
    ///  Returns <see langword="true"/> if the character can appear in an identifier (after the first character).
    ///  Valid subsequent characters: "A-Z, a-z, 0-9, _, -".
    /// </summary>
    public static bool IsIdentifierChar(char c)
        => XmlUtilities.IsValidSubsequentElementNameCharacter(c);

    /// <summary>
    ///  Returns <see langword="true"/> if the character can start a number.
    /// </summary>
    public static bool IsNumberStart(char c)
        => char.IsDigit(c) || c is '+' or '-' or '.';

    /// <summary>
    ///  Returns <see langword="true"/> if the character is a hexadecimal digit.
    /// </summary>
    public static bool IsHexDigit(char c)
        => char.IsDigit(c) || ((uint)((c | 0x20) - 'a') <= 'f' - 'a');

    /// <summary>
    /// Attempts to decode the specified character as a hexadecimal digit.
    /// </summary>
    /// <param name="c">
    ///  The character to decode. Must be a valid hexadecimal digit ('0'-'9', 'A'-'F', or 'a'-'f').
    /// </param>
    /// <param name="value">
    ///  When this method returns, contains the integer value of the hexadecimal digit if decoding succeeds.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the character is a valid hexadecimal digit and was successfully decoded;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryDecodeHexDigit(char c, out int value)
    {
        switch (c)
        {
            case >= '0' and <= '9':
                value = c - '0';
                return true;

            case >= 'A' and <= 'F':
                value = c - 'A' + 10;
                return true;

            case >= 'a' and <= 'f':
                value = c - 'a' + 10;
                return true;

            default:
                value = default;
                return false;
        }
    }
}
