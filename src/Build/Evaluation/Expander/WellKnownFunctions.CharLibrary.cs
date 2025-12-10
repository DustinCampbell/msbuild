// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

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
                switch (args)
                {
                    case [var arg0] when TryConvertToChar(arg0, out char c):
                        result = char.IsDigit(c);
                        return true;

                    case [string s, var arg1] when TryConvertToInt(arg1, out int index):
                        result = char.IsDigit(s, index);
                        return true;

                    default:
                        result = false;
                        return false;
                }
            }
        }
    }
}
