// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class DecimalHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
            => args switch
            {
                [int value] => Invoked(new decimal(value)),
                [uint value] => Invoked(new decimal(value)),
                [long value] => Invoked(new decimal(value)),
                [ulong value] => Invoked(new decimal(value)),
                [float value] => Invoked(new decimal(value)),
                [double value] => Invoked(new decimal(value)),
                [int[] bits] => Invoked(new decimal(bits)),
                [var value] when TryConvertConstructorArgument(value, out decimal converted) => Invoked(converted),
                [var lo, var mid, var hi, var isNegative, var scale]
                    when TryConvertToInt32(lo, out int convertedLo) &&
                         TryConvertToInt32(mid, out int convertedMid) &&
                         TryConvertToInt32(hi, out int convertedHi) &&
                         TryConvertToBoolean(isNegative, out bool convertedIsNegative) &&
                         TryConvertToByte(scale, out byte convertedScale)
                    => Invoked(new decimal(convertedLo, convertedMid, convertedHi, convertedIsNegative, convertedScale)),
                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(decimal.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(args);
            }

            if (args is [])
            {
                return TryInvokeField(methodName);
            }

            if (methodName.Equals(nameof(decimal.FromOACurrency), StringComparison.OrdinalIgnoreCase))
            {
                return args is [var value] && TryConvertToInt64(value, out long converted)
                    ? Invoked(decimal.FromOACurrency(converted))
                    : NotHandled;
            }

            if (methodName.Equals(nameof(decimal.Round), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeRound(args);
            }

            if (TryGetDecimal(args, 0, out decimal first))
            {
                if (args is [_])
                {
                    WellKnownFunctionResult result = TryInvokeUnary(methodName, first);
                    if (result.Status == WellKnownFunctionStatus.Invoked)
                    {
                        return result;
                    }
                }

                if (TryGetDecimal(args, 1, out decimal second))
                {
                    if (args is [_, _])
                    {
                        return TryInvokeBinary(methodName, first, second);
                    }

#if NET
                    if (args is [_, _, _] &&
                        methodName.Equals(nameof(decimal.Clamp), StringComparison.OrdinalIgnoreCase) &&
                        TryGetDecimal(args, 2, out decimal third))
                    {
                        return Invoked(decimal.Clamp(first, second, third));
                    }
#endif
                }
            }

            return TryInvokeConversionOperator(methodName, args);
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, decimal value, object?[] args)
        {
#if NET
            if (args is [] && methodName.Equals(nameof(decimal.Scale), StringComparison.OrdinalIgnoreCase))
            {
                return Invoked(value.Scale);
            }
#endif

            return s_primitiveHandler.TryInvokeInstance(methodName, value, args);
        }

        private static WellKnownFunctionResult TryInvokeField(string methodName)
            => methodName switch
            {
                var name when name.Equals(nameof(decimal.MaxValue), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.MaxValue),
                var name when name.Equals(nameof(decimal.MinValue), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.MinValue),
                var name when name.Equals(nameof(decimal.MinusOne), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.MinusOne),
                var name when name.Equals(nameof(decimal.One), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.One),
                var name when name.Equals(nameof(decimal.Zero), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Zero),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeParse(object?[] args)
            => args switch
            {
                [string value] => Invoked(decimal.Parse(value)),
                [string value, NumberStyles styles] => Invoked(decimal.Parse(value, styles)),
                [string value, IFormatProvider provider] => Invoked(decimal.Parse(value, provider)),
                [string value, null] => Invoked(decimal.Parse(value, provider: null)),
                [string value, NumberStyles styles, IFormatProvider provider] => Invoked(decimal.Parse(value, styles, provider)),
                [string value, NumberStyles styles, null] => Invoked(decimal.Parse(value, styles, provider: null)),
                [string value, var styles] when ArgumentParser.TryConvertToEnum(styles, out NumberStyles convertedStyles)
                    => Invoked(decimal.Parse(value, convertedStyles)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeRound(object?[] args)
        {
            if (!TryGetDecimal(args, 0, out decimal value))
            {
                return NotHandled;
            }

            return args switch
            {
                [_] => Invoked(decimal.Round(value)),
                [_, var decimals] when ArgumentParser.TryConvertToInt(decimals, out int convertedDecimals)
                    => Invoked(decimal.Round(value, convertedDecimals)),
                [_, MidpointRounding mode] => Invoked(decimal.Round(value, mode)),
                [_, var decimals, MidpointRounding mode] when ArgumentParser.TryConvertToInt(decimals, out int convertedDecimals)
                    => Invoked(decimal.Round(value, convertedDecimals, mode)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeUnary(string methodName, decimal value)
            => methodName switch
            {
                var name when name.Equals(nameof(decimal.Ceiling), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Ceiling(value)),
                var name when name.Equals(nameof(decimal.Floor), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Floor(value)),
                var name when name.Equals(nameof(decimal.GetBits), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.GetBits(value)),
                var name when name.Equals(nameof(decimal.Negate), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Negate(value)),
                var name when name.Equals(nameof(decimal.ToByte), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToByte(value)),
                var name when name.Equals(nameof(decimal.ToDouble), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToDouble(value)),
                var name when name.Equals(nameof(decimal.ToInt16), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToInt16(value)),
                var name when name.Equals(nameof(decimal.ToInt32), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToInt32(value)),
                var name when name.Equals(nameof(decimal.ToInt64), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToInt64(value)),
                var name when name.Equals(nameof(decimal.ToOACurrency), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToOACurrency(value)),
                var name when name.Equals(nameof(decimal.ToSByte), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToSByte(value)),
                var name when name.Equals(nameof(decimal.ToSingle), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToSingle(value)),
                var name when name.Equals(nameof(decimal.ToUInt16), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToUInt16(value)),
                var name when name.Equals(nameof(decimal.ToUInt32), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToUInt32(value)),
                var name when name.Equals(nameof(decimal.ToUInt64), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.ToUInt64(value)),
                var name when name.Equals(nameof(decimal.Truncate), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Truncate(value)),
                var name when name.Equals("op_Decrement", StringComparison.OrdinalIgnoreCase) => Invoked(value - decimal.One),
                var name when name.Equals("op_Increment", StringComparison.OrdinalIgnoreCase) => Invoked(value + decimal.One),
                var name when name.Equals("op_UnaryNegation", StringComparison.OrdinalIgnoreCase) => Invoked(-value),
                var name when name.Equals("op_UnaryPlus", StringComparison.OrdinalIgnoreCase) => Invoked(value),
#if NET
                var name when name.Equals(nameof(decimal.Abs), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Abs(value)),
                var name when name.Equals(nameof(decimal.IsCanonical), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.IsCanonical(value)),
                var name when name.Equals(nameof(decimal.IsEvenInteger), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.IsEvenInteger(value)),
                var name when name.Equals(nameof(decimal.IsInteger), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.IsInteger(value)),
                var name when name.Equals(nameof(decimal.IsNegative), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.IsNegative(value)),
                var name when name.Equals(nameof(decimal.IsOddInteger), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.IsOddInteger(value)),
                var name when name.Equals(nameof(decimal.IsPositive), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.IsPositive(value)),
                var name when name.Equals(nameof(decimal.Sign), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Sign(value)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeBinary(string methodName, decimal left, decimal right)
            => methodName switch
            {
                var name when name.Equals(nameof(decimal.Add), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Add(left, right)),
                var name when name.Equals(nameof(decimal.Compare), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Compare(left, right)),
                var name when name.Equals(nameof(decimal.Divide), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Divide(left, right)),
                var name when name.Equals(nameof(decimal.Equals), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Equals(left, right)),
                var name when name.Equals(nameof(decimal.Multiply), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Multiply(left, right)),
                var name when name.Equals(nameof(decimal.Remainder), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Remainder(left, right)),
                var name when name.Equals(nameof(decimal.Subtract), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Subtract(left, right)),
                var name when name.Equals("op_Addition", StringComparison.OrdinalIgnoreCase) => Invoked(left + right),
                var name when name.Equals("op_Division", StringComparison.OrdinalIgnoreCase) => Invoked(left / right),
                var name when name.Equals("op_Equality", StringComparison.OrdinalIgnoreCase) => Invoked(left == right),
                var name when name.Equals("op_GreaterThan", StringComparison.OrdinalIgnoreCase) => Invoked(left > right),
                var name when name.Equals("op_GreaterThanOrEqual", StringComparison.OrdinalIgnoreCase) => Invoked(left >= right),
                var name when name.Equals("op_Inequality", StringComparison.OrdinalIgnoreCase) => Invoked(left != right),
                var name when name.Equals("op_LessThan", StringComparison.OrdinalIgnoreCase) => Invoked(left < right),
                var name when name.Equals("op_LessThanOrEqual", StringComparison.OrdinalIgnoreCase) => Invoked(left <= right),
                var name when name.Equals("op_Modulus", StringComparison.OrdinalIgnoreCase) => Invoked(left % right),
                var name when name.Equals("op_Multiply", StringComparison.OrdinalIgnoreCase) => Invoked(left * right),
                var name when name.Equals("op_Subtraction", StringComparison.OrdinalIgnoreCase) => Invoked(left - right),
#if NET
                var name when name.Equals(nameof(decimal.CopySign), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.CopySign(left, right)),
                var name when name.Equals(nameof(decimal.Max), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Max(left, right)),
                var name when name.Equals(nameof(decimal.MaxMagnitude), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.MaxMagnitude(left, right)),
                var name when name.Equals(nameof(decimal.Min), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.Min(left, right)),
                var name when name.Equals(nameof(decimal.MinMagnitude), StringComparison.OrdinalIgnoreCase) => Invoked(decimal.MinMagnitude(left, right)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeConversionOperator(string methodName, object?[] args)
        {
            if (args is not [var value])
            {
                return NotHandled;
            }

            if (methodName.Equals("op_Implicit", StringComparison.OrdinalIgnoreCase))
            {
                return value switch
                {
                    byte number => Invoked((decimal)number),
                    sbyte number => Invoked((decimal)number),
                    short number => Invoked((decimal)number),
                    ushort number => Invoked((decimal)number),
                    int number => Invoked((decimal)number),
                    uint number => Invoked((decimal)number),
                    long number => Invoked((decimal)number),
                    ulong number => Invoked((decimal)number),
                    char number => Invoked((decimal)number),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals("op_Explicit", StringComparison.OrdinalIgnoreCase))
            {
                return value switch
                {
                    float number => Invoked((decimal)number),
                    double number => Invoked((decimal)number),
                    _ => NotHandled,
                };
            }

            return NotHandled;
        }

        private static bool TryGetDecimal(object?[] args, int index, out decimal result)
        {
            if (!args.TryGetArg(index, out object? value))
            {
                result = default;
                return false;
            }

            return TryConvertToDecimal(value, out result);
        }

        private static bool TryConvertToDecimal(object? value, out decimal result)
        {
            switch (value)
            {
                case decimal number:
                    result = number;
                    return true;
                case byte number:
                    result = number;
                    return true;
                case sbyte number:
                    result = number;
                    return true;
                case short number:
                    result = number;
                    return true;
                case ushort number:
                    result = number;
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
                case char number:
                    result = number;
                    return true;
                case float number:
                    result = (decimal)number;
                    return true;
                case double number:
                    result = (decimal)number;
                    return true;
                case bool boolean:
                    result = boolean ? decimal.One : decimal.Zero;
                    return true;
                case string text when decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out result):
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool TryConvertConstructorArgument(object? value, out decimal result)
        {
            if (value is decimal)
            {
                result = default;
                return false;
            }

            return TryConvertToDecimal(value, out result);
        }

        private static bool TryConvertToByte(object? value, out byte result)
        {
            if (value is byte number)
            {
                result = number;
                return true;
            }

            if (value is string text && byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryConvertToBoolean(object? value, out bool result)
        {
            if (value is bool boolean)
            {
                result = boolean;
                return true;
            }

            if (value is string text && bool.TryParse(text, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryConvertToInt32(object? value, out int result)
        {
            if (ArgumentParser.TryConvertToInt(value, out result))
            {
                return true;
            }

            if (ArgumentParser.TryWidenPrimitive(value, typeof(int), out object? widened) && widened is int number)
            {
                result = number;
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryConvertToInt64(object? value, out long result)
        {
            if (ArgumentParser.TryConvertToLong(value, out result))
            {
                return true;
            }

            if (ArgumentParser.TryWidenPrimitive(value, typeof(long), out object? widened) && widened is long number)
            {
                result = number;
                return true;
            }

            result = default;
            return false;
        }
    }
}
