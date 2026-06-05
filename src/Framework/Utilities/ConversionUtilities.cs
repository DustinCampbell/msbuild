// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
    /// <param name="parameterValue">The string to convert.</param>
    /// <returns>
    ///  Boolean true or false, corresponding to the string.
    /// </returns>
    public static bool ConvertStringToBool(string? parameterValue)
    {
        if (ValidBooleanTrue(parameterValue))
        {
            return true;
        }
        else if (ValidBooleanFalse(parameterValue))
        {
            return false;
        }

        // Unsupported boolean representation.
        throw new ArgumentException(SR.FormatCannotConvertStringToBool(parameterValue));
    }

    public static bool ConvertStringToBool(string? parameterValue, bool nullOrWhitespaceIsFalse)
        => (!nullOrWhitespaceIsFalse || !parameterValue.IsNullOrWhiteSpace()) && ConvertStringToBool(parameterValue);

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

    public static bool TryConvertStringToBool(string? parameterValue, out bool boolValue)
    {
        if (ValidBooleanTrue(parameterValue))
        {
            boolValue = true;
            return true;
        }

        boolValue = false;
        return ValidBooleanFalse(parameterValue);
    }

    public static bool TryConvertStringToBool(ReadOnlySpan<char> parameterValue, out bool boolValue)
    {
        if (ValidBooleanTrue(parameterValue))
        {
            boolValue = true;
            return true;
        }

        boolValue = false;
        return ValidBooleanFalse(parameterValue);
    }

    /// <summary>
    ///  Returns true if the string can be successfully converted to a bool,
    ///  such as "on" or "yes".
    /// </summary>
    public static bool CanConvertStringToBool(string? parameterValue)
        => ValidBooleanTrue(parameterValue) || ValidBooleanFalse(parameterValue);

    /// <summary>
    ///  Returns true if the segment can be successfully converted to a bool,
    ///  such as "on" or "yes".
    /// </summary>
    public static bool CanConvertStringToBool(StringSegment parameterValue)
        => ValidBooleanTrue(parameterValue) || ValidBooleanFalse(parameterValue);

    /// <summary>
    ///  Returns true if the span can be successfully converted to a bool,
    ///  such as "on" or "yes".
    /// </summary>
    public static bool CanConvertStringToBool(ReadOnlySpan<char> parameterValue)
        => ValidBooleanTrue(parameterValue) || ValidBooleanFalse(parameterValue);

    /// <summary>
    ///  Returns true if the string represents a valid MSBuild boolean true value,
    ///  such as "on", "!false", "yes".
    /// </summary>
    public static bool ValidBooleanTrue(string? parameterValue)
        => string.Equals(parameterValue, "true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "on", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "yes", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "!false", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "!off", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "!no", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the segment represents a valid MSBuild boolean true value,
    ///  such as "on", "!false", "yes".
    /// </summary>
    public static bool ValidBooleanTrue(StringSegment parameterValue)
        => parameterValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("on", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!false", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!off", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!no", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the span represents a valid MSBuild boolean true value,
    ///  such as "on", "!false", "yes".
    /// </summary>
    public static bool ValidBooleanTrue(ReadOnlySpan<char> parameterValue)
        => parameterValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("on", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!false", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!off", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!no", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the string represents a valid MSBuild boolean false value,
    ///  such as "!on" "off" "no" "!true".
    /// </summary>
    public static bool ValidBooleanFalse(string? parameterValue)
        => string.Equals(parameterValue, "false", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "off", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "no", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "!true", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "!on", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(parameterValue, "!yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the segment represents a valid MSBuild boolean false value,
    ///  such as "!on" "off" "no" "!true".
    /// </summary>
    public static bool ValidBooleanFalse(StringSegment parameterValue)
        => parameterValue.Equals("false", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("off", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("no", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!true", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!on", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Returns true if the span represents a valid MSBuild boolean false value,
    ///  such as "!on" "off" "no" "!true".
    /// </summary>
    public static bool ValidBooleanFalse(ReadOnlySpan<char> parameterValue)
        => parameterValue.Equals("false", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("off", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("no", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!true", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!on", StringComparison.OrdinalIgnoreCase) ||
           parameterValue.Equals("!yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Converts a string like "123.456" into a double. Leading sign is allowed.
    /// </summary>
    public static double ConvertDecimalToDouble(string number)
        => double.Parse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat);

    /// <summary>
    /// Converts a hex string like "0xABC" into a double.
    /// </summary>
    public static double ConvertHexToDouble(string number)
    {
        return (double)int.Parse(
#if NET
            number.AsSpan(2),
#else
            number.Substring(2),
#endif
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture.NumberFormat);
    }

    /// <summary>
    /// Converts a string like "123.456" or "0xABC" into a double.
    /// Tries decimal conversion first.
    /// </summary>
    public static double ConvertDecimalOrHexToDouble(string number)
    {
        if (TryConvertDecimalOrHexToDouble(number, out double result))
        {
            return result;
        }

        return Assumed.Unreachable<double>("Cannot numeric evaluate");
    }

    public static bool TryConvertDecimalOrHexToDouble(string number, out double doubleValue)
    {
        if (ValidDecimalNumber(number, out doubleValue))
        {
            return true;
        }

        if (ValidHexNumber(number, out int hexValue))
        {
            doubleValue = hexValue;
            return true;
        }

        return false;
    }

#if NET
    public static bool TryConvertDecimalOrHexToDouble(ReadOnlySpan<char> number, out double doubleValue)
    {
        if (ValidDecimalNumber(number, out doubleValue))
        {
            return true;
        }

        if (ValidHexNumber(number, out int hexValue))
        {
            doubleValue = hexValue;
            return true;
        }

        return false;
    }
#endif

    /// <summary>
    ///  Returns true if the string is a valid hex number, like "0xABC".
    /// </summary>
    private static bool ValidHexNumber(string number, out int value)
    {
#if NET
        return ValidHexNumber(number.AsSpan(), out value);
#else
        if (number.Length >= 3 && number[0] is '0' && number[1] is 'x' or 'X')
        {
            return int.TryParse(
                number.Substring(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture.NumberFormat,
                out value);
        }

        value = 0;
        return false;
#endif
    }

#if NET
    /// <summary>
    ///  Returns true if the string is a valid hex number, like "0xABC".
    /// </summary>
    private static bool ValidHexNumber(ReadOnlySpan<char> number, out int value)
    {
        if (number.Length >= 3 && number is ['0', ('x' or 'X'), .. var digits])
        {
            return int.TryParse(
                digits,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture.NumberFormat,
                out value);
        }

        value = 0;
        return false;
    }

#endif

    /// <summary>
    ///  Returns true if the string is a valid decimal number, like "-123.456".
    /// </summary>
    private static bool ValidDecimalNumber(string number, out double value)
        => double.TryParse(number, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture.NumberFormat, out value)
        && !double.IsInfinity(value);

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
