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

    private static bool IsInvariantProvider(IFormatProvider? provider)
        => ReferenceEquals(provider, CultureInfo.InvariantCulture) ||
           ReferenceEquals(provider, NumberFormatInfo.InvariantInfo);

    private static bool TryGetInvariantIntegerDigits(
        StringSegment value,
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
        while (start < value.Length && value[start] is ' ' or (>= '\t' and <= '\r'))
        {
            start++;
        }

        int end = value.Length;
        while (end > start && value[end - 1] is ' ' or (>= '\t' and <= '\r'))
        {
            end--;
        }

        if (start == end)
        {
            digits = default;
            negative = false;
            return false;
        }

        switch (value[start])
        {
            case '-':
                negative = true;
                digits = value[(start + 1)..end];
                break;

            case '+':
                negative = false;
                digits = value[(start + 1)..end];
                break;

            default:
                negative = false;
                digits = value[start..end];
                break;
        }

        return !digits.IsEmpty;
    }

    private static bool TryParseInvariantInteger(StringSegment value, out int result)
    {
        if (!TryGetInvariantIntegerDigits(value, out StringSegment digits, out bool negative))
        {
            result = 0;
            return false;
        }

        return TryParseInvariantIntegerDigits(digits, negative, out result);
    }

    private static bool TryParseInvariantInteger(StringSegment value, NumberStyles style, out int result)
        => style == NumberStyles.Integer
            ? TryParseInvariantInteger(value, out result)
            : TryParseInvariantIntegerDigits(value, negative: false, out result);

    private static bool TryParseInvariantIntegerDigits(StringSegment digits, bool negative, out int result)
    {
        if (digits.IsNullOrEmpty)
        {
            result = 0;
            return false;
        }

        uint limit = negative ? Int32MaxNegativeMagnitude : int.MaxValue;
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

        result = !negative
            ? (int)parsed
            : parsed == Int32MaxNegativeMagnitude ? int.MinValue : -(int)parsed;

        return true;
    }

    private static bool TryParseInvariantInteger(StringSegment value, out long result)
    {
        if (!TryGetInvariantIntegerDigits(value, out StringSegment digits, out bool negative))
        {
            result = 0;
            return false;
        }

        ulong limit = negative ? Int64MaxNegativeMagnitude : long.MaxValue;
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

        result = !negative
            ? (long)parsed
            : parsed == Int64MaxNegativeMagnitude ? long.MinValue : -(long)parsed;

        return true;
    }

    private static bool TryParseUnsignedIntegerDigits(StringSegment value, out uint result)
    {
        int start = 0;
        while (start < value.Length && value[start] is ' ' or (>= '\t' and <= '\r'))
        {
            start++;
        }

        int end = value.Length;
        while (end > start && value[end - 1] is ' ' or (>= '\t' and <= '\r'))
        {
            end--;
        }

        if (start == end)
        {
            result = 0;
            return false;
        }

        uint maxBeforeMultiply = uint.MaxValue / 10;
        uint maxLastDigit = uint.MaxValue % 10;
        uint parsed = 0;

        for (int i = start; i < end; i++)
        {
            uint digit = (uint)(value[i] - '0');
            if (digit > 9 ||
                parsed > maxBeforeMultiply ||
                (parsed == maxBeforeMultiply && digit > maxLastDigit))
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digit;
        }

        result = parsed;
        return true;
    }

    private static bool TryParseInvariantDouble(StringSegment value, out double result)
    {
        if (TryParseInvariantInteger(value, out long integer))
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
        if (separator < 0 || !TryParseVersionComponent(value[..separator], out major))
        {
            return false;
        }

        int componentStart = separator + 1;
        separator = value.IndexOf('.', componentStart);
        if (separator < 0)
        {
            if (!TryParseVersionComponent(value[componentStart..], out minor))
            {
                return false;
            }

            result = new Version(major, minor);
            return true;
        }

        if (!TryParseVersionComponent(value[componentStart..separator], out minor))
        {
            return false;
        }

        componentStart = separator + 1;
        separator = value.IndexOf('.', componentStart);
        if (separator < 0)
        {
            if (!TryParseVersionComponent(value[componentStart..], out build))
            {
                return false;
            }

            result = new Version(major, minor, build);
            return true;
        }

        if (!TryParseVersionComponent(value[componentStart..separator], out build) ||
            !TryParseVersionComponent(value[(separator + 1)..], out revision))
        {
            return false;
        }

        result = new Version(major, minor, build, revision);
        return true;
    }

    private static bool TryParseVersionComponent(StringSegment component, out int result)
        => TryParseInvariantInteger(component, out result) && result >= 0;
}
#endif
