// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class CharHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                7 when methodName.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsDigit(args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeIsDigit(object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out char c)
                    => Invoked(char.IsDigit(c)),

                2 when args.TryGetArg(0, out string? s)
                    && args.TryGetArg(1, out int index)
                    => Invoked(char.IsDigit(s, index)),

                _ => NotHandled,
            };
    }
}
