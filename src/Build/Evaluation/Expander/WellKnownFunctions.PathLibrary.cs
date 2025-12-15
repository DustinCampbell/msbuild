// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class PathLibrary : BaseMemberLibrary, IStaticMethodLibrary
    {
        public static readonly PathLibrary Instance = new();

        private enum StaticId
        {
            Combine,
            DirectorySeparatorChar,
            GetFullPath,
            IsPathRooted,
            GetTempPath,
            GetFileName,
            GetDirectoryName,
            GetFileNameWithoutExtension
        }

        private static readonly FunctionIdLookup<StaticId> s_staticIds = FunctionIdLookup<StaticId>.Instance;

        private PathLibrary()
        {
        }

        public Result TryExecute(string name, ReadOnlySpan<object?> args)
            => s_staticIds.TryFind(name, out StaticId id)
                ? id switch
                {
                    StaticId.Combine => Path_Combine(args),
                    StaticId.DirectorySeparatorChar => Path_DirectorySeparatorChar(args),
                    StaticId.GetFullPath => Path_GetFullPath(args),
                    StaticId.IsPathRooted => Path_IsPathRooted(args),
                    StaticId.GetTempPath => Path_GetTempPath(args),
                    StaticId.GetFileName => Path_GetFileName(args),
                    StaticId.GetDirectoryName => Path_GetDirectoryName(args),
                    StaticId.GetFileNameWithoutExtension => Path_GetFileNameWithoutExtension(args),
                    _ => Result.None,
                }
                : Result.None;

        private static Result Path_Combine(ReadOnlySpan<object?> args)
            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
            => args switch
            {
                [] => Result.From(Path.Combine([])),
                [string path1, string path2] => Result.From(Path.Combine(path1, path2)),
                [string path1, string path2, string path3] => Result.From(Path.Combine(path1, path2, path3)),
                [string path1, string path2, string path3, string path4] => Result.From(Path.Combine(path1, path2, path3, path4)),
                _ => TryConvertToStringArray(args, out string[]? paths)
                    ? Result.From(Path.Combine(paths))
                    : Result.None,
            };

        private static Result Path_DirectorySeparatorChar(ReadOnlySpan<object?> args)
            => args is []
            ? Result.From(Path.DirectorySeparatorChar)
                : Result.None;

        private static Result Path_GetFullPath(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(Path.GetFullPath(path))
                : Result.None;

        private static Result Path_IsPathRooted(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(Path.IsPathRooted(path))
                : Result.None;

        private static Result Path_GetTempPath(ReadOnlySpan<object?> args)
            => args is []
                ? Result.From(Path.GetTempPath())
                : Result.None;

        private static Result Path_GetFileName(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(Path.GetFileName(path))
                : Result.None;

        private static Result Path_GetDirectoryName(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(Path.GetDirectoryName(path))
                : Result.None;

        private static Result Path_GetFileNameWithoutExtension(ReadOnlySpan<object?> args)
            => args is [string path]
                ? Result.From(Path.GetFileNameWithoutExtension(path))
                : Result.None;
    }
}
