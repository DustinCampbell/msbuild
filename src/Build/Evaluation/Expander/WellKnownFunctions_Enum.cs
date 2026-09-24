// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class EnumHandler
    {
        public WellKnownFunctionResult TryInvokeInstance(string methodName, Enum value, object?[] args)
            => methodName.Length switch
            {
                6 when methodName.Equals(nameof(Enum.Equals), StringComparison.OrdinalIgnoreCase)
                    => args.Length == 1 ? Invoked(value.Equals(args[0])) : NotHandled,
                7 when methodName.Equals(nameof(Enum.HasFlag), StringComparison.OrdinalIgnoreCase)
                    => args is [Enum flag] ? Invoked(value.HasFlag(flag)) : NotHandled,
                8 when methodName.Equals(nameof(Enum.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(value, args),
                9 when methodName.Equals(nameof(Enum.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => args.Length == 1 ? Invoked(value.CompareTo(args[0])) : NotHandled,
                11 when methodName.Equals(nameof(Enum.GetHashCode), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(value.GetHashCode()) : NotHandled,
                11 when methodName.Equals(nameof(Enum.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(value.GetTypeCode()) : NotHandled,

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToString(Enum value, object?[] args)
            => args switch
            {
                [] => Invoked(value.ToString()),
                [string format] => Invoked(value.ToString(format)),
                [IFormatProvider] => Invoked(value.ToString()),
                [string format, IFormatProvider] => Invoked(value.ToString(format)),
                _ => NotHandled,
            };
    }
}
