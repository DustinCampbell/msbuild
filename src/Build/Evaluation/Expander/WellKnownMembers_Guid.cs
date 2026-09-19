// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class GuidHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                7 when name.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNewGuid(ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeNewGuid(ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(Guid.NewGuid());
            }

            return NotRecognized;
        }
    }
}
