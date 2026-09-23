// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class GuidHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                7 when methodName.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNewGuid(args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeNewGuid(object?[] args)
            => args.Length == 0
                ? Invoked(Guid.NewGuid())
                : NotHandled;
    }
}
