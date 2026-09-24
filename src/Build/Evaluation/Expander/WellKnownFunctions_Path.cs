// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class PathHandler
    {
        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (args is [])
            {
                if (methodName.Equals(nameof(Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.AltDirectorySeparatorChar);
                if (methodName.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.DirectorySeparatorChar);
                if (methodName.Equals(nameof(Path.GetInvalidFileNameChars), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.GetInvalidFileNameChars());
                if (methodName.Equals(nameof(Path.GetInvalidPathChars), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.GetInvalidPathChars());
                if (methodName.Equals(nameof(Path.GetRandomFileName), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.GetRandomFileName());
                if (methodName.Equals(nameof(Path.GetTempFileName), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.GetTempFileName());
                if (methodName.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.GetTempPath());
                if (methodName.Equals(nameof(Path.PathSeparator), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.PathSeparator);
                if (methodName.Equals(nameof(Path.VolumeSeparatorChar), StringComparison.OrdinalIgnoreCase)) return Invoked(Path.VolumeSeparatorChar);
            }

            if (methodName.Equals(nameof(Path.ChangeExtension), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path, string extension] ? Invoked(Path.ChangeExtension(path, extension)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase)) return TryInvokeCombine(args);
            if (methodName.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase)) return TryInvokeGetDirectoryName(args);
            if (methodName.Equals(nameof(Path.GetExtension), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Path.GetExtension(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase)) return TryInvokeGetFileName(args);
            if (methodName.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase)) return TryInvokeGetFileNameWithoutExtension(args);
            if (methodName.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase)) return TryInvokeGetFullPath(args);
            if (methodName.Equals(nameof(Path.GetPathRoot), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Path.GetPathRoot(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.HasExtension), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Path.HasExtension(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase)) return TryInvokeIsPathRooted(args);

#if NET
            if (methodName.Equals(nameof(Path.EndsInDirectorySeparator), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Path.EndsInDirectorySeparator(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.GetRelativePath), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string relativeTo, string path] ? Invoked(Path.GetRelativePath(relativeTo, path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.IsPathFullyQualified), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Path.IsPathFullyQualified(path)) : NotHandled;
            }

            if (methodName.Equals(nameof(Path.Join), StringComparison.OrdinalIgnoreCase))
            {
                return args switch
                {
                    [string path1, string path2] => Invoked(Path.Join(path1, path2)),
                    [string path1, string path2, string path3] => Invoked(Path.Join(path1, path2, path3)),
                    [string path1, string path2, string path3, string path4] => Invoked(Path.Join(path1, path2, path3, path4)),
                    _ => ArgumentParser.TryConvertToStrings(args, out string[]? paths) ? Invoked(Path.Join(paths)) : NotHandled,
                };
            }

            if (methodName.Equals(nameof(Path.TrimEndingDirectorySeparator), StringComparison.OrdinalIgnoreCase))
            {
                return args is [string path] ? Invoked(Path.TrimEndingDirectorySeparator(path)) : NotHandled;
            }
#endif

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeCombine(object?[] args)
            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
            => args.Length switch
            {
                0 => NotHandled,

                1 when args.TryGetArg(0, out string? arg0)
                    => Invoked(Path.Combine(arg0)),

                2 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out string? arg1)
                    => Invoked(Path.Combine(arg0, arg1)),

                3 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out string? arg1)
                    && args.TryGetArg(2, out string? arg2)
                    => Invoked(Path.Combine(arg0, arg1, arg2)),

                4 when args.TryGetArg(0, out string? arg0)
                    && args.TryGetArg(1, out string? arg1)
                    && args.TryGetArg(2, out string? arg2)
                    && args.TryGetArg(3, out string? arg3)
                    => Invoked(Path.Combine(arg0, arg1, arg2, arg3)),

                _ => ArgumentParser.TryConvertToStrings(args, out string[]? stringArgs)
                    ? Invoked(Path.Combine(stringArgs))
                    : NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeGetDirectoryName(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Path.GetDirectoryName(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetFileName(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Path.GetFileName(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetFileNameWithoutExtension(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Path.GetFileNameWithoutExtension(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeGetFullPath(object?[] args)
        {
#if NET
            if (args is [string path, string basePath])
            {
                return Invoked(Path.GetFullPath(path, basePath));
            }
#endif

            if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
            {
                string fullPath = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                    : Path.GetFullPath(arg0);

                return Invoked(fullPath);
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeIsPathRooted(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Path.IsPathRooted(arg0))
                : NotHandled;
    }
}
