// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class MathLibrary : BaseMemberLibrary, IStaticMethodLibrary
    {
        public static readonly MathLibrary Instance = new();

        private enum StaticId
        {
            Max,
            Min
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private static readonly Func<int, int, int> s_max = Math.Max;
        private static readonly Func<int, int, int> s_min = Math.Min;

        private MathLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFind(name, out StaticId id)
                ? id switch
                {
                    StaticId.Max => Math_Max(args),
                    StaticId.Min => Math_Min(args),
                    _ => Result.None,
                }
                : Result.None;

        private static Result Math_Max(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_max);

        private static Result Math_Min(ReadOnlySpan<object?> args)
            => TryExecuteArithmeticFunction(args, s_min);
    }
}
