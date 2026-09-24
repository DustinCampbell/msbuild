// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
            => args switch
            {
                [] => Invoked(string.Empty),
                [string value] => Invoked(value),
                [char[] value] => Invoked(new string(value)),
                [char[] value, var startIndex, var length]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(length, out int convertedLength)
                    => Invoked(new string(value, convertedStartIndex, convertedLength)),
                [var value, var count]
                    when ArgumentParser.TryConvertToChar(value, out char convertedValue) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount)
                    => Invoked(new string(convertedValue, convertedCount)),
                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                4 when methodName.Equals(nameof(string.Join), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeJoin(args),
                4 when methodName.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCopy(args),
                6 when methodName.Equals(nameof(string.Concat), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeConcat(args),
                6 when methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticEquals(args),
                6 when methodName.Equals(nameof(string.Format), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeFormat(args),
                6 when methodName.Equals(nameof(string.Intern), StringComparison.OrdinalIgnoreCase)
                    => args is [string value] ? Invoked(string.Intern(value)) : NotHandled,
                7 when methodName.Equals(nameof(string.Compare), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticCompare(args),
                10 when methodName.Equals(nameof(string.IsInterned), StringComparison.OrdinalIgnoreCase)
                    => args is [string value] ? Invoked(string.IsInterned(value)) : NotHandled,
                11 when methodName.Equals("op_Equality", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEqualityOperator(args, negate: false),
                13 when methodName.Equals("op_Inequality", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEqualityOperator(args, negate: true),
                14 when methodName.Equals(nameof(string.CompareOrdinal), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareOrdinal(args),
                13 when methodName.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsNullOrEmpty(args),
                18 when methodName.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsNullOrWhiteSpace(args),

                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeInstance(string methodName, string text, object?[] args)
            => methodName.Length switch
            {
                5 when methodName.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSplit(text, args),
                5 when methodName.Equals(nameof(string.Clone), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(text.Clone()) : NotHandled,
                4 when methodName.Equals(nameof(string.Trim), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrim(text, args),
                6 when methodName.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLength(text, args),
                6 when methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(text, args),
                6 when methodName.Equals(nameof(string.Insert), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeInsert(text, args),
                6 when methodName.Equals(nameof(string.Remove), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeRemove(text, args),
                6 when methodName.Equals(nameof(string.CopyTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCopyTo(text, args),
                7 when methodName.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReplace(text, args),
                7 when methodName.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToLower(text, args),
                7 when methodName.Equals(nameof(string.ToUpper), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUpper(text, args),
                7 when methodName.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIndexOf(text, args),
                7 when methodName.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase)
                    => TryInvokePadLeft(text, args),
                7 when methodName.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrimEnd(text, args),
                8 when methodName.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeContains(text, args),
                8 when methodName.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEndsWith(text, args),
                8 when methodName.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase)
                    => TryInvokePadRight(text, args),
                9 when methodName.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeSubstring(text, args),
                9 when methodName.Equals(nameof(string.Normalize), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeNormalize(text, args),
                9 when methodName.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrimStart(text, args),
                9 when methodName.Equals("get_Chars", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetChars(text, args),
                10 when methodName.Equals("get_Length", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLength(text, args),
                9 when methodName.Equals(nameof(string.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareTo(text, args),
                10 when methodName.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStartsWith(text, args),
                10 when methodName.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIndexOfAny(text, args),
                11 when methodName.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLastIndexOf(text, args),
                14 when methodName.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLastIndexOfAny(text, args),
                11 when methodName.Equals(nameof(string.GetTypeCode), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTypeCode(text, args),
                11 when methodName.Equals(nameof(string.GetHashCode), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetHashCode(text, args),
                11 when methodName.Equals(nameof(string.ToCharArray), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToCharArray(text, args),
                13 when methodName.Equals(nameof(string.GetEnumerator), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(text.GetEnumerator()) : NotHandled,
                12 when methodName.Equals(nameof(string.IsNormalized), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsNormalized(text, args),
                18 when methodName.Equals("ReplaceLineEndings", StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReplaceLineEndings(text, args),
                8 when methodName.Equals(nameof(string.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(text, args),
                16 when methodName.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUpperInvariant(text, args),
                16 when methodName.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToLowerInvariant(text, args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCompareTo(string text, object?[] args)
            => args.Length == 1 && args[0] is null or string
                ? Invoked(string.Compare(text, (string?)args[0], StringComparison.CurrentCulture))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeContains(string text, object?[] args)
            => args switch
            {
                [string value] => Invoked(text.Contains(value)),
#if NET
                [string value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.Contains(value, convertedComparison)),
                [char value] => Invoked(text.Contains(value)),
                [char value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.Contains(value, convertedComparison)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCopy(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(arg0)
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeStaticCompare(object?[] args)
        {
            if (args.Length is >= 2 and <= 4 &&
                TryGetString(args, 0, out string? left) &&
                TryGetString(args, 1, out string? right))
            {
                switch (args.Length)
                {
                    case 2:
                        return Invoked(string.Compare(left, right, StringComparison.CurrentCulture));

                    case 3 when TryConvertToBoolean(args[2], out bool ignoreCase):
                        return Invoked(string.Compare(
                            left,
                            right,
                            ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture));

                    case 3 when ArgumentParser.TryConvertToEnum(args[2], out StringComparison comparison):
                        return Invoked(string.Compare(left, right, comparison));

                    case 4 when TryConvertToBoolean(args[2], out bool ignoreCase) &&
                                     TryGetCultureInfo(args[3], out CultureInfo? culture):
                        return Invoked(string.Compare(left, right, ignoreCase, culture));

                    case 4 when TryGetCultureInfo(args[2], out CultureInfo? culture) &&
                                     ArgumentParser.TryConvertToEnum(args[3], out CompareOptions options):
                        return Invoked(string.Compare(left, right, culture, options));
                }
            }

            if (args.Length is 5 or 6 or 7 &&
                TryGetString(args, 0, out left) &&
                ArgumentParser.TryConvertToInt(args[1], out int indexA) &&
                TryGetString(args, 2, out right) &&
                ArgumentParser.TryConvertToInt(args[3], out int indexB) &&
                ArgumentParser.TryConvertToInt(args[4], out int length))
            {
                if (args.Length == 5)
                {
                    return Invoked(string.Compare(left, indexA, right, indexB, length, StringComparison.CurrentCulture));
                }

                if (args.Length == 6 && TryConvertToBoolean(args[5], out bool ignoreCase))
                {
                    return Invoked(string.Compare(
                        left,
                        indexA,
                        right,
                        indexB,
                        length,
                        ignoreCase ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture));
                }

                if (args.Length == 6 && ArgumentParser.TryConvertToEnum(args[5], out StringComparison comparison))
                {
                    return Invoked(string.Compare(left, indexA, right, indexB, length, comparison));
                }

                if (args.Length == 7 &&
                    TryConvertToBoolean(args[5], out ignoreCase) &&
                    TryGetCultureInfo(args[6], out CultureInfo? culture))
                {
                    return Invoked(string.Compare(left, indexA, right, indexB, length, ignoreCase, culture));
                }

                if (args.Length == 7 &&
                    TryGetCultureInfo(args[5], out culture) &&
                    ArgumentParser.TryConvertToEnum(args[6], out CompareOptions options))
                {
                    return Invoked(string.Compare(left, indexA, right, indexB, length, culture, options));
                }
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeCompareOrdinal(object?[] args)
        {
            if (args.Length == 2 &&
                TryGetString(args, 0, out string? left) &&
                TryGetString(args, 1, out string? right))
            {
                return Invoked(string.CompareOrdinal(left, right));
            }

            return args.Length == 5 &&
                   TryGetString(args, 0, out left) &&
                   ArgumentParser.TryConvertToInt(args[1], out int indexA) &&
                   TryGetString(args, 2, out right) &&
                   ArgumentParser.TryConvertToInt(args[3], out int indexB) &&
                   ArgumentParser.TryConvertToInt(args[4], out int length)
                ? Invoked(string.CompareOrdinal(left, indexA, right, indexB, length))
                : NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeConcat(object?[] args)
        {
            if (args is [string[] stringValues])
            {
                return Invoked(string.Concat(stringValues));
            }

            if (args is [object[] objectValues])
            {
                return Invoked(string.Concat(objectValues));
            }

            if (args is [IEnumerable<string> enumerableValues])
            {
                return Invoked(string.Concat(enumerableValues));
            }

            return args.Length switch
            {
                1 => Invoked(string.Concat(args[0])),
                2 when TryGetString(args, 0, out string? value0) && TryGetString(args, 1, out string? value1)
                    => Invoked(string.Concat(value0, value1)),
                2 => Invoked(string.Concat(args[0], args[1])),
                3 when TryGetString(args, 0, out string? value0) &&
                            TryGetString(args, 1, out string? value1) &&
                            TryGetString(args, 2, out string? value2)
                    => Invoked(string.Concat(value0, value1, value2)),
                3 => Invoked(string.Concat(args[0], args[1], args[2])),
                4 when TryGetString(args, 0, out string? value0) &&
                            TryGetString(args, 1, out string? value1) &&
                            TryGetString(args, 2, out string? value2) &&
                            TryGetString(args, 3, out string? value3)
                    => Invoked(string.Concat(value0, value1, value2, value3)),
                _ => Invoked(string.Concat(args)),
            };
        }

        private static WellKnownFunctionResult TryInvokeFormat(object?[] args)
        {
            if (args.Length == 0)
            {
                return NotHandled;
            }

            int formatIndex = 0;
            IFormatProvider? provider = null;
            if (args.Length >= 3 && args[0] is IFormatProvider or null && args[1] is string)
            {
                provider = args[0] as IFormatProvider;
                formatIndex = 1;
            }

            if (!TryGetString(args, formatIndex, out string? format))
            {
                return NotHandled;
            }

            int argumentCount = args.Length - formatIndex - 1;
            if (argumentCount == 1 && args[formatIndex + 1] is object[] values)
            {
                return Invoked(string.Format(provider, format!, values));
            }

            return argumentCount switch
            {
                1 => Invoked(string.Format(provider, format!, args[formatIndex + 1])),
                2 => Invoked(string.Format(provider, format!, args[formatIndex + 1], args[formatIndex + 2])),
                3 => Invoked(string.Format(provider, format!, args[formatIndex + 1], args[formatIndex + 2], args[formatIndex + 3])),
                > 3 => Invoked(string.Format(provider, format!, CopyArguments(args, formatIndex + 1))),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeJoin(object?[] args)
        {
            if (args.Length < 2)
            {
                return NotHandled;
            }

            if (args[0] is string separator)
            {
                if (args.Length == 2 && args[1] is string[] values)
                {
                    return Invoked(string.Join(separator, values));
                }

                if (args.Length == 4 &&
                    args[1] is string[] rangedValues &&
                    ArgumentParser.TryConvertToInt(args[2], out int startIndex) &&
                    ArgumentParser.TryConvertToInt(args[3], out int count))
                {
                    return Invoked(string.Join(separator, rangedValues, startIndex, count));
                }

                if (args.Length == 2 && args[1] is object[] objectValues)
                {
                    return Invoked(string.Join(separator, objectValues));
                }

                if (args.Length == 2 && args[1] is IEnumerable<string> enumerableValues)
                {
                    return Invoked(string.Join(separator, enumerableValues));
                }

                if (TryCopyStrings(args, 1, out string?[]? stringValues))
                {
                    return Invoked(string.Join(separator, stringValues!));
                }

                return Invoked(string.Join(separator, CopyArguments(args, 1)));
            }

#if NET
            if (args[0] is char charSeparator)
            {
                if (args.Length == 2 && args[1] is string[] values)
                {
                    return Invoked(string.Join(charSeparator, values));
                }

                if (args.Length == 4 &&
                    args[1] is string[] rangedValues &&
                    ArgumentParser.TryConvertToInt(args[2], out int startIndex) &&
                    ArgumentParser.TryConvertToInt(args[3], out int count))
                {
                    return Invoked(string.Join(charSeparator, rangedValues, startIndex, count));
                }

                if (args.Length == 2 && args[1] is object[] objectValues)
                {
                    return Invoked(string.Join(charSeparator, objectValues));
                }

                return Invoked(string.Join(charSeparator, CopyArguments(args, 1)));
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeEqualityOperator(object?[] args, bool negate)
            => args.Length == 2 &&
               TryGetString(args, 0, out string? left) &&
               TryGetString(args, 1, out string? right)
                ? Invoked(negate ? left != right : left == right)
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeEndsWith(string text, object?[] args)
            => args switch
            {
                [string value] => Invoked(text.EndsWith(value, StringComparison.CurrentCulture)),
                [string value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.EndsWith(value, convertedComparison)),
                [string value, var ignoreCase, var culture]
                    when TryConvertToBoolean(ignoreCase, out bool convertedIgnoreCase) &&
                         TryGetCultureInfo(culture, out CultureInfo? convertedCulture)
                    => Invoked(text.EndsWith(value, convertedIgnoreCase, convertedCulture)),
#if NET
                [char value] => Invoked(text.EndsWith(value)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeEquals(string text, object?[] args)
            => args switch
            {
                [var value] => Invoked(text.Equals(value)),
                [string value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.Equals(value, convertedComparison)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetChars(string text, object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out int index)
                ? Invoked(text[index])
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIndexOf(string text, object?[] args)
            => args switch
            {
#if NET
                [char value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.IndexOf(value, convertedComparison)),
#endif
                [char value] => Invoked(text.IndexOf(value)),
                [char value, var startIndex] when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                    => Invoked(text.IndexOf(value, convertedStartIndex)),
                [char value, var startIndex, var count]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount)
                    => Invoked(text.IndexOf(value, convertedStartIndex, convertedCount)),
                [string value] => Invoked(text.IndexOf(value, StringComparison.CurrentCulture)),
                [string value, var startIndex] when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                    => Invoked(text.IndexOf(value, convertedStartIndex, StringComparison.CurrentCulture)),
                [string value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.IndexOf(value, convertedComparison)),
                [string value, var startIndex, var count]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount)
                    => Invoked(text.IndexOf(value, convertedStartIndex, convertedCount, StringComparison.CurrentCulture)),
                [string value, var startIndex, var comparison]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.IndexOf(value, convertedStartIndex, convertedComparison)),
                [string value, var startIndex, var count, var comparison]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount) &&
                         ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.IndexOf(value, convertedStartIndex, convertedCount, convertedComparison)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeIndexOfAny(string text, object?[] args)
        {
            if (!TryGetCharArray(args, 0, out char[]? anyOf))
            {
                return NotHandled;
            }

            return args.Length switch
            {
                1 => Invoked(text.IndexOfAny(anyOf!)),
                2 when ArgumentParser.TryConvertToInt(args[1], out int startIndex)
                    => Invoked(text.IndexOfAny(anyOf!, startIndex)),
                3 when ArgumentParser.TryConvertToInt(args[1], out int startIndex) &&
                            ArgumentParser.TryConvertToInt(args[2], out int count)
                    => Invoked(text.IndexOfAny(anyOf!, startIndex, count)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeIsNullOrEmpty(object?[] args)
            => args.Length == 1 && args[0] is null or string
                ? Invoked(string.IsNullOrEmpty((string?)args[0]))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsNullOrWhiteSpace(object?[] args)
            => args.Length == 1 && args[0] is null or string
                ? Invoked(string.IsNullOrWhiteSpace((string?)args[0]))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTypeCode(string text, object?[] args)
            => args is [] ? Invoked(text.GetTypeCode()) : NotHandled;

        private static WellKnownFunctionResult TryInvokeInsert(string text, object?[] args)
            => args is [var startIndex, string value] && ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                ? Invoked(text.Insert(convertedStartIndex, value))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeRemove(string text, object?[] args)
            => args switch
            {
                [var startIndex] when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                    => Invoked(text.Remove(convertedStartIndex)),
                [var startIndex, var count]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount)
                    => Invoked(text.Remove(convertedStartIndex, convertedCount)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeLastIndexOf(string text, object?[] args)
            => args switch
            {
                [char value] => Invoked(text.LastIndexOf(value)),
                [char value, var startIndex] when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                    => Invoked(text.LastIndexOf(value, convertedStartIndex)),
                [char value, var startIndex, var count]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount)
                    => Invoked(text.LastIndexOf(value, convertedStartIndex, convertedCount)),
                [string value] => Invoked(text.LastIndexOf(value, StringComparison.CurrentCulture)),
                [string value, var startIndex] when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                    => Invoked(text.LastIndexOf(value, convertedStartIndex, StringComparison.CurrentCulture)),
                [string value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.LastIndexOf(value, convertedComparison)),
                [string value, var startIndex, var count]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount)
                    => Invoked(text.LastIndexOf(value, convertedStartIndex, convertedCount, StringComparison.CurrentCulture)),
                [string value, var startIndex, var comparison]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.LastIndexOf(value, convertedStartIndex, convertedComparison)),
                [string value, var startIndex, var count, var comparison]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(count, out int convertedCount) &&
                         ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.LastIndexOf(value, convertedStartIndex, convertedCount, convertedComparison)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeLastIndexOfAny(string text, object?[] args)
        {
            if (!TryGetCharArray(args, 0, out char[]? anyOf))
            {
                return NotHandled;
            }

            return args.Length switch
            {
                1 => Invoked(text.LastIndexOfAny(anyOf!)),
                2 when ArgumentParser.TryConvertToInt(args[1], out int startIndex)
                    => Invoked(text.LastIndexOfAny(anyOf!, startIndex)),
                3 when ArgumentParser.TryConvertToInt(args[1], out int startIndex) &&
                            ArgumentParser.TryConvertToInt(args[2], out int count)
                    => Invoked(text.LastIndexOfAny(anyOf!, startIndex, count)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeLength(string text, object?[] args)
            => args.Length == 0
                ? Invoked(text.Length)
                : NotHandled;

        private static WellKnownFunctionResult TryInvokePadLeft(string text, object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out int totalWidth)
                    => Invoked(text.PadLeft(totalWidth)),

                2 when args.TryGetArg(0, out int totalWidth)
                    && args.TryGetArg(1, out char paddingChar)
                    => Invoked(text.PadLeft(totalWidth, paddingChar)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokePadRight(string text, object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out int totalWidth)
                    => Invoked(text.PadRight(totalWidth)),

                2 when args.TryGetArg(0, out int totalWidth)
                    && args.TryGetArg(1, out char paddingChar)
                    => Invoked(text.PadRight(totalWidth, paddingChar)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeReplace(string text, object?[] args)
            => args switch
            {
                [char oldChar, char newChar] => Invoked(text.Replace(oldChar, newChar)),
                [string oldValue, var newValue] when newValue is null or string
                    => Invoked(text.Replace(oldValue, (string?)newValue)),
#if NET
                [string oldValue, var newValue, var comparison]
                    when newValue is null or string &&
                         ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.Replace(oldValue, (string?)newValue, convertedComparison)),
                [string oldValue, var newValue, var ignoreCase, var culture]
                    when newValue is null or string &&
                         TryConvertToBoolean(ignoreCase, out bool convertedIgnoreCase) &&
                         TryGetCultureInfo(culture, out CultureInfo? convertedCulture)
                    => Invoked(text.Replace(oldValue, (string?)newValue, convertedIgnoreCase, convertedCulture)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeSplit(string text, object?[] args)
        {
            if (args.Length == 0)
            {
                return Invoked(text.Split());
            }

            if (args[0] is string[] stringSeparators)
            {
                return args.Length switch
                {
                    2 when ArgumentParser.TryConvertToEnum(args[1], out StringSplitOptions options)
                        => Invoked(text.Split(stringSeparators, options)),
                    3 when ArgumentParser.TryConvertToInt(args[1], out int count) &&
                                ArgumentParser.TryConvertToEnum(args[2], out StringSplitOptions options)
                        => Invoked(text.Split(stringSeparators, count, options)),
                    _ => NotHandled,
                };
            }

#if NET
            if (args[0] is string separator)
            {
                if (args.Length == 2 && ArgumentParser.TryConvertToEnum(args[1], out StringSplitOptions options))
                {
                    return Invoked(text.Split(separator, options));
                }

                if (args.Length == 3 &&
                    ArgumentParser.TryConvertToInt(args[1], out int count) &&
                    ArgumentParser.TryConvertToEnum(args[2], out options))
                {
                    return Invoked(text.Split(separator, count, options));
                }
            }

            if (args[0] is char charSeparator)
            {
                if (args.Length == 2 && ArgumentParser.TryConvertToEnum(args[1], out StringSplitOptions options))
                {
                    return Invoked(text.Split(charSeparator, options));
                }

                if (args.Length == 3 &&
                    ArgumentParser.TryConvertToInt(args[1], out int count) &&
                    ArgumentParser.TryConvertToEnum(args[2], out options))
                {
                    return Invoked(text.Split(charSeparator, count, options));
                }
            }
#endif

            if (!TryGetCharArray(args, 0, out char[]? separators))
            {
                return NotHandled;
            }

            return args.Length switch
            {
                1 => Invoked(text.Split(separators)),
                2 when ArgumentParser.TryConvertToInt(args[1], out int count)
                    => Invoked(text.Split(separators, count)),
                2 when ArgumentParser.TryConvertToEnum(args[1], out StringSplitOptions options)
                    => Invoked(text.Split(separators, options)),
                3 when ArgumentParser.TryConvertToInt(args[1], out int count) &&
                            ArgumentParser.TryConvertToEnum(args[2], out StringSplitOptions options)
                    => Invoked(text.Split(separators, count, options)),
                _ => NotHandled,
            };
        }

        private static WellKnownFunctionResult TryInvokeStaticEquals(object?[] args)
            => args switch
            {
                [var left, var right] when left is null or string && right is null or string
                    => Invoked(string.Equals((string?)left, (string?)right)),
                [var left, var right, var comparison]
                    when left is null or string &&
                         right is null or string &&
                         ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(string.Equals((string?)left, (string?)right, convertedComparison)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeStartsWith(string text, object?[] args)
            => args switch
            {
                [string value] => Invoked(text.StartsWith(value, StringComparison.CurrentCulture)),
                [string value, var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.StartsWith(value, convertedComparison)),
                [string value, var ignoreCase, var culture]
                    when TryConvertToBoolean(ignoreCase, out bool convertedIgnoreCase) &&
                         TryGetCultureInfo(culture, out CultureInfo? convertedCulture)
                    => Invoked(text.StartsWith(value, convertedIgnoreCase, convertedCulture)),
#if NET
                [char value] => Invoked(text.StartsWith(value)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeSubstring(string text, object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out int startIndex)
                    => Invoked(text.Substring(startIndex)),

                2 when args.TryGetArg(0, out int startIndex)
                    && args.TryGetArg(1, out int length)
                    => Invoked(text.Substring(startIndex, length)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToLower(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.ToLower()),
                [CultureInfo culture] => Invoked(text.ToLower(culture)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToUpper(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.ToUpper()),
                [CultureInfo culture] => Invoked(text.ToUpper(culture)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToCharArray(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.ToCharArray()),
                [var startIndex, var length]
                    when ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex) &&
                         ArgumentParser.TryConvertToInt(length, out int convertedLength)
                    => Invoked(text.ToCharArray(convertedStartIndex, convertedLength)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToString(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.ToString()),
                [var provider] when provider is null or IFormatProvider => Invoked(text.ToString((IFormatProvider?)provider)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeToLowerInvariant(string text, object?[] args)
            => args.Length == 0
                ? Invoked(text.ToLowerInvariant())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeToUpperInvariant(string text, object?[] args)
            => args.Length == 0
                ? Invoked(text.ToUpperInvariant())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeTrimEnd(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.TrimEnd()),
                [char[] trimChars] => Invoked(text.TrimEnd(trimChars)),
                [string trimChars] => Invoked(text.TrimEnd(trimChars.ToCharArray())),
                [char trimChar] => Invoked(text.TrimEnd([trimChar])),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeTrim(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.Trim()),
                [char[] trimChars] => Invoked(text.Trim(trimChars)),
                [string trimChars] => Invoked(text.Trim(trimChars.ToCharArray())),
                [char trimChar] => Invoked(text.Trim([trimChar])),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeTrimStart(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.TrimStart()),
                [char[] trimChars] => Invoked(text.TrimStart(trimChars)),
                [string trimChars] => Invoked(text.TrimStart(trimChars.ToCharArray())),
                [char trimChar] => Invoked(text.TrimStart([trimChar])),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCopyTo(string text, object?[] args)
        {
            if (args is [var sourceIndex, char[] destination, var destinationIndex, var count] &&
                ArgumentParser.TryConvertToInt(sourceIndex, out int convertedSourceIndex) &&
                ArgumentParser.TryConvertToInt(destinationIndex, out int convertedDestinationIndex) &&
                ArgumentParser.TryConvertToInt(count, out int convertedCount))
            {
                text.CopyTo(convertedSourceIndex, destination, convertedDestinationIndex, convertedCount);
                return Invoked(null);
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeGetHashCode(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.GetHashCode()),
#if NET
                [var comparison] when ArgumentParser.TryConvertToEnum(comparison, out StringComparison convertedComparison)
                    => Invoked(text.GetHashCode(convertedComparison)),
#endif
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeNormalize(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.Normalize()),
                [var normalizationForm] when ArgumentParser.TryConvertToEnum(normalizationForm, out NormalizationForm convertedNormalizationForm)
                    => Invoked(text.Normalize(convertedNormalizationForm)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeIsNormalized(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.IsNormalized()),
                [var normalizationForm] when ArgumentParser.TryConvertToEnum(normalizationForm, out NormalizationForm convertedNormalizationForm)
                    => Invoked(text.IsNormalized(convertedNormalizationForm)),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeReplaceLineEndings(string text, object?[] args)
        {
#if NET
            return args switch
            {
                [] => Invoked(text.ReplaceLineEndings()),
                [string replacementText] => Invoked(text.ReplaceLineEndings(replacementText)),
                _ => NotHandled,
            };
#else
            return NotHandled;
#endif
        }

        private static bool TryGetString(object?[] args, int index, out string? result)
        {
            if ((uint)index < (uint)args.Length && args[index] is null or string)
            {
                result = (string?)args[index];
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryGetCultureInfo(object? value, out CultureInfo? result)
        {
            if (value is null or CultureInfo)
            {
                result = (CultureInfo?)value;
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryGetCharArray(object?[] args, int index, out char[]? result)
        {
            if ((uint)index < (uint)args.Length)
            {
                if (args[index] is char[] characters)
                {
                    result = characters;
                    return true;
                }

                if (args[index] is string text)
                {
                    result = text.ToCharArray();
                    return true;
                }

                if (args[index] is char character)
                {
                    result = [character];
                    return true;
                }
            }

            result = null;
            return false;
        }

        private static object?[] CopyArguments(object?[] args, int startIndex)
        {
            object?[] result = new object?[args.Length - startIndex];
            Array.Copy(args, startIndex, result, 0, result.Length);
            return result;
        }

        private static bool TryCopyStrings(object?[] args, int startIndex, out string?[]? result)
        {
            result = new string?[args.Length - startIndex];
            for (int i = startIndex; i < args.Length; i++)
            {
                if (args[i] is not null and not string)
                {
                    result = null;
                    return false;
                }

                result[i - startIndex] = (string?)args[i];
            }

            return true;
        }

        private static bool TryConvertToBoolean(object? value, out bool result)
        {
            if (value is bool boolean)
            {
                result = boolean;
                return true;
            }

            if (value is string text && bool.TryParse(text, out result))
            {
                return true;
            }

            result = default;
            return false;
        }
    }
}
