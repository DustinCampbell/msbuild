// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class StringLibrary : FunctionLibrary
    {
        public static readonly StringLibrary Instance = new();

        private StringLibrary()
        {
        }

        protected override void Initialize(ref Builder builder)
        {
            builder.Add(nameof(string.IsNullOrWhiteSpace), String_IsNullOrWhiteSpace);
            builder.Add(nameof(string.IsNullOrEmpty), String_IsNullOrEmpty);
            builder.Add(nameof(string.Copy), String_Copy);
            builder.Add("new", String_New);

            builder.Add<string>(nameof(string.StartsWith), String_StartsWith);
            builder.Add<string>(nameof(string.Replace), String_Replace);
            builder.Add<string>(nameof(string.Contains), String_Contains);
            builder.Add<string>(nameof(string.ToUpperInvariant), String_ToUpperInvariant);
            builder.Add<string>(nameof(string.ToLowerInvariant), String_ToLowerInvariant);
            builder.Add<string>(nameof(string.EndsWith), String_EndsWith);
            builder.Add<string>(nameof(string.ToLower), String_ToLower);
            builder.Add<string>(nameof(string.IndexOf), String_IndexOf);
            builder.Add<string>(nameof(string.IndexOfAny), String_IndexOfAny);
            builder.Add<string>(nameof(string.LastIndexOf), String_LastIndexOf);
            builder.Add<string>(nameof(string.LastIndexOfAny), String_LastIndexOfAny);
            builder.Add<string>(nameof(string.Length), String_Length);
            builder.Add<string>(nameof(string.Split), String_Split);
            builder.Add<string>(nameof(string.Substring), String_Substring);
            builder.Add<string>(nameof(string.PadLeft), String_PadLeft);
            builder.Add<string>(nameof(string.PadRight), String_PadRight);
            builder.Add<string>(nameof(string.Trim), String_Trim);
            builder.Add<string>(nameof(string.TrimStart), String_TrimStart);
            builder.Add<string>(nameof(string.TrimEnd), String_TrimEnd);
            builder.Add<string>("get_Chars", String_Get_Chars);
            builder.Add<string>(nameof(string.Equals), String_Equals);
        }

        private static bool String_IsNullOrWhiteSpace(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string or null])
            {
                var value = (string?)args[0];
                result = string.IsNullOrWhiteSpace(value);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_IsNullOrEmpty(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string or null])
            {
                var value = (string?)args[0];
                result = string.IsNullOrEmpty(value);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Copy(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string str])
            {
#pragma warning disable CS0618 // Type or member is obsolete
                result = string.Copy(str);
#pragma warning restore CS0618 // Type or member is obsolete
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_New(ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case []:
                    result = string.Empty;
                    return true;

                case [string value]:
                    result = value;
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_StartsWith(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string value])
            {
                result = instance.StartsWith(value);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Replace(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string oldValue, string or null])
            {
                var newValue = (string?)args[1];
                result = instance.Replace(oldValue, newValue);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Contains(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string value])
            {
                result = instance.Contains(value);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_ToUpperInvariant(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = instance.ToUpperInvariant();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_ToLowerInvariant(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = instance.ToLowerInvariant();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_EndsWith(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [string value]:
                    result = instance.EndsWith(value);
                    return true;

                case [string value, string arg1] when TryConvertToEnum<StringComparison>(arg1, out var comparisonType):
                    result = instance.EndsWith(value, comparisonType);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_ToLower(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = instance.ToLower();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_IndexOf(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [string value]:
                    result = instance.IndexOf(value);
                    return true;

                case [string value, var arg1] when TryConvertToInt(arg1, out var startIndex):
                    result = instance.IndexOf(value, startIndex);
                    return true;

                case [string value, string arg1] when TryConvertToEnum<StringComparison>(arg1, out var comparisonType):
                    result = instance.IndexOf(value, comparisonType);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_IndexOfAny(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string values])
            {
                result = instance.AsSpan().IndexOfAny(values);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_LastIndexOf(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [string value]:
                    result = instance.LastIndexOf(value);
                    return true;

                case [string value, var arg1] when TryConvertToInt(arg1, out var startIndex):
                    result = instance.LastIndexOf(value, startIndex);
                    return true;

                case [string value, string arg1] when TryConvertToEnum<StringComparison>(arg1, out var comparisonType):
                    result = instance.LastIndexOf(value, comparisonType);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_LastIndexOfAny(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string values])
            {
                result = instance.AsSpan().LastIndexOfAny(values);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Length(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = instance.Length;
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Substring(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [var arg0] when TryConvertToInt(arg0, out var startIndex):
                    result = instance.Substring(startIndex);
                    return true;

                case [var arg0, var arg1] when TryConvertToInt(arg0, out var startIndex) && TryConvertToInt(arg1, out var length):
                    result = instance.Substring(startIndex, length);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_Split(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [var value] when TryConvertToChar(value, out var separator):
                    result = instance.Split(separator);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_PadLeft(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [var arg0] when TryConvertToInt(arg0, out var totalWidth):
                    result = instance.PadLeft(totalWidth);
                    return true;

                case [var arg0, var arg1] when TryConvertToInt(arg0, out var totalWidth) && TryConvertToChar(arg1, out var paddingChar):
                    result = instance.PadLeft(totalWidth, paddingChar);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private static bool String_PadRight(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            switch (args)
            {
                case [var arg0] when TryConvertToInt(arg0, out var totalWidth):
                    result = instance.PadRight(totalWidth);
                    return true;

                case [var arg0, var arg1] when TryConvertToInt(arg0, out var totalWidth) && TryConvertToChar(arg1, out var paddingChar):
                    result = instance.PadRight(totalWidth, paddingChar);
                    return true;

                default:
                    result = null;
                    return false;
            }
        }

        private bool String_Trim(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = instance.Trim();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_TrimStart(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string trimChars] && trimChars.Length > 0)
            {
                result = trimChars.Length == 1
                    ? instance.TrimStart(trimChars[0])
                    : instance.TrimStart(trimChars.ToCharArray());

                return true;
            }

            result = null;
            return false;
        }

        private static bool String_TrimEnd(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string trimChars] && trimChars.Length > 0)
            {
                result = trimChars.Length == 1
                    ? instance.TrimEnd(trimChars[0])
                    : instance.TrimEnd(trimChars.ToCharArray());

                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Get_Chars(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && TryConvertToInt(arg0, out var index))
            {
                result = instance[index];
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Equals(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string or null])
            {
                var value = (string?)args[0];
                result = instance.Equals(value);
                return true;
            }

            result = null;
            return false;
        }
    }
}
