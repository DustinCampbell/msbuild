// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

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
            builder.Add<string>(nameof(string.TrimStart), String_TrimStart);
            builder.Add<string>(nameof(string.TrimEnd), String_TrimEnd);
            builder.Add<string>("get_Chars", String_Get_Chars);
            builder.Add<string>(nameof(string.Equals), String_Equals);
        }

        private static bool String_IsNullOrWhiteSpace(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = string.IsNullOrWhiteSpace(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_IsNullOrEmpty(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = string.IsNullOrEmpty(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Copy(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
#pragma warning disable CS0618 // Type or member is obsolete
                result = string.Copy(arg0);
#pragma warning restore CS0618 // Type or member is obsolete
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_New(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = string.Empty;
                return true;
            }

            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = arg0;
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_StartsWith(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.StartsWith(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Replace(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
            {
                result = instance.Replace(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Contains(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.Contains(arg0);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_ToUpperInvariant(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = instance.ToUpperInvariant();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_ToLowerInvariant(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = instance.ToLowerInvariant();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_EndsWith(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.EndsWith(arg0);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1))
            {
                result = instance.EndsWith(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_ToLower(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = instance.ToLower();
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_IndexOf(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArgs(args, out string? arg0, out StringComparison arg1))
            {
                result = instance.IndexOf(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_IndexOfAny(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.AsSpan().IndexOfAny(arg0.AsSpan());
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_LastIndexOf(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.LastIndexOf(arg0);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out arg0, out int startIndex))
            {
                result = instance.LastIndexOf(arg0, startIndex);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out arg0, out StringComparison arg1))
            {
                result = instance.LastIndexOf(arg0, arg1);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_LastIndexOfAny(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.AsSpan().LastIndexOfAny(arg0.AsSpan());
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Length(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = instance.Length;
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Substring(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out int startIndex))
            {
                result = instance.Substring(startIndex);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out startIndex, out int length))
            {
                result = instance.Substring(startIndex, length);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Split(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out char separator))
            {
                result = instance.Split(separator);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_PadLeft(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out int totalWidth))
            {
                result = instance.PadLeft(totalWidth);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out totalWidth, out char paddingChar))
            {
                result = instance.PadLeft(totalWidth, paddingChar);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_PadRight(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out int totalWidth))
            {
                result = instance.PadRight(totalWidth);
                return true;
            }

            if (ParseArgs.TryGetArgs(args, out totalWidth, out char paddingChar))
            {
                result = instance.PadRight(totalWidth, paddingChar);
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_TrimStart(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars.Length > 0)
            {
                result = instance.TrimStart(trimChars.ToCharArray());
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_TrimEnd(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? trimChars) && trimChars.Length > 0)
            {
                result = instance.TrimEnd(trimChars.ToCharArray());
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Get_Chars(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out int index))
            {
                result = instance[index];
                return true;
            }

            result = null;
            return false;
        }

        private static bool String_Equals(string instance, ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = instance.Equals(arg0);
                return true;
            }

            result = null;
            return false;
        }
    }
}
