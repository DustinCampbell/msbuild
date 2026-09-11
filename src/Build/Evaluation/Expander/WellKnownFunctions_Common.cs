// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringArrayHandler
    {
        internal bool TryInvokeInstance(
            string[] receiver,
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 8 && name.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeGetValue(receiver, ref arguments, out result);
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeGetValue(
            string[] receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out int index))
            {
                result = receiver[index];
                return true;
            }

            return NotHandled(out result);
        }
    }

    private sealed class MathHandler
    {
        internal bool TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 3)
            {
                if (name.Equals(nameof(Math.Max), StringComparison.OrdinalIgnoreCase))
                {
                    return TryInvokeMax(ref arguments, out result);
                }

                if (name.Equals(nameof(Math.Min), StringComparison.OrdinalIgnoreCase))
                {
                    return TryInvokeMin(ref arguments, out result);
                }
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeMax(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out double left, out double right))
            {
                result = Math.Max(left, right);
                return true;
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeMin(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out double left, out double right))
            {
                result = Math.Min(left, right);
                return true;
            }

            return NotHandled(out result);
        }
    }

    private sealed class GuidHandler
    {
        internal bool TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 7 && name.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeNewGuid(ref arguments, out result);
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeNewGuid(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = Guid.NewGuid();
                return true;
            }

            return NotHandled(out result);
        }
    }

    private sealed class CharHandler
    {
        internal bool TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 7 && name.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeIsDigit(ref arguments, out result);
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeIsDigit(ref FunctionArguments arguments, out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out char value):
                    result = char.IsDigit(value);
                    return true;

                case 2 when arguments.TryGetArgs(out string? value, out int index):
                    result = char.IsDigit(value, index);
                    return true;

                default:
                    return NotHandled(out result);
            }
        }
    }

    private sealed class RegexHandler
    {
        internal bool TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 7 && name.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeReplace(ref arguments, out result);
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeReplace(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArgs(out string? input, out string? pattern, out string? replacement))
            {
                result = Regex.Replace(input, pattern, replacement);
                return true;
            }

            return NotHandled(out result);
        }
    }

    private sealed class Int32Handler
    {
        internal bool TryInvokeInstance(
            int receiver,
            StringSegment name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 8 && name.Equals(nameof(int.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeToString(receiver, ref arguments, out result);
            }

            return NotHandled(out result);
        }

        private static bool TryInvokeToString(
            int receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? format))
            {
                result = receiver.ToString(format);
                return true;
            }

            return NotHandled(out result);
        }
    }
}
