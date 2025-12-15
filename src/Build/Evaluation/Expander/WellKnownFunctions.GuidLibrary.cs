// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class GuidLibrary : BaseMemberLibrary, IStaticMethodLibrary, ICustomToStringProvider<Guid>
    {
        public static readonly GuidLibrary Instance = new();

        private enum StaticId
        {
            NewGuid
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private GuidLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFindMatch(name, StaticId.NewGuid)
                ? Guid_NewGuid(args)
                : Result.None;

        public Result TryExecuteToString(Guid g, ReadOnlySpan<object?> args)
            => args is [string format]
                ? Result.From(g.ToString(format))
                : Result.None;

        private static Result Guid_NewGuid(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(Guid.NewGuid())
                : Result.None;
    }
}
