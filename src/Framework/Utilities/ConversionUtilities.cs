// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Build.Text;
#if !NET
using System.Text;
#endif

namespace Microsoft.Build.Shared;

/// <summary>
///  This class contains only static methods, which are useful throughout many
///  of the MSBuild classes and don't really belong in any specific class.
/// </summary>
internal static class ConversionUtilities
{
    /// <summary>
    ///  Converts a string to a bool.  We consider "true/false", "on/off", and
    ///  "yes/no" to be valid boolean representations in the XML.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    ///  Boolean true or false, corresponding to the string.
    /// </returns>
    public static bool ConvertStringToBool([NotNullWhen(true)] string? value)
    {
        if (ValidBooleanTrue(value))
        {
            return true;
        }

        if (ValidBooleanFalse(value))
        {
            return false;
        }

        // Unsupported boolean representation.
        throw new ArgumentException(SR.FormatCannotConvertStringToBool(value));
    }

    /// <summary>
    ///  Converts a <see cref="StringSegment"/> to a bool.  We consider "true/false", "on/off", and
    ///  "yes/no" to be valid boolean representations in the XML.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    ///  Boolean true or false, corresponding to the string.
    /// </returns>
    public static bool ConvertStringToBool(StringSegment value)
    {
        if (ValidBooleanTrue(value))
        {
            return true;
        }

        if (ValidBooleanFalse(value))
        {
            return false;
        }

        // Unsupported boolean representation.
        throw new ArgumentException(SR.FormatCannotConvertStringToBool(value.ToString()));
    }

    public static bool ConvertStringToBool([NotNullWhen(true)] string? value, bool nullOrWhitespaceIsFalse)
        => (!nullOrWhitespaceIsFalse || !value.IsNullOrWhiteSpace()) && ConvertStringToBool(value);

    /// <summary>
    ///  Returns a hex representation of a byte array.
    /// </summary>
    /// <param name="bytes">The bytes to convert</param>
    /// <returns>
    ///  A string byte types formated as X2.
    /// </returns>
    public static string ConvertByteArrayToHex(byte[] bytes)
    {
#if NET
        return Convert.ToHexString(bytes);
#else
        var sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.AppendFormat("{0:X2}", b);
        }

