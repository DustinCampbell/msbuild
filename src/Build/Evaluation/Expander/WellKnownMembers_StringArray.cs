// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class StringArrayHandler
    {
        internal WellKnownMemberResult TryInvokeInstance(string[] value, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                8 when name.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetValue(value, ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeGetValue(string[] value, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int index))
            {
                return Handled(value[index]);
            }

            return NotRecognized;
        }
    }
}
