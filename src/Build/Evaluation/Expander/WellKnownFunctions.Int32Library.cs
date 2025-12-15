// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class Int32Library : BaseMemberLibrary, IInstanceMethodLibrary<int>, ICustomToStringProvider<int>
    {
        public static readonly Int32Library Instance = new();

        private Int32Library()
        {
        }

        public Result TryExecute(int instance, string name, ReadOnlySpan<object?> args)
            => Result.None;

        public Result TryExecuteToString(int i, ReadOnlySpan<object?> args)
            => args is [string format]
                ? Result.From(i.ToString(format))
                : Result.None;
    }
}
