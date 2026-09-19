// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class TimeSpanHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                5 when name.Equals(nameof(TimeSpan.Parse), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeParse(ref arguments),
                6 when name.Equals(nameof(TimeSpan.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(TimeSpan ts, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals(nameof(TimeSpan.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(ts, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeEquals(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out TimeSpan t1) &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(1), out TimeSpan t2))
            {
                return Handled(TimeSpan.Equals(t1, t2));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeParse(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? input))
            {
                return Handled(TimeSpan.Parse(input));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(TimeSpan ts, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(ts.ToString());
            }

            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? format))
            {
                return Handled(ts.ToString(format));
            }

            return NotRecognized;
        }
    }
}
