// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class DirectoryLibrary : FunctionLibrary
        {
            public static readonly DirectoryLibrary Instance = new();

            private DirectoryLibrary()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Directory.GetParent), Directory_GetParent);
            }

            private bool Directory_GetParent(ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [string path])
                {
                    result = Directory.GetParent(path);
                    return true;
                }

                result = false;
                return false;
            }
        }
    }
}
