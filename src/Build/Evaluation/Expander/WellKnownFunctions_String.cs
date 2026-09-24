// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
            => args.Length switch
            {
                0 => Invoked(string.Empty),
                1 when args.TryGetArg(0, out string? arg0) => Invoked(arg0),

                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
            => methodName.Length switch
            {
                4 when methodName.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCopy(args),
                6 when methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeStaticEquals(args),
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
                4 when methodName.Equals(nameof(string.Trim), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeTrim(text, args),
                6 when methodName.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeLength(text, args),
                6 when methodName.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(text, args),
                6 when methodName.Equals(nameof(string.Insert), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeInsert(text, args),
                7 when methodName.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReplace(text, args),
                7 when methodName.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToLower(text, args),
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
                11 when methodName.Equals(nameof(string.ToCharArray), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToCharArray(text, args),
                8 when methodName.Equals(nameof(string.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(text, args),
                16 when methodName.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToUpperInvariant(text, args),
                16 when methodName.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToLowerInvariant(text, args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCompareTo(string text, object?[] args)
            => args is [string value]
                ? Invoked(string.Compare(text, value, StringComparison.CurrentCulture))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeContains(string text, object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(text.Contains(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeCopy(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(arg0)
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeEndsWith(string text, object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(text.EndsWith(arg0, StringComparison.CurrentCulture)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out StringComparison arg1)
                    => Invoked(text.EndsWith(arg0, arg1)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeEquals(string text, object?[] args)
            => args switch
            {
                [string value] => Invoked(text.Equals(value)),
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
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(text.AsSpan().IndexOfAny(arg0.AsSpan()))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsNullOrEmpty(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(string.IsNullOrEmpty(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsNullOrWhiteSpace(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(string.IsNullOrWhiteSpace(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetTypeCode(string text, object?[] args)
            => args is [] ? Invoked(text.GetTypeCode()) : NotHandled;

        private static WellKnownFunctionResult TryInvokeInsert(string text, object?[] args)
            => args is [var startIndex, string value] && ArgumentParser.TryConvertToInt(startIndex, out int convertedStartIndex)
                ? Invoked(text.Insert(convertedStartIndex, value))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeLastIndexOf(string text, object?[] args)
            => args.Length switch
            {
                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(text.LastIndexOf(arg0, StringComparison.CurrentCulture)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out int startIndex)
                    => Invoked(text.LastIndexOf(arg0, startIndex, StringComparison.CurrentCulture)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out StringComparison arg1)
                    => Invoked(text.LastIndexOf(arg0, arg1)),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeLastIndexOfAny(string text, object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(text.AsSpan().LastIndexOfAny(arg0.AsSpan()))
                : NotHandled;

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
            => args.Length == 2
            && args.TryGetArg(0, out string? arg0)
            && args.TryGetArg(1, out string? arg1)
                ? Invoked(text.Replace(arg0, arg1))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeSplit(string text, object?[] args)
            => args switch
            {
                [char[] separators] => Invoked(text.Split(separators)),
                [var separator] when ArgumentParser.TryConvertToChar(separator, out char convertedSeparator)
                    => Invoked(text.Split(convertedSeparator)),
                _ => NotHandled,
            };

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
             => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                 ? Invoked(text.StartsWith(arg0, StringComparison.CurrentCulture))
                 : NotHandled;

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
            => args.Length == 0
                ? Invoked(text.ToLower())
                : NotHandled;

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
            => args is [] ? Invoked(text.ToString()) : NotHandled;

        private static WellKnownFunctionResult TryInvokeToLowerInvariant(string text, object?[] args)
            => args.Length == 0
                ? Invoked(text.ToLowerInvariant())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeToUpperInvariant(string text, object?[] args)
            => args.Length == 0
                ? Invoked(text.ToUpperInvariant())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeTrimEnd(string text, object?[] args)
            => args.Length == 1
            && args.TryGetArg(0, out string? trimChars)
            && trimChars.Length > 0
                ? Invoked(text.TrimEnd(trimChars.ToCharArray()))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeTrim(string text, object?[] args)
            => args switch
            {
                [] => Invoked(text.Trim()),
                [char[] trimChars] => Invoked(text.Trim(trimChars)),
                [string trimChars] when trimChars.Length > 0 => Invoked(text.Trim(trimChars.ToCharArray())),
                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeTrimStart(string text, object?[] args)
            => args.Length == 1
            && args.TryGetArg(0, out string? trimChars)
            && trimChars.Length > 0
                ? Invoked(text.TrimStart(trimChars.ToCharArray()))
                : NotHandled;
    }
}
