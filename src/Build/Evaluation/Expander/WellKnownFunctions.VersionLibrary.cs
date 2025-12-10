// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class VersionLibrary : FunctionLibrary
        {
            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Version.Parse), Version_Parse);
                builder.Add<Version>(nameof(Version.ToString), Version_ToString);
            }

            private static bool Version_Parse(ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    result = Version.Parse(arg0);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Version_ToString(Version v, ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArg(args, out int arg0))
                {
                    result = v.ToString(arg0);
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
