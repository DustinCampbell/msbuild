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

            case string str when double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out arg):
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

            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out arg):
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

            case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out arg):
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
            && double.TryParse(str, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out _));

    public static bool TryGetArithmeticArguments(object?[] args, out ArithmeticArguments result)
    {
        if (args is [var value0, var value1])
        {
            // Keep the original values for phase selection while caching any string parses that later phases reuse.
            ArithmeticArgument argument0 = new(value0);
            ArithmeticArgument argument1 = new(value1);

            // Both arguments must resolve in the same phase; mixing phases would change overload selection.
            // First reproduce the historical well-known-function conversions.
            if (argument0.TryGetDirectLong(out long argLong0) &&
                argument1.TryGetDirectLong(out long argLong1))
            {
                result = new ArithmeticArguments(argLong0, argLong1);
                return true;
            }

            if (argument0.TryGetDirectDouble(out double argDouble0) &&
                argument1.TryGetDirectDouble(out double argDouble1))
            {
                result = new ArithmeticArguments(argDouble0, argDouble1);
                return true;
            }

            // Next reproduce the primitive widening conversions performed by the reflection binder.
            if (argument0.TryGetWidenedLong(out argLong0) &&
                argument1.TryGetWidenedLong(out argLong1))
            {
                result = new ArithmeticArguments(argLong0, argLong1);
                return true;
            }

            if (argument0.TryGetWidenedDouble(out argDouble0) &&
                argument1.TryGetWidenedDouble(out argDouble1))
            {
                result = new ArithmeticArguments(argDouble0, argDouble1);
                return true;
            }

            // Finally reproduce the long-before-double Convert.ChangeType fallback.
            if (argument0.TryGetCoercedLong(out argLong0) &&
                argument1.TryGetCoercedLong(out argLong1))
            {
                result = new ArithmeticArguments(argLong0, argLong1);
                return true;
            }

            if (argument0.TryGetCoercedDouble(out argDouble0) &&
                argument1.TryGetCoercedDouble(out argDouble1))
            {
                result = new ArithmeticArguments(argDouble0, argDouble1);
                return true;
            }
        }

        result = default;
        return false;
    }

    private static bool TryWidenToLong(object? value, out long result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Char:
                result = (char)value;
                return true;

            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
                result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                return true;

            default:
                result = default;
                return false;
        }
    }

    private static bool TryWidenToDouble(object? value, out double result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Char:
                result = (char)value;
                return true;

            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
                result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return true;

            default:
                result = default;
                return false;
        }
    }

    private static bool TryCoerceNonStringToLong(object? value, out long result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        if (value is bool b)
        {
            result = b ? 1 : 0;
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

    private static bool TryCoerceNonStringToDouble(object? value, out double result)
    {
        if (value is null)
        {
            result = default;
            return true;
        }

        if (value is bool b)
        {
            result = b ? 1D : 0D;
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

    // Retains an argument's original type for overload selection while lazily caching string parse attempts.
    // A successfully parsed string must not become eligible for the primitive widening phase.
    private struct ArithmeticArgument
    {
        // Convert.ToDouble(string, ...) does not accept the trailing sign supported by the historical direct parser.
        private const NumberStyles CoercedDoubleStyles = NumberStyles.Float | NumberStyles.AllowThousands;
        private const NumberStyles DirectDoubleStyles = NumberStyles.Number | NumberStyles.Float;

        private readonly object? _value;
        private long _parsedLong;
        private double _coercedDouble;
        private double _directDouble;
        private ConversionState _longState;
        private ConversionState _coercedDoubleState;
        private ConversionState _directDoubleState;

        public ArithmeticArgument(object? value)
        {
            _value = value;
            _parsedLong = default;
            _coercedDouble = default;
            _directDouble = default;
            _longState = ConversionState.NotAttempted;
            _coercedDoubleState = ConversionState.NotAttempted;
            _directDoubleState = ConversionState.NotAttempted;
        }

        public bool TryGetDirectLong(out long result)
            => _value is string text
                ? TryGetStringLong(text, out result)
                : TryConvertToLong(_value, out result);

        public bool TryGetDirectDouble(out double result)
        {
            if (_value is not string text)
            {
                return TryConvertToDouble(_value, out result);
            }

            // Parse with the narrower coercion syntax first so the common result can be reused by both phases.
            if (TryGetCoercedStringDouble(text, out result))
            {
                return true;
            }

            if (_directDoubleState == ConversionState.NotAttempted)
            {
                _directDoubleState = double.TryParse(text, DirectDoubleStyles, CultureInfo.InvariantCulture, out _directDouble)
                    ? ConversionState.Succeeded
                    : ConversionState.Failed;
            }

            result = _directDouble;
            return _directDoubleState == ConversionState.Succeeded;
        }

        public bool TryGetWidenedLong(out long result)
            => TryWidenToLong(_value, out result);

        public bool TryGetWidenedDouble(out double result)
            => TryWidenToDouble(_value, out result);

        public bool TryGetCoercedLong(out long result)
            => _value is string text
                ? TryGetStringLong(text, out result)
                : TryCoerceNonStringToLong(_value, out result);

        public bool TryGetCoercedDouble(out double result)
            => _value is string text
                ? TryGetCoercedStringDouble(text, out result)
                : TryCoerceNonStringToDouble(_value, out result);

        private bool TryGetStringLong(string text, out long result)
        {
            if (_longState == ConversionState.NotAttempted)
            {
                _longState = long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _parsedLong)
                    ? ConversionState.Succeeded
                    : ConversionState.Failed;
            }

            result = _parsedLong;
            return _longState == ConversionState.Succeeded;
        }

        private bool TryGetCoercedStringDouble(string text, out double result)
        {
            if (_coercedDoubleState == ConversionState.NotAttempted)
            {
                _coercedDoubleState = double.TryParse(text, CoercedDoubleStyles, CultureInfo.InvariantCulture, out _coercedDouble)
                    ? ConversionState.Succeeded
                    : ConversionState.Failed;
            }

            result = _coercedDouble;
            return _coercedDoubleState == ConversionState.Succeeded;
        }
    }

    // A failed parse must be cached separately from a parse that has not yet been attempted.
    private enum ConversionState : byte
    {
        NotAttempted,
        Failed,
        Succeeded,
    }
}
