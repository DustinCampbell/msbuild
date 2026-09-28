// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class MathHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, ref Arguments args)
            => methodName.Length switch
            {
                3 when methodName.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMax(ref args),
                3 when methodName.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMin(ref args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeMax(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out double arg0)
            && args.TryGetArg(1, out double arg1)
                ? Invoked(Math.Max(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeMin(ref Arguments args)
            => args.Length == 2
            && args.TryGetArg(0, out double arg0)
            && args.TryGetArg(1, out double arg1)
                ? Invoked(Math.Min(arg0, arg1))
                : NotHandled;
    }
}
