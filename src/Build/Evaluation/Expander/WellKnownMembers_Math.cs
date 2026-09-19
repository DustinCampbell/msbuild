// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class MathHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                3 when name.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMax(ref arguments),
                3 when name.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMin(ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeMax(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out double left) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out double right))
            {
                return Handled(Math.Max(left, right));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeMin(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out double left) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out double right))
            {
                return Handled(Math.Min(left, right));
            }

            return NotRecognized;
        }
    }
}
