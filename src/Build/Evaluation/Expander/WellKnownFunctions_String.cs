// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringHandler
    {
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (name.Length)
            {
                case 4 when name.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeCopy(ref arguments, out result);

                case 13 when name.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeIsNullOrEmpty(ref arguments, out result);

                case 18 when name.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeIsNullOrWhiteSpace(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        internal WellKnownFunctionResult TryInvokeInstance(
            string receiver,
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (name.Length)
            {
                case 5 when name.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeSplit(receiver, ref arguments, out result);

                case 6:
                    if (name.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeEquals(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeLength(receiver, ref arguments, out result);
                    }

                    break;

                case 7:
                    if (name.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeIndexOf(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokePadLeft(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeReplace(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeToLower(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeTrimEnd(receiver, ref arguments, out result);
                    }

                    break;

                case 8:
                    if (name.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeContains(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeEndsWith(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokePadRight(receiver, ref arguments, out result);
                    }

                    break;

                case 9:
                    if (name.Equals("get_Chars", StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetChars(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeSubstring(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeTrimStart(receiver, ref arguments, out result);
                    }

                    break;

                case 10:
                    if (name.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeIndexOfAny(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeStartsWith(receiver, ref arguments, out result);
                    }

                    break;

                case 11 when name.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeLastIndexOf(receiver, ref arguments, out result);

                case 14 when name.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeLastIndexOfAny(receiver, ref arguments, out result);

                case 16:
                    if (name.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeToLowerInvariant(receiver, ref arguments, out result);
                    }

                    if (name.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeToUpperInvariant(receiver, ref arguments, out result);
                    }

                    break;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIsNullOrWhiteSpace(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = string.IsNullOrWhiteSpace(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIsNullOrEmpty(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = string.IsNullOrEmpty(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeCopy(ref FunctionArguments arguments, out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = value;
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeStartsWith(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = receiver.StartsWith(value, StringComparison.CurrentCulture);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeReplace(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArgs(out string? oldValue, out string? newValue))
            {
                result = receiver.Replace(oldValue, newValue);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeContains(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = receiver.Contains(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeToUpperInvariant(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = receiver.ToUpperInvariant();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeToLowerInvariant(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = receiver.ToLowerInvariant();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeEndsWith(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? value):
                    result = receiver.EndsWith(value, StringComparison.CurrentCulture);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? value, out StringComparison comparison):
                    result = receiver.EndsWith(value, comparison);
                    return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeToLower(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = receiver.ToLower();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIndexOf(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArgs(out string? value, out StringComparison comparison))
            {
                result = receiver.IndexOf(value, comparison);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIndexOfAny(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? values))
            {
                result = receiver.AsSpan().IndexOfAny(values.AsSpan());
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeLastIndexOf(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? value):
                    result = receiver.LastIndexOf(value, StringComparison.CurrentCulture);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? value, out int startIndex):
                    result = receiver.LastIndexOf(value, startIndex, StringComparison.CurrentCulture);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? value, out StringComparison comparison):
                    result = receiver.LastIndexOf(value, comparison);
                    return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeLastIndexOfAny(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? values))
            {
                result = receiver.AsSpan().LastIndexOfAny(values.AsSpan());
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeLength(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = receiver.Length;
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeSubstring(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out int startIndex):
                    result = receiver.Substring(startIndex);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out int startIndex, out int length):
                    result = receiver.Substring(startIndex, length);
                    return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeSplit(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out char separator))
            {
                result = receiver.Split(separator);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokePadLeft(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out int totalWidth):
                    result = receiver.PadLeft(totalWidth);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out int totalWidth, out char paddingChar):
                    result = receiver.PadLeft(totalWidth, paddingChar);
                    return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokePadRight(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out int totalWidth):
                    result = receiver.PadRight(totalWidth);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out int totalWidth, out char paddingChar):
                    result = receiver.PadRight(totalWidth, paddingChar);
                    return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeTrimStart(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? trimChars) && trimChars.Length > 0)
            {
                result = receiver.TrimStart(trimChars.ToCharArray());
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeTrimEnd(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? trimChars) && trimChars.Length > 0)
            {
                result = receiver.TrimEnd(trimChars.ToCharArray());
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetChars(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out int index))
            {
                result = receiver[index];
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeEquals(
            string receiver,
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? value))
            {
                result = receiver.Equals(value);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }
}
