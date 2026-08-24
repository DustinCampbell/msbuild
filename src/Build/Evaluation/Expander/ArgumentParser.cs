// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Converts evaluated property-function argument objects to common destination types.
/// </summary>
internal static class ArgumentParser
{
    /// <summary>
    ///  Tries to convert an evaluated argument to a <see cref="Version"/>.
    /// </summary>
    public static bool TryConvertToVersion(object? value, out Version? result)
    {
        if (value is not string text || string.IsNullOrEmpty(text) || !Version.TryParse(text, out result))
        {
            result = null;
            return false;
        }

        return true;
    }

    /// <summary>
    ///  Tries to convert an evaluated argument to a 32-bit signed integer without losing precision.
    /// </summary>
    public static bool TryConvertToInt(object? value, out int result)
    {
        switch (value)
        {
            case double doubleValue when doubleValue is >= int.MinValue and <= int.MaxValue:
                result = Convert.ToInt32(doubleValue);
                if (Math.Abs(result - doubleValue) == 0)
                {
                    return true;
                }
                break;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                result = Convert.ToInt32(longValue);
                return true;
            case int intValue:
                result = intValue;
                return true;
            case string text when int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture.NumberFormat,
                out result):
                return true;
        }

        result = 0;
        return false;
    }

    /// <summary>
    ///  Tries to convert an evaluated argument to a 64-bit signed integer without losing precision.
    /// </summary>
    public static bool TryConvertToLong(object? value, out long result)
    {
        switch (value)
        {
            case double doubleValue when doubleValue is >= long.MinValue and <= long.MaxValue:
                result = (long)doubleValue;
                if (Math.Abs(result - doubleValue) == 0)
                {
                    return true;
                }
                break;
            case long longValue:
                result = longValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case string text when long.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture.NumberFormat,
                out result):
                return true;
        }

        result = 0;
        return false;
    }

    /// <summary>
    ///  Tries to convert an evaluated argument to a double-precision floating-point value.
    /// </summary>
    public static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double doubleValue:
                result = doubleValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case string text when double.TryParse(
                text,
                NumberStyles.Number | NumberStyles.Float,
                CultureInfo.InvariantCulture.NumberFormat,
                out result):
                return true;
            default:
                result = 0;
                return false;
        }
    }

    /// <summary>
    ///  Determines whether an evaluated argument can be represented as a floating-point value.
    /// </summary>
    public static bool IsFloatingPointRepresentation(object? value)
        => value is double ||
            (value is string text &&
             double.TryParse(
                 text,
                 NumberStyles.Number | NumberStyles.Float,
                 CultureInfo.InvariantCulture.NumberFormat,
                 out _));
}
