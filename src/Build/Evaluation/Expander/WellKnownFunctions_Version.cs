// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class VersionHandler
    {
        internal bool TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 5 && name.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(ref arguments, out result);
            }

            return NotHandled(out result);
        }

        internal bool TryInvokeInstance(
            Version receiver,
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 8 && name.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeToString(receiver, ref arguments, out result);
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeParse(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = Version.Parse(value);
                return true;
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeToString(
            Version receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out int fieldCount))
            {
                result = receiver.ToString(fieldCount);
                return true;
            }

            return NotHandled(out result);
        }
    }
}
