// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class CharLibrary : BaseMemberLibrary, IStaticMethodLibrary
    {
        public static readonly CharLibrary Instance = new();

        private enum StaticId
        {
            IsDigit
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private CharLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFindMatch(name, StaticId.IsDigit)
                ? Char_IsDigit(args)
                : Result.None;

        private static Result Char_IsDigit(ReadOnlySpan<object?> args)
            => args switch
            {
                [var arg0] when TryConvertToChar(arg0, out char c)
                    => Result.From(char.IsDigit(c)),
                [string s, var arg1] when TryConvertToInt(arg1, out int index)
                    => Result.From(char.IsDigit(s, index)),

                _ => Result.None,
            };
    }
}
