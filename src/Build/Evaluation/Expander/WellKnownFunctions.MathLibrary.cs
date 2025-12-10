// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class MathLibrary : FunctionLibrary
        {
            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Math.Max), Math_Max);
                builder.Add(nameof(Math.Min), Math_Min);
            }

            private static bool Math_Max(ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArgs(args, out double arg0, out double arg1))
                {
                    result = Math.Max(arg0, arg1);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Math_Min(ReadOnlySpan<object?> args, out object? result)
            {
                if (ParseArgs.TryGetArgs(args, out double arg0, out double arg1))
                {
                    result = Math.Min(arg0, arg1);
                    return true;
                }

                result = null;
                return false;
            }
        }
    }
}
