// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Build.Text;

/// <summary>
///  Extension methods for working with <see cref="StringSegment"/> values.
/// </summary>
internal static partial class Extensions
{
    /// <summary>
    ///  The maximum magnitude accepted after a negative sign, which is one greater than
    ///  <see cref="int.MaxValue"/>.
    /// </summary>
    private const uint Int32MaxNegativeMagnitude = 1u << 31;

    /// <summary>
    ///  The maximum magnitude accepted after a negative sign, which is one greater than
    ///  <see cref="long.MaxValue"/>.
    /// </summary>
    private const ulong Int64MaxNegativeMagnitude = 1UL << 63;

    // Hexadecimal changes digit-only values; the second check rejects undefined flags.
    private static bool CanUseIntegerFastPath(NumberStyles style)
        => (style & NumberStyles.AllowHexSpecifier) == NumberStyles.None &&
           (style & ~(NumberStyles.Any | NumberStyles.AllowHexSpecifier)) == NumberStyles.None;

    private static bool TryGetIntegerDigits(
        StringSegment value,
        NumberStyles style,
        NumberFormatInfo numberFormat,
        out StringSegment digits,
        out bool negative)
    {
        if (value.IsNullOrEmpty)
        {
            digits = default;
            negative = false;
            return false;
        }

        int start = 0;
        if ((style & NumberStyles.AllowLeadingWhite) != 0)
        {
            while (start < value.Length && value[start] is ' ' or (>= '\t' and <= '\r'))
            {
                start++;
            }
        }

        int end = value.Length;
        if ((style & NumberStyles.AllowTrailingWhite) != 0)
        {
            while (end > start && value[end - 1] is ' ' or (>= '\t' and <= '\r'))
            {
                end--;
            }
        }

        if (start == end)
        {
            digits = default;
            negative = false;
            return false;
        }

        digits = value[start..end];
        negative = false;

        if ((style & NumberStyles.AllowLeadingSign) != 0)
        {
            if (numberFormat.PositiveSign is { Length: > 0 } positiveSign &&
                digits.StartsWith(positiveSign, StringComparison.Ordinal))
            {
                digits = digits[positiveSign.Length..];
            }
            else if (numberFormat.NegativeSign is { Length: > 0 } negativeSign &&
                     digits.StartsWith(negativeSign, StringComparison.Ordinal))
            {
                digits = digits[negativeSign.Length..];
                negative = true;
            }
        }

        return !digits.IsEmpty;
    }

    private static bool TryParseInt32(StringSegment value, NumberStyles style, NumberFormatInfo numberFormat, out int result)
    {
        if (!TryGetIntegerDigits(value, style, numberFormat, out StringSegment digits, out bool negative))
        {
            result = 0;
            return false;
        }

        uint limit = negative ? Int32MaxNegativeMagnitude : int.MaxValue;
        if (!TryParseUInt32Digits(digits, limit, out uint parsed))
        {
            result = 0;
            return false;
        }

        result = !negative
            ? (int)parsed
            : parsed == Int32MaxNegativeMagnitude ? int.MinValue : -(int)parsed;

        return true;
    }

    private static bool TryParseInt64(StringSegment value, NumberStyles style, NumberFormatInfo numberFormat, out long result)
    {
        if (!TryGetIntegerDigits(value, style, numberFormat, out StringSegment digits, out bool negative))
        {
            result = 0;
            return false;
        }

        ulong limit = negative ? Int64MaxNegativeMagnitude : long.MaxValue;
        if (!TryParseUInt64Digits(digits, limit, out ulong parsed))
        {
            result = 0;
            return false;
        }

        result = !negative
            ? (long)parsed
            : parsed == Int64MaxNegativeMagnitude ? long.MinValue : -(long)parsed;

        return true;
    }

    private static bool TryParseUInt32(StringSegment value, NumberStyles style, NumberFormatInfo numberFormat, out uint result)
    {
        if (!TryGetIntegerDigits(value, style, numberFormat, out StringSegment digits, out bool negative) ||
            negative ||
            !TryParseUInt32Digits(digits, uint.MaxValue, out uint parsed))
        {
            result = 0;
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseUInt32Digits(StringSegment digits, uint limit, out uint result)
    {
        if (digits.IsNullOrEmpty)
        {
            result = 0;
            return false;
        }

        uint maxBeforeMultiply = limit / 10;
        uint maxLastDigit = limit % 10;
        uint parsed = 0;

        foreach (char digit in digits)
        {
            uint digitValue = (uint)(digit - '0');
            if (digitValue > 9 ||
                parsed > maxBeforeMultiply ||
                (parsed == maxBeforeMultiply && digitValue > maxLastDigit))
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digitValue;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseUInt64Digits(StringSegment digits, ulong limit, out ulong result)
    {
        if (digits.IsNullOrEmpty)
        {
            result = 0;
            return false;
        }

        ulong maxBeforeMultiply = limit / 10;
        ulong maxLastDigit = limit % 10;
        ulong parsed = 0;

        foreach (char digit in digits)
        {
            ulong digitValue = (uint)(digit - '0');
            if (digitValue > 9 ||
                parsed > maxBeforeMultiply ||
                (parsed == maxBeforeMultiply && digitValue > maxLastDigit))
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digitValue;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseInt64AsDouble(
        StringSegment value,
        NumberStyles style,
        NumberFormatInfo numberFormat,
        out double result)
    {
        if (TryParseInt64(value, style, numberFormat, out long integer))
        {
            result = integer;
            return true;
        }

        result = 0;
        return false;
    }

    private static bool TryParseVersion(StringSegment value, [NotNullWhen(true)] out Version? result)
    {
        result = null;
        int major, minor, build, revision;

        int separator = value.IndexOf('.');
        if (separator < 0 || !TryParseComponent(value[..separator], out major))
        {
            return false;
        }

        int componentStart = separator + 1;
        separator = value.IndexOf('.', componentStart);
        if (separator < 0)
        {
            if (!TryParseComponent(value[componentStart..], out minor))
            {
                return false;
            }

            result = new Version(major, minor);
            return true;
        }

        if (!TryParseComponent(value[componentStart..separator], out minor))
        {
            return false;
        }

        componentStart = separator + 1;
        separator = value.IndexOf('.', componentStart);
        if (separator < 0)
        {
            if (!TryParseComponent(value[componentStart..], out build))
            {
                return false;
            }

            result = new Version(major, minor, build);
            return true;
        }

        if (!TryParseComponent(value[componentStart..separator], out build) ||
            !TryParseComponent(value[(separator + 1)..], out revision))
        {
            return false;
        }

        result = new Version(major, minor, build, revision);
        return true;

        static bool TryParseComponent(StringSegment component, out int result)
            => TryParseInt32(component, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out result) && result >= 0;
    }
}
#endif
