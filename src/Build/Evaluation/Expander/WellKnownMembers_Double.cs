// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class DoubleHandler
    {
        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length switch
            {
                3 when name.Equals(nameof(double.NaN), StringComparison.OrdinalIgnoreCase)
                    => Handled(double.NaN),
                8 when name.Equals(nameof(double.MaxValue), StringComparison.OrdinalIgnoreCase)
                    => Handled(double.MaxValue),
                8 when name.Equals(nameof(double.MinValue), StringComparison.OrdinalIgnoreCase)
                    => Handled(double.MinValue),
                16 when name.Equals(nameof(double.NegativeInfinity), StringComparison.OrdinalIgnoreCase)
                    => Handled(double.NegativeInfinity),
                16 when name.Equals(nameof(double.PositiveInfinity), StringComparison.OrdinalIgnoreCase)
                    => Handled(double.PositiveInfinity),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(double value, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                6 when name.Equals(nameof(double.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(value, ref arguments),
                9 when name.Equals(nameof(double.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareTo(value, ref arguments),
                8 when name.Equals(nameof(double.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(value, ref arguments),
                11 when name.Equals(nameof(double.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTypeCode(value, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeCompareTo(double value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out double other))
            {
                return Handled(value.CompareTo(other));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEquals(double value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out double obj))
            {
                return Handled(value.Equals(obj));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTypeCode(double value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(value.GetTypeCode());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(double value, ref FunctionArguments arguments)
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
