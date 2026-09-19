// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class Int64Handler
    {
        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length switch
            {
                8 when name.Equals(nameof(long.MaxValue), StringComparison.OrdinalIgnoreCase)
                    => Handled(long.MaxValue),
                8 when name.Equals(nameof(long.MinValue), StringComparison.OrdinalIgnoreCase)
                    => Handled(long.MinValue),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(long value, StringSegment name, ref FunctionArguments arguments)
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

        private static WellKnownMemberResult TryInvokeCompareTo(long value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out long other))
            {
                return Handled(value.CompareTo(other));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEquals(long value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out long obj))
            {
                return Handled(value.Equals(obj));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTypeCode(long value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(value.GetTypeCode());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(long value, ref FunctionArguments arguments)
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
