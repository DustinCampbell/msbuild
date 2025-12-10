// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class CharLibrary : FunctionLibrary
        {
            public static readonly CharLibrary Instance = new();

            private CharLibrary()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(char.IsDigit), Char_IsDigit);
            }

            private static bool Char_IsDigit(ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArg(args, out char c))
                {
                    result = char.IsDigit(c);
                    return true;
                }

                if (ParseArgs.TryGetArgs(args, out string? str, out int index))
                {
                    result = char.IsDigit(str, index);
                    return true;
                }

                result = false;
                return false;
            }
        }
    }
}
