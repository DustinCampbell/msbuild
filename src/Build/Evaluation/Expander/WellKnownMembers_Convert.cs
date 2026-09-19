// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class ConvertHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                6 when name.Equals(nameof(Convert.ToByte), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToByte(ref arguments),
                6 when name.Equals(nameof(Convert.ToChar), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToChar(ref arguments),
                7 when name.Equals(nameof(Convert.ToInt16), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToInt16(ref arguments),
                7 when name.Equals(nameof(Convert.ToInt32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToInt32(ref arguments),
                7 when name.Equals(nameof(Convert.ToInt64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToInt64(ref arguments),
                7 when name.Equals(nameof(Convert.ToSByte), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToSByte(ref arguments),
                8 when name.Equals(nameof(Convert.ToDouble), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToDouble(ref arguments),
                8 when name.Equals(nameof(Convert.ToSingle), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToSingle(ref arguments),
                8 when name.Equals(nameof(Convert.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(ref arguments),
                8 when name.Equals(nameof(Convert.ToUInt16), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUInt16(ref arguments),
                8 when name.Equals(nameof(Convert.ToUInt32), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUInt32(ref arguments),
                8 when name.Equals(nameof(Convert.ToUInt64), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUInt64(ref arguments),
                9 when name.Equals(nameof(Convert.ToBoolean), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToBoolean(ref arguments),
                9 when name.Equals(nameof(Convert.ToDecimal), StringComparison.OrdinalIgnoreCase)
                     => TryInvokeToDecimal(ref arguments),
                10 when name.Equals(nameof(Convert.ChangeType), StringComparison.OrdinalIgnoreCase)
                     => TryInvokeChangeType(ref arguments),
                11 when name.Equals(nameof(Convert.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                     => TryInvokeGetTypeCode(ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeGetTypeCode(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1)
            {
                return Handled(Convert.GetTypeCode(arguments.GetValue(0)));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToBoolean(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToBoolean(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToByte(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToByte(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToByte(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeChangeType(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out TypeCode typeCode))
            {
                return Handled(Convert.ChangeType(value, typeCode));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToChar(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToChar(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToDecimal(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToDecimal(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToDouble(ref FunctionArguments arguments)
        {
            if (arguments.Count != 1)
            {
                return NotRecognized;
            }

            object? value = arguments.GetValue(0);
            if (FunctionArgumentCoercion.TryCoerce(value, out long longValue))
            {
                return Handled(Convert.ToDouble(longValue));
            }

            if (FunctionArgumentCoercion.TryCoerce(value, out string? stringValue))
            {
                return Handled(Convert.ToDouble(stringValue));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToInt16(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToInt16(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToInt16(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToInt32(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToInt32(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToInt32(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToInt64(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToInt64(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToInt64(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToSByte(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToSByte(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToSByte(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToSingle(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToSingle(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToString(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToUInt16(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToUInt16(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToUInt16(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToUInt32(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToUInt32(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToUInt32(value, fromBase));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToUInt64(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(Convert.ToUInt64(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out int fromBase))
            {
                return Handled(Convert.ToUInt64(value, fromBase));
            }

            return NotRecognized;
        }
    }
}
