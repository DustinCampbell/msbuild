// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class RegexHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                7 when methodName.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReplace(args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeReplace(object?[] args)
            => args.Length == 3
            && args.TryGetArg(0, out string? input)
            && args.TryGetArg(1, out string? pattern)
            && args.TryGetArg(2, out string? replacement)
                ? Invoked(Regex.Replace(input, pattern, replacement))
                : NotHandled;
    }
}
