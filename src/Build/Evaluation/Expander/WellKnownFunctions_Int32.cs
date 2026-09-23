// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class Int32Handler
    {
        public WellKnownFunctionResult TryInvokeInstance(string methodName, int integer, object?[] args)
            => methodName.Length switch
            {
                8 when methodName.Equals(nameof(int.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(integer, args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToString(int integer, object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(integer.ToString(arg0))
                : NotHandled;
    }
}
