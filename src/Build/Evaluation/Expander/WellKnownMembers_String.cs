// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class StringHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                4 when name.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCopy(ref arguments),
                6 when name.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(ref arguments),
                13 when name.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsNullOrEmpty(ref arguments),
                18 when name.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsNullOrWhiteSpace(ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryInvokeInstance(string s, StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                4 when name.Equals(nameof(string.Trim), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrim(s, ref arguments),
                5 when name.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSplit(s, ref arguments),
                6 when name.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(s, ref arguments),
                6 when name.Equals(nameof(string.Insert), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeInsert(s, ref arguments),
                7 when name.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIndexOf(s, ref arguments),
                7 when name.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase)
                    => TryInvokePadLeft(s, ref arguments),
                7 when name.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReplace(s, ref arguments),
                7 when name.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToLower(s, ref arguments),
                7 when name.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrimEnd(s, ref arguments),
                8 when name.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeContains(s, ref arguments),
                8 when name.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEndsWith(s, ref arguments),
                8 when name.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase)
                    => TryInvokePadRight(s, ref arguments),
                8 when name.Equals(nameof(string.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(s, ref arguments),
                9 when name.Equals(nameof(string.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareTo(s, ref arguments),
                9 when name.Equals("get_Chars", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetChars(s, ref arguments),
                9 when name.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubstring(s, ref arguments),
                9 when name.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrimStart(s, ref arguments),
                10 when name.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIndexOfAny(s, ref arguments),
                10 when name.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStartsWith(s, ref arguments),
                11 when name.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLastIndexOf(s, ref arguments),
                11 when name.Equals(nameof(string.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTypeCode(ref arguments),
                11 when name.Equals(nameof(string.ToCharArray), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToCharArray(s, ref arguments),
                14 when name.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLastIndexOfAny(s, ref arguments),
                16 when name.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToLowerInvariant(s, ref arguments),
                16 when name.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUpperInvariant(s, ref arguments),

                _ => NotRecognized,
            };

        internal WellKnownMemberResult TryGetInstance(string s, StringSegment name)
            => name.Length == 6 && name.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase)
                ? Handled(s.Length)
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeIsNullOrWhiteSpace(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(string.IsNullOrWhiteSpace(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsNullOrEmpty(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(string.IsNullOrEmpty(value));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeCopy(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(value);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeStartsWith(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(s.StartsWith(value!, StringComparison.CurrentCulture));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeReplace(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? oldValue) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out string? newValue))
            {
                return Handled(s.Replace(oldValue!, newValue));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeContains(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(s.Contains(value!));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeCompareTo(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(string.Compare(s, value, StringComparison.CurrentCulture));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTypeCode(ref FunctionArguments arguments)
            => arguments.Count == 0
                ? Handled(TypeCode.String)
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeInsert(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrDefault(arguments.GetValue(0), out int startIndex) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out string? value))
            {
                return Handled(s.Insert(startIndex, value!));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToUpperInvariant(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(s.ToUpperInvariant());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToLowerInvariant(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(s.ToLowerInvariant());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEndsWith(string s, ref FunctionArguments arguments)
        {
            switch (arguments.Count)
            {
                case 1:
                {
                    return FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value)
                        ? Handled(s.EndsWith(value!, StringComparison.CurrentCulture))
                        : NotRecognized;
                }

                case 2:
                {
                    object? valueArgument = arguments.GetValue(0);
                    object? comparisonArgument = arguments.GetValue(1);
                    return FunctionArgumentCoercion.TryCoerceOrNull(valueArgument, out string? value) &&
                           FunctionArgumentCoercion.TryCoerce(comparisonArgument, out StringComparison comparison)
                        ? Handled(s.EndsWith(value!, comparison))
                        : NotRecognized;
                }
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToLower(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(s.ToLower());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIndexOf(string s, ref FunctionArguments arguments)
        {
            switch (arguments.Count)
            {
                case 1:
                {
                    return FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value)
                        ? Handled(s.IndexOf(value!, StringComparison.CurrentCulture))
                        : NotRecognized;
                }

                case 2:
                {
                    object? valueArgument = arguments.GetValue(0);
                    object? secondArgument = arguments.GetValue(1);
                    if (!FunctionArgumentCoercion.TryCoerceOrNull(valueArgument, out string? value))
                    {
                        return NotRecognized;
                    }

                    if (FunctionArgumentCoercion.TryCoerceOrDefault(secondArgument, out int startIndex))
                    {
                        return Handled(s.IndexOf(value!, startIndex, StringComparison.CurrentCulture));
                    }

                    return FunctionArgumentCoercion.TryCoerce(secondArgument, out StringComparison comparison)
                        ? Handled(s.IndexOf(value!, comparison))
                        : NotRecognized;
                }

                case 3:
                {
                    object? valueArgument = arguments.GetValue(0);
                    object? startIndexArgument = arguments.GetValue(1);
                    object? thirdArgument = arguments.GetValue(2);
                    if (!FunctionArgumentCoercion.TryCoerceOrNull(valueArgument, out string? value) ||
                        !FunctionArgumentCoercion.TryCoerceOrDefault(startIndexArgument, out int startIndex))
                    {
                        return NotRecognized;
                    }

                    if (FunctionArgumentCoercion.TryCoerceOrDefault(thirdArgument, out int count))
                    {
                        return Handled(s.IndexOf(value!, startIndex, count, StringComparison.CurrentCulture));
                    }

                    return FunctionArgumentCoercion.TryCoerce(thirdArgument, out StringComparison comparison)
                        ? Handled(s.IndexOf(value!, startIndex, comparison))
                        : NotRecognized;
                }

                case 4:
                {
                    object? valueArgument = arguments.GetValue(0);
                    object? startIndexArgument = arguments.GetValue(1);
                    object? countArgument = arguments.GetValue(2);
                    object? comparisonArgument = arguments.GetValue(3);
                    return FunctionArgumentCoercion.TryCoerceOrNull(valueArgument, out string? value) &&
                           FunctionArgumentCoercion.TryCoerceOrDefault(startIndexArgument, out int startIndex) &&
                           FunctionArgumentCoercion.TryCoerceOrDefault(countArgument, out int count) &&
                           FunctionArgumentCoercion.TryCoerce(comparisonArgument, out StringComparison comparison)
                        ? Handled(s.IndexOf(value!, startIndex, count, comparison))
                        : NotRecognized;
                }

                default:
                    return NotRecognized;
            }
        }

        private static WellKnownMemberResult TryInvokeIndexOfAny(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? values))
            {
                return Handled(s.AsSpan().IndexOfAny(values.AsSpan()));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeLastIndexOf(string s, ref FunctionArguments arguments)
        {
            switch (arguments.Count)
            {
                case 1:
                {
                    return FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value)
                        ? Handled(s.LastIndexOf(value!, StringComparison.CurrentCulture))
                        : NotRecognized;
                }

                case 2:
                {
                    object? valueArgument = arguments.GetValue(0);
                    object? secondArgument = arguments.GetValue(1);
                    if (!FunctionArgumentCoercion.TryCoerceOrNull(valueArgument, out string? value))
                    {
                        return NotRecognized;
                    }

                    if (FunctionArgumentCoercion.TryCoerce(secondArgument, out int startIndex))
                    {
                        return Handled(s.LastIndexOf(value!, startIndex, StringComparison.CurrentCulture));
                    }

                    return FunctionArgumentCoercion.TryCoerce(secondArgument, out StringComparison comparison)
                        ? Handled(s.LastIndexOf(value!, comparison))
                        : NotRecognized;
                }

                default:
                    return NotRecognized;
            }
        }

        private static WellKnownMemberResult TryInvokeLastIndexOfAny(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? values))
            {
                return Handled(s.AsSpan().LastIndexOfAny(values.AsSpan()));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeSubstring(string s, ref FunctionArguments arguments)
        {
            switch (arguments.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int startIndex):
                    return Handled(s.Substring(startIndex));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int startIndex) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out int length):
                    return Handled(s.Substring(startIndex, length));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeSplit(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count != 1)
            {
                return NotRecognized;
            }

            object? value = arguments.GetValue(0);
            if (FunctionArgumentCoercion.TryCoerce(value, out char separator))
            {
                return Handled(s.Split(separator));
            }

            if (value is char[] separators)
            {
                return Handled(s.Split(separators));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeToCharArray(string s, ref FunctionArguments arguments)
            => arguments.Count == 0
                ? Handled(s.ToCharArray())
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeToString(string s, ref FunctionArguments arguments)
            => arguments.Count == 0
                ? Handled(s)
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeTrim(string s, ref FunctionArguments arguments)
            => arguments.Count == 0
                ? Handled(s.Trim())
                : NotRecognized;

        private static WellKnownMemberResult TryInvokePadLeft(string s, ref FunctionArguments arguments)
        {
            switch (arguments.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int totalWidth):
                    return Handled(s.PadLeft(totalWidth));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int totalWidth) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out char paddingChar):
                    return Handled(s.PadLeft(totalWidth, paddingChar));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokePadRight(string s, ref FunctionArguments arguments)
        {
            switch (arguments.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int totalWidth):
                    return Handled(s.PadRight(totalWidth));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int totalWidth) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out char paddingChar):
                    return Handled(s.PadRight(totalWidth, paddingChar));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeTrimStart(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? trimChars) &&
                trimChars.Length > 0)
            {
                return Handled(s.TrimStart(trimChars.ToCharArray()));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeTrimEnd(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? trimChars) &&
                trimChars.Length > 0)
            {
                return Handled(s.TrimEnd(trimChars.ToCharArray()));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetChars(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out int index))
            {
                return Handled(s[index]);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEquals(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? strA) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out string? strB))
            {
                return Handled(string.Equals(strA, strB));
            }

            if (arguments.Count == 3 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out strA) &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(1), out strB) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out StringComparison comparisonType))
            {
                return Handled(string.Equals(strA, strB, comparisonType));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeEquals(string s, ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out string? value))
            {
                return Handled(s.Equals(value));
            }

            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerceOrNull(arguments.GetValue(0), out value) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out StringComparison comparisonType))
            {
                return Handled(s.Equals(value, comparisonType));
            }

            return NotRecognized;
        }
    }
}
