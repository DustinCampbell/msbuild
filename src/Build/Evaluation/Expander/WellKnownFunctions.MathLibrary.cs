// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class MathLibrary : FunctionLibrary
        {
            private static readonly Func<double, double, double> s_max = Math.Max;
            private static readonly Func<double, double, double> s_min = Math.Min;

            public static readonly MathLibrary Instance = new();

            private MathLibrary()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Math.Max), Math_Max);
                builder.Add(nameof(Math.Min), Math_Min);
            }

            private static bool Math_Max(ReadOnlySpan<object?> args, out object? result)
                => TryExecuteArithmeticFunction(args, s_max, out result);

            private static bool Math_Min(ReadOnlySpan<object?> args, out object? result)
                => TryExecuteArithmeticFunction(args, s_min, out result);
        }
    }
}
