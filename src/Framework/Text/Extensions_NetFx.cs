// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
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

    // Currency symbols can precede digits. Leave currency, non-decimal, and invalid styles to the runtime.
    private static bool CanUseIntegerFastPath(NumberStyles style)
        => (style & ~(NumberStyles.Any & ~NumberStyles.AllowCurrencySymbol)) == NumberStyles.None;

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
            !TryParseUInt32Digits(digits, uint.MaxValue, out uint parsed) ||
            (negative && parsed != 0))
        {
            result = 0;
            return false;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseUInt32Digits(StringSegment digits, uint limit, out uint result)
    {
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

    private static bool TryGetIntegerDigits(
        StringSegment value, NumberStyles style, NumberFormatInfo numberFormat, out StringSegment digits, out bool negative)
    {
        digits = default;
        negative = false;

        if (value.IsNullOrEmpty)
        {
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

        if ((style & NumberStyles.AllowLeadingSign) != 0)
        {
            string positiveSign = numberFormat.PositiveSign;
            string negativeSign = numberFormat.NegativeSign;

            // Runtime sign matching has special rules for nulls and non-breaking spaces.
            if (positiveSign.AsSpan().IndexOfAny('\0', '\u00a0', '\u202f') >= 0 ||
                negativeSign.AsSpan().IndexOfAny('\0', '\u00a0', '\u202f') >= 0)
            {
                return false;
            }

            StringSegment remaining = value[start..];
            if (positiveSign.Length > 0 && remaining.StartsWith(positiveSign, StringComparison.Ordinal))
            {
                start += positiveSign.Length;
            }
            else if (negativeSign.Length > 0 && remaining.StartsWith(negativeSign, StringComparison.Ordinal))
            {
                start += negativeSign.Length;
                negative = true;
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

        digits = value[start..end];
        return !digits.IsEmpty;
    }

    private static bool TryParseInt32AsDouble(StringSegment value, NumberStyles style, NumberFormatInfo numberFormat, out double result)
    {
        // Keep signed-zero handling and large-integer rounding in the runtime parser.
        if (TryParseInt32(value, style, numberFormat, out int integer) && integer != 0)
        {
            result = integer;
            return true;
        }

        result = 0;
        return false;
    }
}
#endif
