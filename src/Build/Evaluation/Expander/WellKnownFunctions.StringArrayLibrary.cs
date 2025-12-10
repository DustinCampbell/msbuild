// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class StringArrayLibrary : FunctionLibrary
        {
            public static readonly StringArrayLibrary Instance = new();

            private StringArrayLibrary()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add<string[]>("GetValue", StringArray_GetValue);
            }

            private static bool StringArray_GetValue(string[] instance, ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArg(args, out int index))
                {
                    result = instance[index];
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