        return sb.ToString();
#endif
    }

    public static bool TryConvertStringToBool([NotNullWhen(true)] string? value, out bool result)
    {
        if (ValidBooleanTrue(value))
        {
            result = true;
            return true;
        }

        result = false;
        return ValidBooleanFalse(value);
    }

    public static bool TryConvertStringToBool(StringSegment value, out bool result)
    {
        if (ValidBooleanTrue(value))
        {
            result = true;
            return true;
        }

        result = false;
        return ValidBooleanFalse(value);
    }

    public static bool TryConvertStringToBool(ReadOnlySpan<char> value, out bool result)
    {
        if (ValidBooleanTrue(value))
        {
            result = true;
            return true;
        }

        result = false;
        return ValidBooleanFalse(value);
    }

    /// <summary>
    ///  Returns true if the string can be successfully converted to a bool,
    ///  such as "on" or "yes".
    /// </summary>
    public static bool CanConvertStringToBool([NotNullWhen(true)] string? value)
        => ValidBooleanTrue(value) || ValidBooleanFalse(value);

    /// <summary>
    ///  Returns true if the segment can be successfully converted to a bool,
    ///  such as "on" or "yes".
    /// </summary>
    public static bool CanConvertStringToBool(StringSegment value)
        => ValidBooleanTrue(value) || ValidBooleanFalse(value);

    /// <summary>
    ///  Returns true if the span can be successfully converted to a bool,
    ///  such as "on" or "yes".
    /// </summary>
    public static bool CanConvertStringToBool(ReadOnlySpan<char> value)
        => ValidBooleanTrue(value) || ValidBooleanFalse(value);

    /// <summary>
    ///  Returns true if the string represents a valid MSBuild boolean true value,
    ///  such as "on", "!false", "yes".
    /// </summary>
    public static bool ValidBooleanTrue([NotNullWhen(true)] string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "!false", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "!off", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "!no", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the segment represents a valid MSBuild boolean true value,
    ///  such as "on", "!false", "yes".
    /// </summary>
    public static bool ValidBooleanTrue(StringSegment value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!false", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!off", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!no", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the span represents a valid MSBuild boolean true value,
    ///  such as "on", "!false", "yes".
    /// </summary>
    public static bool ValidBooleanTrue(ReadOnlySpan<char> value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!false", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!off", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!no", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the string represents a valid MSBuild boolean false value,
    ///  such as "!on" "off" "no" "!true".
    /// </summary>
    public static bool ValidBooleanFalse([NotNullWhen(true)] string? value)
        => string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "no", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "!true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "!on", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(value, "!yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the segment represents a valid MSBuild boolean false value,
    ///  such as "!on" "off" "no" "!true".
    /// </summary>
    public static bool ValidBooleanFalse(StringSegment value)
        => value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!on", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the span represents a valid MSBuild boolean false value,
    ///  such as "!on" "off" "no" "!true".
    /// </summary>
    public static bool ValidBooleanFalse(ReadOnlySpan<char> value)
        => value.Equals("false", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!on", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("!yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Converts a string like "123.456" into a double. Leading sign is allowed.
    /// </summary>
    public static double ConvertDecimalToDouble(string number)
        => double.Parse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat);

    /// <summary>
    /// Converts a hex string like "0xABC" into a double.
    /// </summary>
    public static double ConvertHexToDouble(string number)
        => int.Parse(
#if NET
            number.AsSpan(2),
#else
            number.Substring(2),
#endif
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture.NumberFormat);

    /// <summary>
    /// Converts a string like "123.456" or "0xABC" into a double.
    /// Tries decimal conversion first.
    /// </summary>
    public static double ConvertDecimalOrHexToDouble(string number)
        => TryConvertDecimalOrHexToDouble(number, out double result)
            ? result
            : Assumed.Unreachable<double>("Cannot numeric evaluate");

    public static bool TryConvertDecimalOrHexToDouble(string number, out double result)
    {
        if (ValidDecimalNumber(number, out result))
        {
            return true;
        }

        if (ValidHexNumber(number, out int hexValue))
        {
            result = hexValue;
            return true;
        }

        return false;
    }

    public static bool TryConvertDecimalOrHexToDouble(StringSegment number, out double result)
    {
        if (ValidDecimalNumber(number, out result))
        {
            return true;
        }

        if (ValidHexNumber(number, out int hexValue))
        {
            result = hexValue;
            return true;
        }

        return false;
    }

#if NET
    public static bool TryConvertDecimalOrHexToDouble(ReadOnlySpan<char> number, out double result)
    {
        if (ValidDecimalNumber(number, out result))
        {
            return true;
        }

        if (ValidHexNumber(number, out int hexValue))
        {
            result = hexValue;
            return true;
        }

        return false;
    }
#endif

    /// <summary>
    ///  Returns true if the string is a valid hex number, like "0xABC".
    /// </summary>
    private static bool ValidHexNumber(string number, out int result)
    {
#if NET
        return ValidHexNumber(number.AsSpan(), out result);
#else
        if (number.Length >= 3 && number[0] is '0' && number[1] is 'x' or 'X')
        {
            return int.TryParse(
                number.Substring(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture.NumberFormat,
                out result);
        }

        result = 0;
        return false;
#endif
    }

    /// <summary>
    ///  Returns true if the string is a valid hex number, like "0xABC".
    /// </summary>
    private static bool ValidHexNumber(StringSegment number, out int result)
    {
#if NET
        return ValidHexNumber(number.AsSpan(), out result);
#else
        if (number.Length >= 3 && number is ['0', 'x' or 'X', .. var digits])
        {
            return int.TryParse(
                digits.ToString(),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture.NumberFormat,
                out result);
        }

        result = 0;
        return false;
#endif
    }

#if NET
    /// <summary>
    ///  Returns true if the string is a valid hex number, like "0xABC".
    /// </summary>
    private static bool ValidHexNumber(ReadOnlySpan<char> number, out int result)
    {
        if (number.Length >= 3 && number is ['0', ('x' or 'X'), .. var digits])
        {
            return int.TryParse(
                digits,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture.NumberFormat,
                out result);
        }

        result = 0;
        return false;
    }

#endif

    /// <summary>
    ///  Returns true if the string is a valid decimal number, like "-123.456".
    /// </summary>
    private static bool ValidDecimalNumber(string number, out double result)
        => double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat, out result)
        && !double.IsInfinity(result);

    /// <summary>
    ///  Returns true if the string is a valid decimal number, like "-123.456".
    /// </summary>
    private static bool ValidDecimalNumber(StringSegment number, out double result)
        => double.TryParse(
#if NET
            number.AsSpan(),
#else
            number.ToString(),
#endif
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture.NumberFormat,
            out result)
        && !double.IsInfinity(result);

#if NET
    /// <summary>
    ///  Returns true if the string is a valid decimal number, like "-123.456".
    /// </summary>
    private static bool ValidDecimalNumber(ReadOnlySpan<char> number, out double value)
        => double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat, out value)
        && !double.IsInfinity(value);
#endif

    /// <summary>
    ///  Returns true if the string is a valid decimal or hex number.
    /// </summary>
    public static bool ValidDecimalOrHexNumber(string number)
        => ValidDecimalNumber(number, out _) || ValidHexNumber(number, out _);
}
