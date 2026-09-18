// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class VersionHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 5 && name.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        internal WellKnownFunctionResult TryInvokeInstance(
            Version receiver,
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 8 && name.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeToString(receiver, ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeParse(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value))
            {
                result = Version.Parse(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeToString(
            Version receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int fieldCount))
            {
                result = receiver.ToString(fieldCount);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }
}
