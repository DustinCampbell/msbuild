// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class MathHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (args is [])
            {
                if (methodName.Equals(nameof(Math.E), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.E);
                }
                if (methodName.Equals(nameof(Math.PI), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.PI);
                }
#if NET
                if (methodName.Equals(nameof(Math.Tau), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Tau);
                }
#endif
                return NotHandled;
            }

            if (methodName.Equals(nameof(Math.Abs), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeAbs(args);
            }
            if (methodName.Equals(nameof(Math.BigMul), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeBigMul(args);
            }
            if (methodName.Equals(nameof(Math.Ceiling), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeCeiling(args);
            }
            if (methodName.Equals(nameof(Math.Floor), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeFloor(args);
            }
            if (methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeMinMax(args, maximum: true);
            }
            if (methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeMinMax(args, maximum: false);
            }
            if (methodName.Equals(nameof(Math.Round), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeRound(args);
            }
            if (methodName.Equals(nameof(Math.Sign), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeSign(args);
            }
            if (methodName.Equals(nameof(Math.Truncate), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeTruncate(args);
            }
#if NET
            if (methodName.Equals(nameof(Math.DivRem), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeDivRem(args);
            }
#endif

            if (args is [var value] && TryConvertToDouble(value, out double convertedValue))
            {
                if (methodName.Equals(nameof(Math.Acos), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Acos(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Asin), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Asin(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Atan), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Atan(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Cos), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Cos(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Cosh), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Cosh(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Exp), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Exp(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Log), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Log(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Log10), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Log10(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Sin), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Sin(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Sinh), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Sinh(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Sqrt), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Sqrt(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Tan), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Tan(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Tanh), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Tanh(convertedValue));
                }
#if NET
                if (methodName.Equals(nameof(Math.Acosh), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Acosh(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Asinh), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Asinh(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Atanh), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Atanh(convertedValue));
                }
                if (methodName.Equals(nameof(Math.BitDecrement), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.BitDecrement(convertedValue));
                }

                if (methodName.Equals(nameof(Math.BitIncrement), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.BitIncrement(convertedValue));
                }

                if (methodName.Equals(nameof(Math.Cbrt), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Cbrt(convertedValue));
                }
                if (methodName.Equals(nameof(Math.ILogB), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.ILogB(convertedValue));
                }
                if (methodName.Equals(nameof(Math.Log2), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Log2(convertedValue));
                }

                if (methodName.Equals(nameof(Math.ReciprocalEstimate), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.ReciprocalEstimate(convertedValue));
                }

                if (methodName.Equals(nameof(Math.ReciprocalSqrtEstimate), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.ReciprocalSqrtEstimate(convertedValue));
                }

                if (methodName.Equals(nameof(Math.SinCos), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.SinCos(convertedValue));
                }
#endif
            }

            if (args is [var left, var right]
                && TryConvertToDouble(left, out double convertedLeft)
                && TryConvertToDouble(right, out double convertedRight))
            {
                if (methodName.Equals(nameof(Math.Atan2), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Atan2(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(Math.IEEERemainder), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.IEEERemainder(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(Math.Log), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Log(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(Math.Pow), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.Pow(convertedLeft, convertedRight));
                }

#if NET
                if (methodName.Equals(nameof(Math.CopySign), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.CopySign(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(Math.MaxMagnitude), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.MaxMagnitude(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(Math.MinMagnitude), StringComparison.OrdinalIgnoreCase))
                {
                    return Invoked(Math.MinMagnitude(convertedLeft, convertedRight));
                }

                if (methodName.Equals(nameof(Math.ScaleB), StringComparison.OrdinalIgnoreCase)
                    && TryConvertToInt(right, out int convertedScale))
                {
                    return Invoked(Math.ScaleB(convertedLeft, convertedScale));
                }
#endif
            }

#if NET
            if (methodName.Equals(nameof(Math.Clamp), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeClamp(args);
            }

            if (methodName.Equals(nameof(Math.FusedMultiplyAdd), StringComparison.OrdinalIgnoreCase)
                && args is [var x, var y, var z]
                && TryConvertToDouble(x, out double convertedX)
                && TryConvertToDouble(y, out double convertedY)
                && TryConvertToDouble(z, out double convertedZ))
            {
                return Invoked(Math.FusedMultiplyAdd(convertedX, convertedY, convertedZ));
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeAbs(object?[] args)
        {
            if (args is not [var value])
            {
                return NotHandled;
            }

            return value switch
            {
                decimal number => Invoked(Math.Abs(number)),
                double number => Invoked(Math.Abs(number)),
                float number => Invoked(Math.Abs(number)),
                long number => Invoked(Math.Abs(number)),
                int number => Invoked(Math.Abs(number)),
                short number => Invoked(Math.Abs(number)),
                sbyte number => Invoked(Math.Abs(number)),
                byte number => Invoked(Math.Abs((short)number)),
                ushort number => Invoked(Math.Abs((int)number)),
                uint number => Invoked(Math.Abs((long)number)),
                ulong number => Invoked(Math.Abs((float)number)),
                string text when ArgumentParser.TryConvertToDouble(text, out double number) => Invoked(Math.Abs(number)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeBigMul(object?[] args)
        {
            if (args is not [var left, var right])
            {
                return NotHandled;
            }

            if (TryGetCommonNumericType(left, right, out TypeCode type))
            {
                switch (type)
                {
                    case TypeCode.SByte:
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                        return Invoked(Math.BigMul(Convert.ToInt32(left), Convert.ToInt32(right)));
#if NET
                    case TypeCode.UInt32:
                        return Invoked(Math.BigMul(Convert.ToUInt32(left), Convert.ToUInt32(right)));
                    case TypeCode.Int64:
                        return Invoked(Math.BigMul(Convert.ToInt64(left), Convert.ToInt64(right)));
                    case TypeCode.UInt64:
                        return Invoked(Math.BigMul(Convert.ToUInt64(left), Convert.ToUInt64(right)));
#endif
                    default:
                        return NotHandled;
                }
            }

            if (TryConvertToInt(left, out int leftInt) && TryConvertToInt(right, out int rightInt))
            {
                return Invoked(Math.BigMul(leftInt, rightInt));
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeCeiling(object?[] args)
        {
            if (args is [decimal decimalValue])
            {
                return Invoked(Math.Ceiling(decimalValue));
            }

            return args is [var value] && TryConvertToDouble(value, out double doubleValue)
                ? Invoked(Math.Ceiling(doubleValue))
                : NotHandled;
        }

#if NET
        private static WellKnownFunctionResult TryInvokeClamp(object?[] args)
        {
            if (args is not [var value, var min, var max])
            {
                return NotHandled;
            }

            if (TryGetCommonNumericType(value, min, max, out TypeCode type))
            {
                return type switch
                {
                    TypeCode.SByte => Invoked(Math.Clamp(Convert.ToSByte(value), Convert.ToSByte(min), Convert.ToSByte(max))),
                    TypeCode.Byte => Invoked(Math.Clamp(Convert.ToByte(value), Convert.ToByte(min), Convert.ToByte(max))),
                    TypeCode.Int16 => Invoked(Math.Clamp(Convert.ToInt16(value), Convert.ToInt16(min), Convert.ToInt16(max))),
                    TypeCode.UInt16 => Invoked(Math.Clamp(Convert.ToUInt16(value), Convert.ToUInt16(min), Convert.ToUInt16(max))),
                    TypeCode.Int32 => Invoked(Math.Clamp(Convert.ToInt32(value), Convert.ToInt32(min), Convert.ToInt32(max))),
                    TypeCode.UInt32 => Invoked(Math.Clamp(Convert.ToUInt32(value), Convert.ToUInt32(min), Convert.ToUInt32(max))),
                    TypeCode.Int64 => Invoked(Math.Clamp(Convert.ToInt64(value), Convert.ToInt64(min), Convert.ToInt64(max))),
                    TypeCode.UInt64 => Invoked(Math.Clamp(Convert.ToUInt64(value), Convert.ToUInt64(min), Convert.ToUInt64(max))),
                    TypeCode.Single => Invoked(Math.Clamp(Convert.ToSingle(value), Convert.ToSingle(min), Convert.ToSingle(max))),
                    TypeCode.Double => Invoked(Math.Clamp(Convert.ToDouble(value), Convert.ToDouble(min), Convert.ToDouble(max))),
                    TypeCode.Decimal => Invoked(Math.Clamp(Convert.ToDecimal(value), Convert.ToDecimal(min), Convert.ToDecimal(max))),
                    _ => NotHandled,
                };
            }

            return TryConvertToDouble(value, out double convertedValue)
                && TryConvertToDouble(min, out double convertedMin)
                && TryConvertToDouble(max, out double convertedMax)
                    ? Invoked(Math.Clamp(convertedValue, convertedMin, convertedMax))
                    : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeDivRem(object?[] args)
        {
            if (args is not [var left, var right])
            {
                return NotHandled;
            }

            return TryGetCommonNumericType(left, right, out TypeCode type)
                ? type switch
                {
                    TypeCode.SByte => Invoked(Math.DivRem(Convert.ToSByte(left), Convert.ToSByte(right))),
                    TypeCode.Byte => Invoked(Math.DivRem(Convert.ToByte(left), Convert.ToByte(right))),
                    TypeCode.Int16 => Invoked(Math.DivRem(Convert.ToInt16(left), Convert.ToInt16(right))),
                    TypeCode.UInt16 => Invoked(Math.DivRem(Convert.ToUInt16(left), Convert.ToUInt16(right))),
                    TypeCode.Int32 => Invoked(Math.DivRem(Convert.ToInt32(left), Convert.ToInt32(right))),
                    TypeCode.UInt32 => Invoked(Math.DivRem(Convert.ToUInt32(left), Convert.ToUInt32(right))),
                    TypeCode.Int64 => Invoked(Math.DivRem(Convert.ToInt64(left), Convert.ToInt64(right))),
                    TypeCode.UInt64 => Invoked(Math.DivRem(Convert.ToUInt64(left), Convert.ToUInt64(right))),
                    _ => NotHandled,
                }
                : NotHandled;
        }
#endif

        private static WellKnownFunctionResult TryInvokeFloor(object?[] args)
        {
            if (args is [decimal decimalValue])
            {
                return Invoked(Math.Floor(decimalValue));
            }

            return args is [var value] && TryConvertToDouble(value, out double convertedValue)
                ? Invoked(Math.Floor(convertedValue))
                : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeMinMax(object?[] args, bool maximum)
        {
            if (args is not [var left, var right])
            {
                return NotHandled;
            }

            if (TryGetCommonNumericType(left, right, out TypeCode type))
            {
                return type switch
                {
                    TypeCode.SByte => InvokeMinMax(Convert.ToSByte(left), Convert.ToSByte(right), maximum),
                    TypeCode.Byte => InvokeMinMax(Convert.ToByte(left), Convert.ToByte(right), maximum),
                    TypeCode.Int16 => InvokeMinMax(Convert.ToInt16(left), Convert.ToInt16(right), maximum),
                    TypeCode.UInt16 => InvokeMinMax(Convert.ToUInt16(left), Convert.ToUInt16(right), maximum),
                    TypeCode.Int32 => InvokeMinMax(Convert.ToInt32(left), Convert.ToInt32(right), maximum),
                    TypeCode.UInt32 => InvokeMinMax(Convert.ToUInt32(left), Convert.ToUInt32(right), maximum),
                    TypeCode.Int64 => InvokeMinMax(Convert.ToInt64(left), Convert.ToInt64(right), maximum),
                    TypeCode.UInt64 => InvokeMinMax(Convert.ToUInt64(left), Convert.ToUInt64(right), maximum),
                    TypeCode.Single => InvokeMinMax(Convert.ToSingle(left), Convert.ToSingle(right), maximum),
                    TypeCode.Double => InvokeMinMax(Convert.ToDouble(left), Convert.ToDouble(right), maximum),
                    TypeCode.Decimal => InvokeMinMax(Convert.ToDecimal(left), Convert.ToDecimal(right), maximum),
                    _ => NotHandled,
                };
            }

            return TryConvertToDouble(left, out double convertedLeft)
                && TryConvertToDouble(right, out double convertedRight)
                    ? Invoked(maximum ? Math.Max(convertedLeft, convertedRight) : Math.Min(convertedLeft, convertedRight))
                    : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeRound(object?[] args)
        {
            if (args is [decimal decimalValue1])
            {
                return Invoked(Math.Round(decimalValue1));
            }
            if (args is [decimal decimalValue2, var second])
            {
                if (ArgumentParser.TryConvertToInt(second, out int digits))
                {
                    return Invoked(Math.Round(decimalValue2, digits));
                }
                if (second is MidpointRounding mode)
                {
                    return Invoked(Math.Round(decimalValue2, mode));
                }
            }

            if (args is [decimal decimalValue3, var digitsValue, var modeValue]
                && ArgumentParser.TryConvertToInt(digitsValue, out int convertedDigits)
                && modeValue is MidpointRounding convertedMode)
            {
                return Invoked(Math.Round(decimalValue3, convertedDigits, convertedMode));
            }

            if (args is [var value] && TryConvertToDouble(value, out double doubleValue))
            {
                return Invoked(Math.Round(doubleValue));
            }
            if (args is [var value2, var second2] && TryConvertToDouble(value2, out double doubleValue2))
            {
                if (ArgumentParser.TryConvertToInt(second2, out int digits))
                {
                    return Invoked(Math.Round(doubleValue2, digits));
                }
                if (second2 is MidpointRounding mode)
                {
                    return Invoked(Math.Round(doubleValue2, mode));
                }
            }

            return args is [var value3, var digitsValue2, var modeValue2]
                && TryConvertToDouble(value3, out double doubleValue3)
                && ArgumentParser.TryConvertToInt(digitsValue2, out int convertedDigits2)
                && modeValue2 is MidpointRounding convertedMode2
                    ? Invoked(Math.Round(doubleValue3, convertedDigits2, convertedMode2))
                    : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeSign(object?[] args)
        {
            if (args is not [var value])
            {
                return NotHandled;
            }

            return value switch
            {
                decimal number => Invoked(Math.Sign(number)),
                double number => Invoked(Math.Sign(number)),
                float number => Invoked(Math.Sign(number)),
                long number => Invoked(Math.Sign(number)),
                int number => Invoked(Math.Sign(number)),
                short number => Invoked(Math.Sign(number)),
                sbyte number => Invoked(Math.Sign(number)),
                byte number => Invoked(Math.Sign((short)number)),
                ushort number => Invoked(Math.Sign((int)number)),
                uint number => Invoked(Math.Sign((long)number)),
                ulong number => Invoked(Math.Sign((double)number)),
                string text when ArgumentParser.TryConvertToDouble(text, out double number) => Invoked(Math.Sign(number)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeTruncate(object?[] args)
        {
            if (args is [decimal decimalValue])
            {
                return Invoked(Math.Truncate(decimalValue));
            }

            return args is [var value] && TryConvertToDouble(value, out double doubleValue)
                ? Invoked(Math.Truncate(doubleValue))
                : NotHandled;
        }

        private static bool TryConvertToDouble(object? value, out double result)
            => ArgumentParser.TryConvertToDouble(value, out result)
                || (value is not null && ArgumentParser.TryWidenToDouble(value, out result));

        private static bool TryConvertToInt(object? value, out int result)
            => ArgumentParser.TryConvertToInt(value, out result)
                || (ArgumentParser.TryWidenPrimitive(value, typeof(int), out object? widened) && TryGetValue(widened, out result));

        private static bool TryGetCommonNumericType(object? left, object? right, out TypeCode result)
        {
            if (TryGetNumericTypeCode(left, out TypeCode leftType)
                && TryGetNumericTypeCode(right, out TypeCode rightType))
            {
                return TryGetCommonNumericType(leftType, rightType, out result);
            }

            result = default;
            return false;
        }

        private static bool TryGetCommonNumericType(object? value, object? min, object? max, out TypeCode result)
        {
            if (TryGetNumericTypeCode(value, out TypeCode valueType)
                && TryGetNumericTypeCode(min, out TypeCode minType)
                && TryGetNumericTypeCode(max, out TypeCode maxType))
            {
                return TryGetCommonNumericType(valueType, minType, maxType, out result);
            }

            result = default;
            return false;
        }

        private static bool TryGetCommonNumericType(TypeCode first, TypeCode second, out TypeCode result)
        {
            // Match the reflection binder by selecting the narrowest Math overload to which every boxed argument can widen.
            if (CanConvertTo(first, second, TypeCode.SByte))
            {
                return SetResult(TypeCode.SByte, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Byte))
            {
                return SetResult(TypeCode.Byte, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Int16))
            {
                return SetResult(TypeCode.Int16, out result);
            }
            if (CanConvertTo(first, second, TypeCode.UInt16))
            {
                return SetResult(TypeCode.UInt16, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Int32))
            {
                return SetResult(TypeCode.Int32, out result);
            }
            if (CanConvertTo(first, second, TypeCode.UInt32))
            {
                return SetResult(TypeCode.UInt32, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Int64))
            {
                return SetResult(TypeCode.Int64, out result);
            }
            if (CanConvertTo(first, second, TypeCode.UInt64))
            {
                return SetResult(TypeCode.UInt64, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Single))
            {
                return SetResult(TypeCode.Single, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Double))
            {
                return SetResult(TypeCode.Double, out result);
            }
            if (CanConvertTo(first, second, TypeCode.Decimal))
            {
                return SetResult(TypeCode.Decimal, out result);
            }

            result = default;
            return false;
        }

        private static bool TryGetCommonNumericType(TypeCode first, TypeCode second, TypeCode third, out TypeCode result)
        {
            if (CanConvertTo(first, second, third, TypeCode.SByte))
            {
                return SetResult(TypeCode.SByte, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Byte))
            {
                return SetResult(TypeCode.Byte, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Int16))
            {
                return SetResult(TypeCode.Int16, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.UInt16))
            {
                return SetResult(TypeCode.UInt16, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Int32))
            {
                return SetResult(TypeCode.Int32, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.UInt32))
            {
                return SetResult(TypeCode.UInt32, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Int64))
            {
                return SetResult(TypeCode.Int64, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.UInt64))
            {
                return SetResult(TypeCode.UInt64, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Single))
            {
                return SetResult(TypeCode.Single, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Double))
            {
                return SetResult(TypeCode.Double, out result);
            }
            if (CanConvertTo(first, second, third, TypeCode.Decimal))
            {
                return SetResult(TypeCode.Decimal, out result);
            }

            result = default;
            return false;
        }

        private static bool CanConvertTo(TypeCode first, TypeCode second, TypeCode target)
            => CanConvertTo(first, target) && CanConvertTo(second, target);

        private static bool CanConvertTo(TypeCode first, TypeCode second, TypeCode third, TypeCode target)
            => CanConvertTo(first, target) && CanConvertTo(second, target) && CanConvertTo(third, target);

        private static bool CanConvertTo(TypeCode source, TypeCode target)
            => source == target || ArgumentParser.CanWidenPrimitive(source, target);

        private static bool SetResult(TypeCode value, out TypeCode result)
        {
            result = value;
            return true;
        }

        private static bool TryGetNumericTypeCode(object? value, out TypeCode result)
        {
            result = value switch
            {
                char => TypeCode.Char,
                sbyte => TypeCode.SByte,
                byte => TypeCode.Byte,
                short => TypeCode.Int16,
                ushort => TypeCode.UInt16,
                int => TypeCode.Int32,
                uint => TypeCode.UInt32,
                long => TypeCode.Int64,
                ulong => TypeCode.UInt64,
                float => TypeCode.Single,
                double => TypeCode.Double,
                decimal => TypeCode.Decimal,
                _ => TypeCode.Empty,
            };

            return result != TypeCode.Empty;
        }

        private static bool TryGetValue<T>(object? value, out T result)
        {
            if (value is T converted)
            {
                result = converted;
                return true;
            }

            result = default!;
            return false;
        }

        private static WellKnownFunctionResult InvokeMinMax(sbyte left, sbyte right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(byte left, byte right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(short left, short right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(ushort left, ushort right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(int left, int right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(uint left, uint right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(long left, long right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(ulong left, ulong right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(float left, float right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(double left, double right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));

        private static WellKnownFunctionResult InvokeMinMax(decimal left, decimal right, bool maximum)
            => Invoked(maximum ? Math.Max(left, right) : Math.Min(left, right));
    }
}
