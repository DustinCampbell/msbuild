// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringArrayHandler
    {
        public WellKnownFunctionResult TryInvokeInstance(string methodName, string[] stringArray, object?[] args)
            => methodName.Length switch
            {
                8 when methodName.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetValue(stringArray, args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetValue(string[] stringArray, object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out int index)
                ? Invoked(stringArray[index])
                : NotHandled;
    }
}
