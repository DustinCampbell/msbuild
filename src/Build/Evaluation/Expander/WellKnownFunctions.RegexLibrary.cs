// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class RegexLibrary : FunctionLibrary
        {
            public static readonly RegexLibrary Instance = new();

            private RegexLibrary()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Regex.Escape), Regex_Escape);
                builder.Add(nameof(Regex.IsMatch), Regex_IsMatch);
                builder.Add(nameof(Regex.Match), Regex_Match);
                builder.Add(nameof(Regex.Replace), Regex_Replace);
            }

            private static bool Regex_Escape(ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [string str])
                {
                    result = Regex.Escape(str);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Regex_IsMatch(ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [string input, string pattern])
                {
                    result = Regex.IsMatch(input, pattern);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Regex_Match(ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [string input, string pattern])
                {
                    result = Regex.Match(input, pattern);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Regex_Replace(ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [string input, string pattern, string replacement])
                {
                    result = Regex.Replace(input, pattern, replacement);
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
