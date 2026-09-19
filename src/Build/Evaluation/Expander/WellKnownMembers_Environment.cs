// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class EnvironmentHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                13 when name.Equals(nameof(Environment.GetFolderPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetFolderPath(ref arguments),
                22 when name.Equals(nameof(Environment.GetEnvironmentVariable), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetEnvironmentVariable(ref arguments),
                23 when name.Equals(nameof(Environment.GetEnvironmentVariables), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetEnvironmentVariables(ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeGetEnvironmentVariable(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? variable))
            {
                return Handled(Environment.GetEnvironmentVariable(variable));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetEnvironmentVariables(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(Environment.GetEnvironmentVariables());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetFolderPath(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out Environment.SpecialFolder folder))
            {
                return Handled(Environment.GetFolderPath(folder));
            }

            return NotRecognized;
        }
    }
}
