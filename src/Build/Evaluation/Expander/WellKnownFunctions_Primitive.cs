// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
#if NET
using System.Numerics;
#endif

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed partial class PrimitiveHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(Type receiverType, string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(int.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(receiverType, args);
            }

#if NET
            WellKnownFunctionResult floatingPointResult = TryInvokeFloatingPoint(receiverType, methodName, args);
            if (floatingPointResult.Status == WellKnownFunctionStatus.Invoked)
            {
                return floatingPointResult;
            }

            WellKnownFunctionResult integerResult = TryInvokeInteger(receiverType, methodName, args);
            if (integerResult.Status == WellKnownFunctionStatus.Invoked)
            {
                return integerResult;
            }
#endif

            if (args is not [])
            {
                return NotHandled;
            }

            if (methodName.Equals(nameof(int.MaxValue), StringComparison.OrdinalIgnoreCase))
            {
                return receiverType == typeof(byte) ? Invoked(byte.MaxValue)
                    : receiverType == typeof(sbyte) ? Invoked(sbyte.MaxValue)
                    : receiverType == typeof(short) ? Invoked(short.MaxValue)
                    : receiverType == typeof(ushort) ? Invoked(ushort.MaxValue)
                    : receiverType == typeof(int) ? Invoked(int.MaxValue)
                    : receiverType == typeof(uint) ? Invoked(uint.MaxValue)
                    : receiverType == typeof(long) ? Invoked(long.MaxValue)
                    : receiverType == typeof(ulong) ? Invoked(ulong.MaxValue)
                    : receiverType == typeof(float) ? Invoked(float.MaxValue)
                    : receiverType == typeof(double) ? Invoked(double.MaxValue)
                    : receiverType == typeof(char) ? Invoked(char.MaxValue)
                    : NotHandled;
            }

            if (methodName.Equals(nameof(int.MinValue), StringComparison.OrdinalIgnoreCase))
            {
                return receiverType == typeof(byte) ? Invoked(byte.MinValue)
                    : receiverType == typeof(sbyte) ? Invoked(sbyte.MinValue)
                    : receiverType == typeof(short) ? Invoked(short.MinValue)
                    : receiverType == typeof(ushort) ? Invoked(ushort.MinValue)
                    : receiverType == typeof(int) ? Invoked(int.MinValue)
                    : receiverType == typeof(uint) ? Invoked(uint.MinValue)
                    : receiverType == typeof(long) ? Invoked(long.MinValue)
                    : receiverType == typeof(ulong) ? Invoked(ulong.MinValue)
                    : receiverType == typeof(float) ? Invoked(float.MinValue)
                    : receiverType == typeof(double) ? Invoked(double.MinValue)
                    : receiverType == typeof(char) ? Invoked(char.MinValue)
                    : NotHandled;
            }

            if (receiverType == typeof(float))
            {
                return TryInvokeSingleField(methodName);
            }

            if (receiverType == typeof(double))
            {
                return TryInvokeDoubleField(methodName);
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, object value, object?[] args)
        {
            if (methodName.Equals(nameof(IComparable.CompareTo), StringComparison.OrdinalIgnoreCase))
            {
                if (value is IComparable comparable && args is [var other])
                {
                    ArgumentParser.TryWidenPrimitive(other, value.GetType(), out object? widened);
                    return Invoked(comparable.CompareTo(widened ?? other));
                }

                return NotHandled;
            }

            if (methodName.Equals(nameof(object.Equals), StringComparison.OrdinalIgnoreCase))
            {
                if (args is [var other])
                {
                    ArgumentParser.TryWidenPrimitive(other, value.GetType(), out object? widened);
                    return Invoked(value.Equals(widened ?? other));
                }

                return NotHandled;
            }

            if (methodName.Equals(nameof(object.GetHashCode), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(value.GetHashCode()) : NotHandled;
            }

            if (methodName.Equals(nameof(IConvertible.GetTypeCode), StringComparison.OrdinalIgnoreCase))
            {
                return value is IConvertible convertible && args is []
                    ? Invoked(convertible.GetTypeCode())
                    : NotHandled;
            }

            if (methodName.Equals(nameof(object.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeToString(value, args);
            }

            return NotHandled;
        }

#if NET
        private static WellKnownFunctionResult TryInvokeInteger(Type receiverType, string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(int.BigMul), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeBigMul(receiverType, args);
            }

            return receiverType == typeof(byte) ? TryInvokeInteger<byte>(methodName, args, signed: false)
                : receiverType == typeof(sbyte) ? TryInvokeInteger<sbyte>(methodName, args, signed: true)
                : receiverType == typeof(short) ? TryInvokeInteger<short>(methodName, args, signed: true)
                : receiverType == typeof(ushort) ? TryInvokeInteger<ushort>(methodName, args, signed: false)
                : receiverType == typeof(int) ? TryInvokeInteger<int>(methodName, args, signed: true)
                : receiverType == typeof(uint) ? TryInvokeInteger<uint>(methodName, args, signed: false)
                : receiverType == typeof(long) ? TryInvokeInteger<long>(methodName, args, signed: true)
                : receiverType == typeof(ulong) ? TryInvokeInteger<ulong>(methodName, args, signed: false)
                : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeInteger<T>(string methodName, object?[] args, bool signed)
            where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
        {
            if (!TryGetInteger(args, 0, out T first))
            {
                return NotHandled;
            }

            if (args is [_])
            {
                WellKnownFunctionResult result = methodName switch
                {
                    var name when name.Equals(nameof(int.IsEvenInteger), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsEvenInteger(first)),
                    var name when name.Equals(nameof(int.IsOddInteger), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsOddInteger(first)),
                    var name when name.Equals(nameof(int.IsPow2), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsPow2(first)),
                    var name when name.Equals(nameof(int.LeadingZeroCount), StringComparison.OrdinalIgnoreCase) => Invoked(T.LeadingZeroCount(first)),
                    var name when name.Equals(nameof(int.Log10), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log10(first)),
                    var name when name.Equals(nameof(int.Log2), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log2(first)),
                    var name when name.Equals(nameof(int.PopCount), StringComparison.OrdinalIgnoreCase) => Invoked(T.PopCount(first)),
                    var name when name.Equals(nameof(int.Sign), StringComparison.OrdinalIgnoreCase) => Invoked(T.Sign(first)),
                    var name when name.Equals(nameof(int.TrailingZeroCount), StringComparison.OrdinalIgnoreCase) => Invoked(T.TrailingZeroCount(first)),
                    _ => NotHandled,
                };

                if (result.Status == WellKnownFunctionStatus.Invoked || !signed)
                {
                    return result;
                }

                return methodName switch
                {
                    var name when name.Equals(nameof(int.Abs), StringComparison.OrdinalIgnoreCase) => Invoked(T.Abs(first)),
                    var name when name.Equals(nameof(int.IsNegative), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsNegative(first)),
                    var name when name.Equals(nameof(int.IsPositive), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsPositive(first)),
                    _ => NotHandled,
                };
            }

            if (args is [_, var amount] &&
                ArgumentParser.TryConvertToInt(amount, out int convertedAmount))
            {
                WellKnownFunctionResult rotationResult = methodName switch
                {
                    var name when name.Equals(nameof(int.RotateLeft), StringComparison.OrdinalIgnoreCase) => Invoked(T.RotateLeft(first, convertedAmount)),
                    var name when name.Equals(nameof(int.RotateRight), StringComparison.OrdinalIgnoreCase) => Invoked(T.RotateRight(first, convertedAmount)),
                    _ => NotHandled,
                };

                if (rotationResult.Status == WellKnownFunctionStatus.Invoked)
                {
                    return rotationResult;
                }
            }

            if (TryGetInteger(args, 1, out T second))
            {
                if (args is [_, _])
                {
                    WellKnownFunctionResult result = methodName switch
                    {
                        var name when name.Equals(nameof(int.DivRem), StringComparison.OrdinalIgnoreCase) => Invoked(T.DivRem(first, second)),
                        var name when name.Equals(nameof(int.Max), StringComparison.OrdinalIgnoreCase) => Invoked(T.Max(first, second)),
                        var name when name.Equals(nameof(int.Min), StringComparison.OrdinalIgnoreCase) => Invoked(T.Min(first, second)),
                        _ => NotHandled,
                    };

                    if (result.Status == WellKnownFunctionStatus.Invoked || !signed)
                    {
                        return result;
                    }

                    return methodName switch
                    {
                        var name when name.Equals(nameof(int.CopySign), StringComparison.OrdinalIgnoreCase) => Invoked(T.CopySign(first, second)),
                        var name when name.Equals(nameof(int.MaxMagnitude), StringComparison.OrdinalIgnoreCase) => Invoked(T.MaxMagnitude(first, second)),
                        var name when name.Equals(nameof(int.MinMagnitude), StringComparison.OrdinalIgnoreCase) => Invoked(T.MinMagnitude(first, second)),
                        _ => NotHandled,
                    };
                }

                if (args is [_, _, _] &&
                    methodName.Equals(nameof(int.Clamp), StringComparison.OrdinalIgnoreCase) &&
                    TryGetInteger(args, 2, out T third))
                {
                    return Invoked(T.Clamp(first, second, third));
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeBigMul(Type receiverType, object?[] args)
        {
            if (receiverType == typeof(int) && TryGetInteger(args, 0, out int intLeft) && TryGetInteger(args, 1, out int intRight))
            {
                return args is [_, _] ? Invoked(int.BigMul(intLeft, intRight)) : NotHandled;
            }

            if (receiverType == typeof(uint) && TryGetInteger(args, 0, out uint uintLeft) && TryGetInteger(args, 1, out uint uintRight))
            {
                return args is [_, _] ? Invoked(uint.BigMul(uintLeft, uintRight)) : NotHandled;
            }

            if (receiverType == typeof(long) && TryGetInteger(args, 0, out long longLeft) && TryGetInteger(args, 1, out long longRight))
            {
                return args is [_, _] ? Invoked(long.BigMul(longLeft, longRight)) : NotHandled;
            }

            if (receiverType == typeof(ulong) && TryGetInteger(args, 0, out ulong ulongLeft) && TryGetInteger(args, 1, out ulong ulongRight))
            {
                return args is [_, _] ? Invoked(ulong.BigMul(ulongLeft, ulongRight)) : NotHandled;
            }

            return NotHandled;
        }

        private static bool TryGetInteger<T>(object?[] args, int index, out T result)
            where T : unmanaged, IBinaryInteger<T>, IMinMaxValue<T>
        {
            if (!args.TryGetArg(index, out object? value))
            {
                result = default;
                return false;
            }

            if (value is T number)
            {
                result = number;
                return true;
            }

            switch (value)
            {
                case byte source:
                    result = T.CreateChecked(source);
                    return true;
                case sbyte source:
                    result = T.CreateChecked(source);
                    return true;
                case short source:
                    result = T.CreateChecked(source);
                    return true;
                case ushort source:
                    result = T.CreateChecked(source);
                    return true;
                case int source:
                    result = T.CreateChecked(source);
                    return true;
                case uint source:
                    result = T.CreateChecked(source);
                    return true;
                case long source:
                    result = T.CreateChecked(source);
                    return true;
                case ulong source:
                    result = T.CreateChecked(source);
                    return true;
                case char source:
                    result = T.CreateChecked((ushort)source);
                    return true;
                case float source:
                    result = T.CreateChecked(source);
                    return true;
                case double source:
                    result = T.CreateChecked(source);
                    return true;
                case decimal source:
                    result = T.CreateChecked(source);
                    return true;
                case bool source:
                    result = T.CreateChecked(source ? 1 : 0);
                    return true;
                case string text when T.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                    return true;
                default:
                    result = default;
                    return false;
            }
        }
#endif

        private static WellKnownFunctionResult TryInvokeParse(Type receiverType, object?[] args)
            => args switch
            {
                [string value] => Parse(receiverType, value),
                [string value, NumberStyles styles] => Parse(receiverType, value, styles),
                [string value, IFormatProvider provider] => Parse(receiverType, value, provider),
                [string value, null] => Parse(receiverType, value, provider: null),
                [string value, NumberStyles styles, IFormatProvider provider] => Parse(receiverType, value, styles, provider),
                [string value, NumberStyles styles, null] => Parse(receiverType, value, styles, provider: null),
                [string value, var styles] when ArgumentParser.TryConvertToEnum(styles, out NumberStyles convertedStyles)
                    => Parse(receiverType, value, convertedStyles),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult Parse(Type receiverType, string value)
            => Type.GetTypeCode(receiverType) switch
            {
                TypeCode.Byte => Invoked(byte.Parse(value)),
                TypeCode.SByte => Invoked(sbyte.Parse(value)),
                TypeCode.Int16 => Invoked(short.Parse(value)),
                TypeCode.UInt16 => Invoked(ushort.Parse(value)),
                TypeCode.Int32 => Invoked(int.Parse(value)),
                TypeCode.UInt32 => Invoked(uint.Parse(value)),
                TypeCode.Int64 => Invoked(long.Parse(value)),
                TypeCode.UInt64 => Invoked(ulong.Parse(value)),
                TypeCode.Single => Invoked(float.Parse(value)),
                TypeCode.Double => Invoked(double.Parse(value)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult Parse(Type receiverType, string value, NumberStyles styles)
            => Type.GetTypeCode(receiverType) switch
            {
                TypeCode.Byte => Invoked(byte.Parse(value, styles)),
                TypeCode.SByte => Invoked(sbyte.Parse(value, styles)),
                TypeCode.Int16 => Invoked(short.Parse(value, styles)),
                TypeCode.UInt16 => Invoked(ushort.Parse(value, styles)),
                TypeCode.Int32 => Invoked(int.Parse(value, styles)),
                TypeCode.UInt32 => Invoked(uint.Parse(value, styles)),
                TypeCode.Int64 => Invoked(long.Parse(value, styles)),
                TypeCode.UInt64 => Invoked(ulong.Parse(value, styles)),
                TypeCode.Single => Invoked(float.Parse(value, styles)),
                TypeCode.Double => Invoked(double.Parse(value, styles)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult Parse(Type receiverType, string value, IFormatProvider? provider)
            => Type.GetTypeCode(receiverType) switch
            {
                TypeCode.Byte => Invoked(byte.Parse(value, provider)),
                TypeCode.SByte => Invoked(sbyte.Parse(value, provider)),
                TypeCode.Int16 => Invoked(short.Parse(value, provider)),
                TypeCode.UInt16 => Invoked(ushort.Parse(value, provider)),
                TypeCode.Int32 => Invoked(int.Parse(value, provider)),
                TypeCode.UInt32 => Invoked(uint.Parse(value, provider)),
                TypeCode.Int64 => Invoked(long.Parse(value, provider)),
                TypeCode.UInt64 => Invoked(ulong.Parse(value, provider)),
                TypeCode.Single => Invoked(float.Parse(value, provider)),
                TypeCode.Double => Invoked(double.Parse(value, provider)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult Parse(Type receiverType, string value, NumberStyles styles, IFormatProvider? provider)
            => Type.GetTypeCode(receiverType) switch
            {
                TypeCode.Byte => Invoked(byte.Parse(value, styles, provider)),
                TypeCode.SByte => Invoked(sbyte.Parse(value, styles, provider)),
                TypeCode.Int16 => Invoked(short.Parse(value, styles, provider)),
                TypeCode.UInt16 => Invoked(ushort.Parse(value, styles, provider)),
                TypeCode.Int32 => Invoked(int.Parse(value, styles, provider)),
                TypeCode.UInt32 => Invoked(uint.Parse(value, styles, provider)),
                TypeCode.Int64 => Invoked(long.Parse(value, styles, provider)),
                TypeCode.UInt64 => Invoked(ulong.Parse(value, styles, provider)),
                TypeCode.Single => Invoked(float.Parse(value, styles, provider)),
                TypeCode.Double => Invoked(double.Parse(value, styles, provider)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeDoubleField(string methodName)
        {
            if (methodName.Equals(nameof(double.Epsilon), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.Epsilon);
            }

            if (methodName.Equals(nameof(double.NaN), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.NaN);
            }

            if (methodName.Equals(nameof(double.NegativeInfinity), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.NegativeInfinity);
            }

            if (methodName.Equals(nameof(double.PositiveInfinity), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.PositiveInfinity);
            }

#if NET
            if (methodName.Equals(nameof(double.E), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.E);
            }

            if (methodName.Equals(nameof(double.NegativeZero), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.NegativeZero);
            }

            if (methodName.Equals(nameof(double.Pi), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.Pi);
            }

            if (methodName.Equals(nameof(double.Tau), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(double.Tau);
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeSingleField(string methodName)
        {
            if (methodName.Equals(nameof(float.Epsilon), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.Epsilon);
            }

            if (methodName.Equals(nameof(float.NaN), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.NaN);
            }

            if (methodName.Equals(nameof(float.NegativeInfinity), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.NegativeInfinity);
            }

            if (methodName.Equals(nameof(float.PositiveInfinity), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.PositiveInfinity);
            }

#if NET
            if (methodName.Equals(nameof(float.E), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.E);
            }

            if (methodName.Equals(nameof(float.NegativeZero), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.NegativeZero);
            }

            if (methodName.Equals(nameof(float.Pi), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.Pi);
            }

            if (methodName.Equals(nameof(float.Tau), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(float.Tau);
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeToString(object value, object?[] args)
            => args switch
            {
                [] => Invoked(value.ToString()),
                [string format] when value is IFormattable formattable => Invoked(formattable.ToString(format, formatProvider: null)),
                [IFormatProvider provider] when value is IFormattable formattable => Invoked(formattable.ToString(format: null, provider)),
                [null] when value is IFormattable formattable => Invoked(formattable.ToString(format: null, formatProvider: null)),
                [string format, IFormatProvider provider] when value is IFormattable formattable => Invoked(formattable.ToString(format, provider)),
                [string format, null] when value is IFormattable formattable => Invoked(formattable.ToString(format, formatProvider: null)),
                [IFormatProvider provider] when value is IConvertible convertible => Invoked(convertible.ToString(provider)),
                [null] when value is IConvertible convertible => Invoked(convertible.ToString(provider: null)),
                _ => NotHandled,
            };
    }
}
