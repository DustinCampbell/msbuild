// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class VersionHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                5 when name.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase) => TryInvokeParse(ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(Version v, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(v, ref arguments),
                9 when name.Equals(nameof(Version.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareTo(v, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeCompareTo(Version v, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out Version? value))
            {
                return Handled(v.CompareTo(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeParse(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                return Handled(Version.Parse(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToString(Version v, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(v.ToString());
            }

            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int fieldCount))
            {
                return Handled(v.ToString(fieldCount));
            }

            return NotRecognized;
        }
    }
}
