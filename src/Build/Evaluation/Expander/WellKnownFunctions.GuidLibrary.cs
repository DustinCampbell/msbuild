// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class GuidLibrary : FunctionLibrary
        {
            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Guid.NewGuid), Guid_NewGuid);
            }

            private static bool Guid_NewGuid(ReadOnlySpan<object?> args, out object? result)
            {
                if (args.Length == 0)
                {
                    result = Guid.NewGuid();
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
