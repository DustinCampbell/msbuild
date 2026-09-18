// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Expander;

internal enum ArithmeticArgumentKind
{
    Int64,
    Double,
}

internal readonly struct ArithmeticArguments
{
    private ArithmeticArguments(ArithmeticArgumentKind kind, long leftInt64, long rightInt64, double leftDouble, double rightDouble)
    {
        Kind = kind;
        LeftInt64 = leftInt64;
        RightInt64 = rightInt64;
        LeftDouble = leftDouble;
        RightDouble = rightDouble;
    }

    public ArithmeticArgumentKind Kind { get; }

    public long LeftInt64 { get; }

    public long RightInt64 { get; }

    public double LeftDouble { get; }

    public double RightDouble { get; }

    public static ArithmeticArguments Create(long left, long right)
        => new(ArithmeticArgumentKind.Int64, left, right, default, default);

    public static ArithmeticArguments Create(double left, double right)
        => new(ArithmeticArgumentKind.Double, default, default, left, right);
}

internal static class FunctionArgumentCoercion
{
    public static bool TryCoerce(object? value, [NotNullWhen(true)] out string? result)
    {
        if (value is string stringValue)
        {
            result = stringValue;
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryCoerceOrNull(object? value, out string? result)
    {
        if (value is null)
        {
            result = null;
            return true;
        }

        return TryCoerce(value, out result);
    }

    public static bool TryCoerce(object? value, out char result)
    {
        switch (value)
        {
            case char character:
                result = character;
                return true;

            case string { Length: 1 } text:
                result = text[0];
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out sbyte result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case string text when sbyte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out byte result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                return true;

            case string text when byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out short result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case byte number:
                result = number;
                return true;

            case short number:
                result = number;
                return true;

            case string text when short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out ushort result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case ushort number:
                result = number;
                return true;

            case string text when ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out int result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case byte number:
                result = number;
                return true;

            case short number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case int number:
                result = number;
                return true;

            case long number when number is >= int.MinValue and <= int.MaxValue:
                result = (int)number;
                return true;

            case double number when number is >= int.MinValue and <= int.MaxValue:
                result = Convert.ToInt32(number);
                if (Math.Abs(result - number) == 0)
                {
                    return true;
                }

                break;

            case Enum enumValue when Enum.GetUnderlyingType(enumValue.GetType()) == typeof(int):
                result = Convert.ToInt32(enumValue, CultureInfo.InvariantCulture);
                return true;

            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerceOrDefault(object? value, out int result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        return TryCoerce(value, out result);
    }

    public static bool TryCoerce(object? value, out uint result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case uint number:
                result = number;
                return true;

            case string text when uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out long result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case byte number:
                result = number;
                return true;

            case short number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case int number:
                result = number;
                return true;

            case uint number:
                result = number;
                return true;

            case long number:
                result = number;
                return true;

            case double number when number is >= long.MinValue and <= long.MaxValue:
                result = (long)number;
                if (Math.Abs(result - number) == 0)
                {
                    return true;
                }

                break;

            case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out ulong result)
    {
        switch (value)
        {
            case byte number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case uint number:
                result = number;
                return true;

            case ulong number:
                result = number;
                return true;

            case string text when ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out float result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case byte number:
                result = number;
                return true;

            case short number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case int number:
                result = number;
                return true;

            case uint number:
                result = number;
                return true;

            case long number:
                result = number;
                return true;

            case ulong number:
                result = number;
                return true;

            case float number:
                result = number;
                return true;

            case string text when float.TryParse(
                    text,
                    NumberStyles.Number | NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out double result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case byte number:
                result = number;
                return true;

            case short number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case int number:
                result = number;
                return true;

            case uint number:
                result = number;
                return true;

            case long number:
                result = number;
                return true;

            case ulong number:
                result = number;
                return true;

            case float number:
                result = number;
                return true;

            case double number:
                result = number;
                return true;

            case string text when double.TryParse(
                    text,
                    NumberStyles.Number | NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, out decimal result)
    {
        switch (value)
        {
            case sbyte number:
                result = number;
                return true;

            case byte number:
                result = number;
                return true;

            case short number:
                result = number;
                return true;

            case ushort number:
                result = number;
                return true;

            case char character:
                result = character;
                return true;

            case int number:
                result = number;
                return true;

            case uint number:
                result = number;
                return true;

            case long number:
                result = number;
                return true;

            case ulong number:
                result = number;
                return true;

            case decimal number:
                result = number;
                return true;

            case string text when decimal.TryParse(
                    text,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out result):
                return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerce(object? value, [NotNullWhen(true)] out Version? result)
    {
        if (value is Version version)
        {
            result = version;
            return true;
        }

        if (value is string { Length: > 0 } text && Version.TryParse(text, out result))
        {
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryCoerce(object? value, out TimeSpan result)
    {
        if (value is TimeSpan timeSpan)
        {
            result = timeSpan;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerceOrDefault(object? value, out TimeSpan result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        return TryCoerce(value, out result);
    }

    public static bool TryCoerce(object? value, out OSPlatform result)
    {
        if (value is OSPlatform platform)
        {
            result = platform;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerceOrDefault(object? value, out OSPlatform result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        return TryCoerce(value, out result);
    }

    public static bool TryCoerce<T>(object? value, out T result)
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

    public static bool TryCoerceArithmetic(object? left, object? right, out ArithmeticArguments result)
    {
        if (TryConvertArithmeticToLong(left, out long leftInt64) &&
            TryConvertArithmeticToLong(right, out long rightInt64))
        {
            result = ArithmeticArguments.Create(leftInt64, rightInt64);
            return true;
        }

        if (TryConvertArithmeticToDouble(left, out double leftDouble) &&
            TryConvertArithmeticToDouble(right, out double rightDouble))
        {
            result = ArithmeticArguments.Create(leftDouble, rightDouble);
            return true;
        }

        if (CanReflectionBindToLong(left) &&
            CanReflectionBindToLong(right) &&
            TryChangeTypeToLong(left, out leftInt64) &&
            TryChangeTypeToLong(right, out rightInt64))
        {
            result = ArithmeticArguments.Create(leftInt64, rightInt64);
            return true;
        }

        if (CanReflectionBindToDouble(left) &&
            CanReflectionBindToDouble(right) &&
            TryWidenToDouble(left, out leftDouble) &&
            TryWidenToDouble(right, out rightDouble))
        {
            result = ArithmeticArguments.Create(leftDouble, rightDouble);
            return true;
        }

        if (TryChangeTypeToLong(left, out leftInt64) &&
            TryChangeTypeToLong(right, out rightInt64))
        {
            result = ArithmeticArguments.Create(leftInt64, rightInt64);
            return true;
        }

        if (TryChangeTypeToDouble(left, out leftDouble) &&
            TryChangeTypeToDouble(right, out rightDouble))
        {
            result = ArithmeticArguments.Create(leftDouble, rightDouble);
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryCoerceForReflection(object? value, Type targetType, out object? result)
    {
        if (value is null)
        {
            result = null;
            return true;
        }

        try
        {
            result = targetType == typeof(char[])
                ? value.ToString()!.ToCharArray()
                : targetType.IsEnum && value is string text && text.IndexOf('.') >= 0
                    ? Enum.Parse(targetType, NormalizeEnumArgument(targetType, text))
                    : Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch (InvalidCastException)
        {
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }

        result = null;
        return false;
    }

    public static bool IsFloatingPointRepresentation(object? value)
        => value is double ||
            (value is string text &&
             double.TryParse(text, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out _));

    public static string NormalizeEnumArgument(Type enumType, string value)
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

    private static bool TryConvertArithmeticToLong(object? value, out long result)
    {
        if (value is null)
        {
            result = 0;
            return true;
        }

        switch (value)
        {
            case double number when number is >= long.MinValue and <= long.MaxValue:
                result = (long)number;
                if (Math.Abs(result - number) == 0)
                {
                    return true;
                }

                break;

            case long number:
                result = number;
                return true;

            case int number:
                result = number;
                return true;

            case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = default;
        return false;
    }

    private static bool TryConvertArithmeticToDouble(object? value, out double result)
    {
        if (value is null)
        {
            result = 0;
            return true;
        }

        switch (value)
        {
            case double number:
                result = number;
                return true;

            case long number:
                result = number;
                return true;

            case int number:
                result = number;
                return true;

            case string text when double.TryParse(
                    text,
                    NumberStyles.Number | NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result):
                return true;
        }

        result = default;
        return false;
    }

    private static bool CanReflectionBindToLong(object? value)
        => value is null || GetTypeCode(value) is
            TypeCode.SByte or
            TypeCode.Byte or
            TypeCode.Int16 or
            TypeCode.UInt16 or
            TypeCode.Int32 or
            TypeCode.UInt32 or
            TypeCode.Int64 or
            TypeCode.Char;

    private static bool CanReflectionBindToDouble(object? value)
        => value is null || GetTypeCode(value) is
            TypeCode.SByte or
            TypeCode.Byte or
            TypeCode.Int16 or
            TypeCode.UInt16 or
            TypeCode.Int32 or
            TypeCode.UInt32 or
            TypeCode.Int64 or
            TypeCode.UInt64 or
            TypeCode.Single or
            TypeCode.Double or
            TypeCode.Char;

    private static TypeCode GetTypeCode(object value)
    {
        Type type = value.GetType();
        return Type.GetTypeCode(type.IsEnum ? Enum.GetUnderlyingType(type) : type);
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

    private static bool TryWidenToDouble(object? value, out double result)
    {
        if (value is char character)
        {
            result = character;
            return true;
        }

        return TryChangeTypeToDouble(value, out result);
    }

    private static bool TryChangeTypeToLong(object? value, out long result)
    {
        if (value is null)
        {
            result = 0;
            return true;
        }

        try
        {
            result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (InvalidCastException)
        {
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }

        result = default;
        return false;
    }

    private static bool TryChangeTypeToDouble(object? value, out double result)
    {
        if (value is null)
        {
            result = 0;
            return true;
        }

        try
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (InvalidCastException)
        {
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }

        result = default;
        return false;
    }
}
