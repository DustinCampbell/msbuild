// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringArrayHandler
    {
        internal WellKnownFunctionResult TryInvokeInstance(
            string[] receiver,
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 8 && name.Equals(nameof(Array.GetValue), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeGetValue(receiver, ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetValue(
            string[] receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int index))
            {
                result = receiver[index];
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }

    private sealed class MathHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
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

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeMax(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out double left) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out double right))
            {
                result = Math.Max(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeMin(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out double left) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out double right))
            {
                result = Math.Min(left, right);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }

    private sealed class GuidHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 7 && name.Equals(nameof(Guid.NewGuid), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeNewGuid(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeNewGuid(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 0)
            {
                result = Guid.NewGuid();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }

    private sealed class CharHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 7 && name.Equals(nameof(char.IsDigit), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeIsDigit(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIsDigit(ref FunctionArguments arguments, out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out char value):
                    result = char.IsDigit(value);
                    return WellKnownFunctionResult.Handled;

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? value) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out int index):
                    result = char.IsDigit(value, index);
                    return WellKnownFunctionResult.Handled;

                default:
                    return NotRecognized(out result);
            }
        }
    }

    private sealed class RegexHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 7 && name.Equals(nameof(Regex.Replace), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeReplace(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeReplace(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? input) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? pattern) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out string? replacement))
            {
                result = Regex.Replace(input, pattern, replacement);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }

    private sealed class Int32Handler
    {
        internal WellKnownFunctionResult TryInvokeInstance(
            int receiver,
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (name.Length == 8 && name.Equals(nameof(int.ToString), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeToString(receiver, ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeToString(
            int receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? format))
            {
                result = receiver.ToString(format);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }
}
