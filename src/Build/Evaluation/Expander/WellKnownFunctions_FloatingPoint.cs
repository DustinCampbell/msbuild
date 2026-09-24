// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System;
using System.Globalization;
using System.Numerics;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed partial class PrimitiveHandler
    {
        private static WellKnownFunctionResult TryInvokeFloatingPoint(Type receiverType, string methodName, object?[] args)
            => receiverType == typeof(float) ? TryInvokeFloatingPoint<float>(methodName, args)
                : receiverType == typeof(double) ? TryInvokeFloatingPoint<double>(methodName, args)
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeFloatingPoint<T>(string methodName, object?[] args)
            where T : unmanaged, IFloatingPointIeee754<T>, IMinMaxValue<T>
        {
            if (!TryGetFloatingPoint(args, 0, out T first))
            {
                return NotHandled;
            }

            if (args is [_])
            {
                return methodName switch
                {
                    var name when name.Equals(nameof(float.Abs), StringComparison.OrdinalIgnoreCase) => Invoked(T.Abs(first)),
                    var name when name.Equals(nameof(float.Acos), StringComparison.OrdinalIgnoreCase) => Invoked(T.Acos(first)),
                    var name when name.Equals(nameof(float.Acosh), StringComparison.OrdinalIgnoreCase) => Invoked(T.Acosh(first)),
                    var name when name.Equals(nameof(float.AcosPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.AcosPi(first)),
                    var name when name.Equals(nameof(float.Asin), StringComparison.OrdinalIgnoreCase) => Invoked(T.Asin(first)),
                    var name when name.Equals(nameof(float.Asinh), StringComparison.OrdinalIgnoreCase) => Invoked(T.Asinh(first)),
                    var name when name.Equals(nameof(float.AsinPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.AsinPi(first)),
                    var name when name.Equals(nameof(float.Atan), StringComparison.OrdinalIgnoreCase) => Invoked(T.Atan(first)),
                    var name when name.Equals(nameof(float.Atanh), StringComparison.OrdinalIgnoreCase) => Invoked(T.Atanh(first)),
                    var name when name.Equals(nameof(float.AtanPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.AtanPi(first)),
                    var name when name.Equals(nameof(float.BitDecrement), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.BitDecrement(first)),
                    var name when name.Equals(nameof(float.BitIncrement), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.BitIncrement(first)),
                    var name when name.Equals(nameof(float.Cbrt), StringComparison.OrdinalIgnoreCase) => Invoked(T.Cbrt(first)),
                    var name when name.Equals(nameof(float.Ceiling), StringComparison.OrdinalIgnoreCase) => Invoked(T.Ceiling(first)),
                    var name when name.Equals(nameof(float.Cos), StringComparison.OrdinalIgnoreCase) => Invoked(T.Cos(first)),
                    var name when name.Equals(nameof(float.Cosh), StringComparison.OrdinalIgnoreCase) => Invoked(T.Cosh(first)),
                    var name when name.Equals(nameof(float.CosPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.CosPi(first)),
                    var name when name.Equals(nameof(float.DegreesToRadians), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.DegreesToRadians(first)),
                    var name when name.Equals(nameof(float.Exp), StringComparison.OrdinalIgnoreCase) => Invoked(T.Exp(first)),
                    var name when name.Equals(nameof(float.Exp10), StringComparison.OrdinalIgnoreCase) => Invoked(T.Exp10(first)),
                    var name when name.Equals(nameof(float.Exp10M1), StringComparison.OrdinalIgnoreCase) => Invoked(T.Exp10M1(first)),
                    var name when name.Equals(nameof(float.Exp2), StringComparison.OrdinalIgnoreCase) => Invoked(T.Exp2(first)),
                    var name when name.Equals(nameof(float.Exp2M1), StringComparison.OrdinalIgnoreCase) => Invoked(T.Exp2M1(first)),
                    var name when name.Equals(nameof(float.ExpM1), StringComparison.OrdinalIgnoreCase) => Invoked(T.ExpM1(first)),
                    var name when name.Equals(nameof(float.Floor), StringComparison.OrdinalIgnoreCase) => Invoked(T.Floor(first)),
                    var name when name.Equals(nameof(float.ILogB), StringComparison.OrdinalIgnoreCase) => Invoked(T.ILogB(first)),
                    var name when name.Equals(nameof(float.IsEvenInteger), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.IsEvenInteger(first)),
                    var name when name.Equals(nameof(float.IsFinite), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsFinite(first)),
                    var name when name.Equals(nameof(float.IsInfinity), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsInfinity(first)),
                    var name when name.Equals(nameof(float.IsInteger), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsInteger(first)),
                    var name when name.Equals(nameof(float.IsNaN), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsNaN(first)),
                    var name when name.Equals(nameof(float.IsNegative), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsNegative(first)),
                    var name when name.Equals(nameof(float.IsNegativeInfinity), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.IsNegativeInfinity(first)),
                    var name when name.Equals(nameof(float.IsNormal), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsNormal(first)),
                    var name when name.Equals(nameof(float.IsOddInteger), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.IsOddInteger(first)),
                    var name when name.Equals(nameof(float.IsPositive), StringComparison.OrdinalIgnoreCase) => Invoked(T.IsPositive(first)),
                    var name when name.Equals(nameof(float.IsPositiveInfinity), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.IsPositiveInfinity(first)),
                    var name when name.Equals(nameof(float.IsPow2), StringComparison.OrdinalIgnoreCase) => Invoked(IsPow2(first)),
                    var name when name.Equals(nameof(float.IsRealNumber), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.IsRealNumber(first)),
                    var name when name.Equals(nameof(float.IsSubnormal), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.IsSubnormal(first)),
                    var name when name.Equals(nameof(float.Log), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log(first)),
                    var name when name.Equals(nameof(float.Log10), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log10(first)),
                    var name when name.Equals(nameof(float.Log10P1), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log10P1(first)),
                    var name when name.Equals(nameof(float.Log2), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log2(first)),
                    var name when name.Equals(nameof(float.Log2P1), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log2P1(first)),
                    var name when name.Equals(nameof(float.LogP1), StringComparison.OrdinalIgnoreCase) => Invoked(T.LogP1(first)),
                    var name when name.Equals(nameof(float.RadiansToDegrees), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.RadiansToDegrees(first)),
                    var name when name.Equals(nameof(float.ReciprocalEstimate), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.ReciprocalEstimate(first)),
                    var name when name.Equals(nameof(float.ReciprocalSqrtEstimate), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.ReciprocalSqrtEstimate(first)),
                    var name when name.Equals(nameof(float.Round), StringComparison.OrdinalIgnoreCase) => Invoked(T.Round(first)),
                    var name when name.Equals(nameof(float.Sign), StringComparison.OrdinalIgnoreCase) => Invoked(T.Sign(first)),
                    var name when name.Equals(nameof(float.Sin), StringComparison.OrdinalIgnoreCase) => Invoked(T.Sin(first)),
                    var name when name.Equals(nameof(float.SinCos), StringComparison.OrdinalIgnoreCase) => Invoked(T.SinCos(first)),
                    var name when name.Equals(nameof(float.SinCosPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.SinCosPi(first)),
                    var name when name.Equals(nameof(float.Sinh), StringComparison.OrdinalIgnoreCase) => Invoked(T.Sinh(first)),
                    var name when name.Equals(nameof(float.SinPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.SinPi(first)),
                    var name when name.Equals(nameof(float.Sqrt), StringComparison.OrdinalIgnoreCase) => Invoked(T.Sqrt(first)),
                    var name when name.Equals(nameof(float.Tan), StringComparison.OrdinalIgnoreCase) => Invoked(T.Tan(first)),
                    var name when name.Equals(nameof(float.Tanh), StringComparison.OrdinalIgnoreCase) => Invoked(T.Tanh(first)),
                    var name when name.Equals(nameof(float.TanPi), StringComparison.OrdinalIgnoreCase) => Invoked(T.TanPi(first)),
                    var name when name.Equals(nameof(float.Truncate), StringComparison.OrdinalIgnoreCase) => Invoked(T.Truncate(first)),
                    _ => NotHandled,
                };
            }

            if (args is [_, var secondArgument])
            {
                if (methodName.Equals(nameof(float.RootN), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(secondArgument, out int root))
                {
                    return Invoked(T.RootN(first, root));
                }

                if (methodName.Equals(nameof(float.Round), StringComparison.OrdinalIgnoreCase))
                {
                    if (ArgumentParser.TryConvertToEnum(secondArgument, out MidpointRounding mode))
                    {
                        return Invoked(T.Round(first, mode));
                    }

                    if (ArgumentParser.TryConvertToInt(secondArgument, out int digits))
                    {
                        return Invoked(T.Round(first, digits));
                    }
                }

                if (methodName.Equals(nameof(float.ScaleB), StringComparison.OrdinalIgnoreCase) &&
                    ArgumentParser.TryConvertToInt(secondArgument, out int scale))
                {
                    return Invoked(T.ScaleB(first, scale));
                }
            }

            if (!TryGetFloatingPoint(args, 1, out T second))
            {
                return NotHandled;
            }

            if (args is [_, _])
            {
                return methodName switch
                {
                    var name when name.Equals(nameof(float.Atan2), StringComparison.OrdinalIgnoreCase) => Invoked(T.Atan2(first, second)),
                    var name when name.Equals(nameof(float.Atan2Pi), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.Atan2Pi(first, second)),
                    var name when name.Equals(nameof(float.CopySign), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.CopySign(first, second)),
                    var name when name.Equals(nameof(float.Hypot), StringComparison.OrdinalIgnoreCase) => Invoked(T.Hypot(first, second)),
                    var name when name.Equals(nameof(float.Ieee754Remainder), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.Ieee754Remainder(first, second)),
                    var name when name.Equals(nameof(float.Log), StringComparison.OrdinalIgnoreCase) => Invoked(T.Log(first, second)),
                    var name when name.Equals(nameof(float.Max), StringComparison.OrdinalIgnoreCase) => Invoked(T.Max(first, second)),
                    var name when name.Equals(nameof(float.MaxMagnitude), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MaxMagnitude(first, second)),
                    var name when name.Equals(nameof(float.MaxMagnitudeNumber), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MaxMagnitudeNumber(first, second)),
                    var name when name.Equals(nameof(float.MaxNative), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MaxNative(first, second)),
                    var name when name.Equals(nameof(float.MaxNumber), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MaxNumber(first, second)),
                    var name when name.Equals(nameof(float.Min), StringComparison.OrdinalIgnoreCase) => Invoked(T.Min(first, second)),
                    var name when name.Equals(nameof(float.MinMagnitude), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MinMagnitude(first, second)),
                    var name when name.Equals(nameof(float.MinMagnitudeNumber), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MinMagnitudeNumber(first, second)),
                    var name when name.Equals(nameof(float.MinNative), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MinNative(first, second)),
                    var name when name.Equals(nameof(float.MinNumber), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MinNumber(first, second)),
                    var name when name.Equals(nameof(float.Pow), StringComparison.OrdinalIgnoreCase) => Invoked(T.Pow(first, second)),
                    _ => NotHandled,
                };
            }

            if (args is [_, _, _] && TryGetFloatingPoint(args, 2, out T third))
            {
                return methodName switch
                {
                    var name when name.Equals(nameof(float.Clamp), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.Clamp(first, second, third)),
                    var name when name.Equals(nameof(float.ClampNative), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.ClampNative(first, second, third)),
                    var name when name.Equals(nameof(float.FusedMultiplyAdd), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.FusedMultiplyAdd(first, second, third)),
                    var name when name.Equals(nameof(float.Lerp), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.Lerp(first, second, third)),
                    var name when name.Equals(nameof(float.MultiplyAddEstimate), StringComparison.OrdinalIgnoreCase)
                        => Invoked(T.MultiplyAddEstimate(first, second, third)),
                    _ => NotHandled,
                };
            }

            if (args is [_, var digitsArgument, var roundingModeArgument] &&
                methodName.Equals(nameof(float.Round), StringComparison.OrdinalIgnoreCase) &&
                ArgumentParser.TryConvertToInt(digitsArgument, out int convertedDigits) &&
                ArgumentParser.TryConvertToEnum(roundingModeArgument, out MidpointRounding roundingMode))
            {
                return Invoked(T.Round(first, convertedDigits, roundingMode));
            }

            return NotHandled;
        }

        private static bool TryGetFloatingPoint<T>(object?[] args, int index, out T result)
            where T : unmanaged, IFloatingPointIeee754<T>, IMinMaxValue<T>
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
                case string text when T.TryParse(
                    text,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out result):
                    return true;
                default:
                    result = default;
                    return false;
            }
        }

        private static bool IsPow2<T>(T value)
            where T : unmanaged, IFloatingPointIeee754<T>, IMinMaxValue<T>
            => typeof(T) == typeof(float)
                ? float.IsPow2(float.CreateChecked(value))
                : double.IsPow2(double.CreateChecked(value));
    }
}

#endif
