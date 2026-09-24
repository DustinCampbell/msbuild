// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class GuidHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
        {
            if (args is [string value])
            {
                return Invoked(new Guid(value));
            }

            if (args is [byte[] bytes])
            {
                return Invoked(new Guid(bytes));
            }

            if (args is [var a, var b, var c, byte[] tail]
                && TryConvertToInt32(a, out int convertedA)
                && TryConvertToInt16(b, out short convertedB)
                && TryConvertToInt16(c, out short convertedC))
            {
                return Invoked(new Guid(convertedA, convertedB, convertedC, tail));
            }

            if (args is [var a2, var b2, var c2, var d, var e, var f, var g, var h, var i, var j, var k]
                && TryConvertToInt32(a2, out int convertedA2)
                && TryConvertToInt16(b2, out short convertedB2)
                && TryConvertToInt16(c2, out short convertedC2)
                && TryConvertToByte(d, out byte convertedD)
                && TryConvertToByte(e, out byte convertedE)
                && TryConvertToByte(f, out byte convertedF)
                && TryConvertToByte(g, out byte convertedG)
                && TryConvertToByte(h, out byte convertedH)
                && TryConvertToByte(i, out byte convertedI)
                && TryConvertToByte(j, out byte convertedJ)
                && TryConvertToByte(k, out byte convertedK))
            {
                return Invoked(new Guid(
                    convertedA2,
                    convertedB2,
                    convertedC2,
                    convertedD,
                    convertedE,
                    convertedF,
                    convertedG,
                    convertedH,
                    convertedI,
                    convertedJ,
                    convertedK));
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                5 when methodName.Equals(nameof(Guid.Empty), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(Guid.Empty) : NotHandled,
                5 when methodName.Equals(nameof(Guid.Parse), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeParse(args),
                7 when methodName.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNewGuid(args),
                10 when methodName.Equals(nameof(Guid.ParseExact), StringComparison.OrdinalIgnoreCase)
                    => args is [string input, string format] ? Invoked(Guid.ParseExact(input, format)) : NotHandled,

                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeInstance(string methodName, Guid guid, object?[] args)
        {
            if (methodName.Equals(nameof(Guid.CompareTo), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [Guid other] => Invoked(guid.CompareTo(other)),
                    [object other] => Invoked(guid.CompareTo(other)),
                    _ => NotHandled,
                };
            }

            if (methodName.Equals(nameof(Guid.Equals), StringComparison.OrdinalIgnoreCase))
            {
                return args is [object other] ? Invoked(guid.Equals(other)) : NotHandled;
            }

            if (methodName.Equals(nameof(Guid.GetHashCode), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(guid.GetHashCode()) : NotHandled;
            }

            if (methodName.Equals(nameof(Guid.ToByteArray), StringComparison.OrdinalIgnoreCase))
            {
                return args is [] ? Invoked(guid.ToByteArray()) : NotHandled;
            }

            if (methodName.Equals(nameof(Guid.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [] => Invoked(guid.ToString()),
                    [string format] => Invoked(guid.ToString(format)),
                    [string format, IFormatProvider provider] => Invoked(guid.ToString(format, provider)),
                    _ => NotHandled,
                };
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeNewGuid(object?[] args)
            => args.Length == 0
                ? Invoked(Guid.NewGuid())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeParse(object?[] args)
            => args switch
            {
                [string input] => Invoked(Guid.Parse(input)),
#if NET
                [string input, IFormatProvider provider] => Invoked(Guid.Parse(input, provider)),
#endif
                _ => NotHandled,
            };

        private static bool TryConvertToByte(object? value, out byte result)
        {
            if (value is byte byteValue)
            {
                result = byteValue;
                return true;
            }

            if (value is string text && byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryConvertToInt16(object? value, out short result)
        {
            if (value is short shortValue)
            {
                result = shortValue;
                return true;
            }

            if (value is string text && short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                return true;
            }

            result = default;
            return false;
        }

        private static bool TryConvertToInt32(object? value, out int result)
            => ArgumentParser.TryConvertToInt(value, out result);
    }
}
