// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class VersionHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                5 when methodName.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeParse(args),

                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeInstance(string methodName, Version version, object?[] args)
            => methodName.Length switch
            {
                8 when methodName.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(version, args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeParse(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Version.Parse(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeToString(Version version, object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out int arg0)
                ? Invoked(version.ToString(arg0))
                : NotHandled;
    }
}
