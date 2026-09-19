// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class DateTimeHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                5 when name.Equals(nameof(DateTime.Parse), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeParse(ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length == 3 && name.Equals(nameof(DateTime.Now), StringComparison.OrdinalIgnoreCase)
                ? Handled(DateTime.Now)
                : NotRecognized;

        internal WellKnownMemberResult TryInvokeInstance(DateTime dt, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals(nameof(DateTime.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(dt, ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryGetInstance(DateTime dt, StringSegment name)
            => name.Length == 9 && name.Equals(nameof(DateTime.DayOfWeek), StringComparison.OrdinalIgnoreCase)
                ? Handled(dt.DayOfWeek)
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeParse(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? input))
            {
                return Handled(DateTime.Parse(input));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(DateTime dt, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(dt.ToString());
            }

            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? format))
            {
                return Handled(dt.ToString(format));
            }

            return NotRecognized;
        }
    }
}
