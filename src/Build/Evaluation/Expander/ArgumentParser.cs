// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static class ArgumentParser
{
    public static bool TryGetArg(object?[] args, [NotNullWhen(true)] out string? arg0)
    {
        if (args is [string value0])
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        return false;
    }

    public static bool TryGetArg(object?[] args, out int arg0)
    {
        if (args is [var value0] &&
            TryConvertToInt(value0, out arg0))
        {
            return true;
        }

        arg0 = default;
        return false;
    }

    public static bool TryGetArg(object?[] args, out Version? arg0)
    {
        if (args is [var value0] &&
            TryConvertToVersion(value0, out arg0))
        {
            return true;
        }

        arg0 = null;
        return false;
    }

    public static bool TryGetArgs(object?[] args, [NotNullWhen(true)] out string? arg0, [NotNullWhen(true)] out string? arg1)
    {
        if (args is [string value0, string value1])
        {
            arg0 = value0;
            arg1 = value1;
            return true;
        }

        arg0 = null;
        arg1 = null;
        return false;
    }

    public static bool TryGetArgs(object?[] args, [NotNullWhen(true)] out string? arg0, out int arg1, out int arg2)
    {
        if (args is [string value0, var value1, var value2] &&
            TryConvertToInt(value1, out arg1) &&
            TryConvertToInt(value2, out arg2))
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        arg1 = default;
        arg2 = default;
        return false;
    }

    public static bool TryGetArgs(
        object?[] args,
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1,
        [NotNullWhen(true)] out string? arg2)
    {
        if (args is [string value0, string value1, string value2])
        {
            arg0 = value0;
            arg1 = value1;
            arg2 = value2;
            return true;
        }

        arg0 = null;
        arg1 = null;
        arg2 = null;
        return false;
    }

    public static bool TryGetArgs(
        object?[] args,
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1,
        [NotNullWhen(true)] out string? arg2,
        [NotNullWhen(true)] out string? arg3)
    {
        if (args is [string value0, string value1, string value2, string value3])
        {
            arg0 = value0;
            arg1 = value1;
            arg2 = value2;
            arg3 = value3;
            return true;
        }

        arg0 = null;
        arg1 = null;
        arg2 = null;
        arg3 = null;
        return false;
    }

    internal static bool TryConvertToVersion(object? value, out Version? arg0)
    {
        string? val = value as string;

        if (string.IsNullOrEmpty(val) || !Version.TryParse(val, out arg0))
        {
            arg0 = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Try to convert value to int.
    /// </summary>
    public static bool TryConvertToInt(object? value, out int arg)
    {
        switch (value)
        {
            case double d when d is >= int.MinValue and <= int.MaxValue:
                arg = Convert.ToInt32(d);
                if (Math.Abs(arg - d) == 0)
                {
                    return true;
                }

                break;

            case long l when l is >= int.MinValue and <= int.MaxValue:
                arg = (int)l;
                return true;

            case int i:
                arg = i;
                return true;

            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                return true;

            case null:
                arg = default;
                return true;
        }

        arg = 0;
        return false;
    }

    /// <summary>
    /// Try to convert value to long.
    /// </summary>
    public static bool TryConvertToLong(object? value, out long arg)
    {
        switch (value)
        {
            case double d when d is >= long.MinValue and <= long.MaxValue:
                arg = (long)d;
                if (Math.Abs(arg - d) == 0)
                {
                    return true;
                }

                break;

            case long l:
                arg = l;
                return true;

            case int i:
                arg = i;
                return true;

            case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                return true;

            case null:
                arg = default;
                return true;
        }

        arg = 0;
        return false;
    }

    /// <summary>
    /// Try to convert value to double.
    /// </summary>
    public static bool TryConvertToDouble(object? value, out double arg)
    {
        switch (value)
        {
            case double d:
                arg = d;
                return true;

            case long l:
                arg = l;
                return true;

            case int i:
                arg = i;
                return true;

            case string str when double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out arg):
                return true;

            case null:
                arg = default;
                return true;

            default:
                arg = default;
                return false;
        }
    }

    public static bool TryConvertToChar(object? value, out char c)
    {
        if (value is char ch)
        {
            c = ch;
            return true;
        }
        else if (value is string { Length: 1 } s)
        {
            c = s[0];
            return true;
        }

        c = default;
        return false;
    }

    public static bool TryConvertToStrings(object?[] args, [NotNullWhen(true)] out string[]? result)
    {
        if (args is [])
        {
            result = [];
            return true;
        }

        foreach (object? arg in args)
        {
            if (arg is not string)
            {
                result = null;
                return false;
            }
        }

        result = new string[args.Length];

        for (int i = 0; i < args.Length; i++)
        {
            result[i] = (string)args[i]!;
        }

        return true;
    }

    internal static bool TryGetArgs(object?[] args, out string? arg0, out StringComparison arg1)
    {
        if (args.Length != 2)
        {
            arg0 = null;
            arg1 = default;

            return false;
        }

        arg0 = args[0] as string;

        // reject enums as ints. In C# this would require a cast, which is not supported in msbuild expressions
        if (arg0 == null || args[1] is not string comparisonTypeName || int.TryParse(comparisonTypeName, out _))
        {
            arg1 = default;
            return false;
        }

        // Allow fully-qualified enum, e.g. "System.StringComparison.OrdinalIgnoreCase"
        if (comparisonTypeName.IndexOf('.') >= 0)
        {
            comparisonTypeName = comparisonTypeName.Replace("System.StringComparison.", "").Replace("StringComparison.", "");
        }

        return Enum.TryParse(comparisonTypeName, out arg1);
    }

    public static bool TryGetArgs(object?[] args, out int arg0, out int arg1)
    {
        if (args is [var value0, var value1] &&
            TryConvertToInt(value0, out arg0) &&
            TryConvertToInt(value1, out arg1))
        {
            return true;
        }

        arg0 = default;
        arg1 = default;
        return false;
    }

    public static bool TryGetArgs(object?[] args, out double arg0, out double arg1)
    {
        if (args is [var value0, var value1] &&
            TryConvertToDouble(value0, out arg0) &&
            TryConvertToDouble(value1, out arg1))
        {
            return true;
        }

        arg0 = default;
        arg1 = default;
        return false;
    }

    public static bool TryGetArgs(object?[] args, out int arg0, out char arg1)
    {
        if (args is [var value0, var value1] &&
            TryConvertToInt(value0, out arg0) &&
            TryConvertToChar(value1, out arg1))
        {
            return true;
        }

        arg0 = default;
        arg1 = default;
        return false;
    }

    public static bool TryGetArgs(object?[] args, out int arg0, [NotNullWhen(true)] out string? arg1)
    {
        if (args is [var value0, string value1] &&
            TryConvertToInt(value0, out arg0))
        {
            arg1 = value1;
            return true;
        }

        arg0 = default;
        arg1 = null;
        return false;
    }

    public static bool TryGetArgs(object?[] args, out string? arg0, out int arg1)
    {
        if (args is [string value0, var value1] &&
            TryConvertToInt(value1, out arg1))
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        arg1 = default;
        return false;
    }

    public static bool IsFloatingPointRepresentation(object? value)
        => value is double
        || (value is string str
            && double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out _));

    public static bool TryExecuteArithmeticOverload(
        object?[] args,
        Func<long, long, long> integerOperation,
        Func<double, double, double> realOperation,
        out object? resultValue)
    {
        if (args is [var value0, var value1])
        {
            if (TryConvertToLong(value0, out long argLong0) &&
                TryConvertToLong(value1, out long argLong1))
            {
                resultValue = integerOperation(argLong0, argLong1);
                return true;
            }

            if (TryConvertToDouble(value0, out double argDouble0) &&
                TryConvertToDouble(value1, out double argDouble1))
            {
                resultValue = realOperation(argDouble0, argDouble1);
                return true;
            }
        }

        resultValue = null;
        return false;
    }
}
