// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class Int32Library : FunctionLibrary
        {
            protected override void Initialize(ref Builder builder)
            {
                builder.Add<int>(nameof(int.ToString), Int32_ToString);
            }

            private static bool Int32_ToString(int i, ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArg(args, out string? arg0))
                {
                    result = i.ToString(arg0);
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
