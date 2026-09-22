// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Expander;

internal static class ArgumentParser
{
    public static bool TryGetArg(this object?[] args, int index, out object? result)
    {
        if (index >= 0 && index < args.Length)
        {
            result = args[index];
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryGetArg(this object?[] args, int index, out char result)
    {
        if (TryGetArg(args, index, out object? value))
        {
            return TryConvertToChar(value, out result);
        }

        result = default;
        return false;
    }

    public static bool TryGetArg(this object?[] args, int index, out double result)
    {
        if (TryGetArg(args, index, out object? value))
        {
            return TryConvertToDouble(value, out result);
        }

        result = default;
        return false;
    }

    public static bool TryGetArg<T>(this object?[] args, int index, out T result)
        where T : struct, Enum
    {
        if (TryGetArg(args, index, out object? value))
        {
            return TryConvertToEnum(value, out result);
        }

        result = default;
        return false;
    }

    public static bool TryGetArg(this object?[] args, int index, out int result)
    {
        if (TryGetArg(args, index, out object? value))
        {
            return TryConvertToInt(value, out result);
        }

        result = default;
        return false;
    }

    public static bool TryGetArg(this object?[] args, int index, out long result)
    {
        if (TryGetArg(args, index, out object? value))
        {
            return TryConvertToLong(value, out result);
        }

        result = default;
        return false;
    }

    public static bool TryGetArg(this object?[] args, int index, [NotNullWhen(true)] out string? result)
    {
        if (TryGetArg(args, index, out object? value) && value is string s)
        {
            result = s;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryGetArg(this object?[] args, int index, [NotNullWhen(true)] out Version? result)
    {
        if (TryGetArg(args, index, out object? value))
        {
            return TryConvertToVersion(value, out result);
        }

        result = default;
        return false;
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

    public static bool TryConvertToEnum<T>(object? value, out T result)
        where T : struct, Enum
    {
        if (value is T enumValue)
        {
            result = enumValue;
            return true;
        }

        if (value is string text &&
            !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            !ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            if (text.IndexOf('.') >= 0)
            {
                text = NormalizeEnumArgument(typeof(T), text);
            }

            return Enum.TryParse(text, out result);
        }

        result = default;
        return false;
    }

    private static string NormalizeEnumArgument(Type enumType, string value)
    {
        string? fullName = enumType.FullName;
        Assumed.NotNull(fullName);

        string leafName = enumType.Name;
        StringBuilder builder = StringBuilderCache.Acquire(value.Length);

        int copyStart = 0;
        int index = 0;

        while (index < value.Length)
        {
            if (value[index] == '|')
            {
                builder.Append(value, copyStart, index - copyStart);
                builder.Append(',');
                copyStart = ++index;
            }
            else if (TryGetEnumQualifierLength(value, index, fullName, out int qualifierLength) ||
                     TryGetEnumQualifierLength(value, index, leafName, out qualifierLength))
            {
                builder.Append(value, copyStart, index - copyStart);
                index += qualifierLength;
                copyStart = index;
            }
            else
            {
                index++;
            }
        }

        builder.Append(value, copyStart, value.Length - copyStart);
        return StringBuilderCache.GetStringAndRelease(builder);
    }

    private static bool TryGetEnumQualifierLength(string value, int startIndex, string typeName, out int result)
    {
        if (value.Length - startIndex > typeName.Length &&
            value[startIndex + typeName.Length] == '.' &&
            string.CompareOrdinal(value, startIndex, typeName, 0, typeName.Length) == 0)
        {
            result = typeName.Length + 1;
            return true;
        }

        result = 0;
        return false;
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

    internal static bool TryConvertToVersion(object? value, [NotNullWhen(true)] out Version? arg0)
    {
        switch (value)
        {
            case Version v:
                arg0 = v;
                return true;

            case string s when Version.TryParse(s, out arg0):
                return true;
        }

        arg0 = null;
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
