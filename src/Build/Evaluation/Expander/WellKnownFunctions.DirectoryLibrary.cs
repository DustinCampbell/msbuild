// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class DirectoryLibrary : BaseMemberLibrary, IStaticMethodLibrary
    {
        public static readonly DirectoryLibrary Instance = new();

        private enum StaticId
        {
            GetParent
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private DirectoryLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFindMatch(name, StaticId.GetParent)
                ? Directory_GetParent(args)
                : Result.None;

        private static Result Directory_GetParent(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(Directory.GetParent(path))
                : Result.None;
    }
}
