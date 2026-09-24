// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class PrimitiveHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(Type receiverType, string methodName, object?[] args)
        {
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
                    : receiverType == typeof(decimal) ? Invoked(decimal.MaxValue)
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
                    : receiverType == typeof(decimal) ? Invoked(decimal.MinValue)
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

            if (receiverType == typeof(decimal))
            {
                return methodName.Equals(nameof(decimal.MinusOne), StringComparison.OrdinalIgnoreCase) ? Invoked(decimal.MinusOne)
                    : methodName.Equals(nameof(decimal.One), StringComparison.OrdinalIgnoreCase) ? Invoked(decimal.One)
                    : methodName.Equals(nameof(decimal.Zero), StringComparison.OrdinalIgnoreCase) ? Invoked(decimal.Zero)
                    : NotHandled;
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
