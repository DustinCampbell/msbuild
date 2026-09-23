// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class MathHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                3 when methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMax(args),
                3 when methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMin(args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeMax(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out double arg0)
            && args.TryGetArg(1, out double arg1)
                ? Invoked(Math.Max(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeMin(object?[] args)
            => args.Length == 2
            && args.TryGetArg(0, out double arg0)
            && args.TryGetArg(1, out double arg1)
                ? Invoked(Math.Min(arg0, arg1))
                : NotHandled;
    }
}
