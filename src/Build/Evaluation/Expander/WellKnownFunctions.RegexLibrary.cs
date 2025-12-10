// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

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
                builder.Add(nameof(Regex.Replace), Regex_Replace);
            }

            private static bool Regex_Replace(ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArgs(args, out string? arg1, out string? arg2, out string? arg3))
                {
                    result = Regex.Replace(arg1, arg2, arg3);
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
