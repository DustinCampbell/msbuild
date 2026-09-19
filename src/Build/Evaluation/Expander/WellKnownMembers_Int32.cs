// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class Int32Handler
    {
        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length switch
            {
                8 when name.Equals(nameof(int.MaxValue), StringComparison.OrdinalIgnoreCase)
                    => Handled(int.MaxValue),
                8 when name.Equals(nameof(int.MinValue), StringComparison.OrdinalIgnoreCase)
                    => Handled(int.MinValue),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(int value, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                6 when name.Equals(nameof(double.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(value, ref arguments),
                9 when name.Equals(nameof(double.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareTo(value, ref arguments),
                8 when name.Equals(nameof(int.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(value, ref arguments),
                11 when name.Equals(nameof(int.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTypeCode(value, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeCompareTo(int value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int other))
            {
                return Handled(value.CompareTo(other));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEquals(int value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int obj))
            {
                return Handled(value.Equals(obj));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTypeCode(int value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(value.GetTypeCode());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(int value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(value.ToString());
            }

            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? format))
            {
                return Handled(value.ToString(format));
            }

            return NotRecognized;
        }
    }
}
