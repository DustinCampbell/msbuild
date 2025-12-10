// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class PathLibrary : FunctionLibrary
    {
        public static readonly PathLibrary Instance = new();

        private PathLibrary()
        {
        }

        protected override void Initialize(ref Builder builder)
        {
            builder.Add(nameof(Path.Combine), Path_Combine);
            builder.Add(nameof(Path.DirectorySeparatorChar), Path_DirectorySeparatorChar);
            builder.Add(nameof(Path.GetFullPath), Path_GetFullPath);
            builder.Add(nameof(Path.IsPathRooted), Path_IsPathRooted);
            builder.Add(nameof(Path.GetTempPath), Path_GetTempPath);
            builder.Add(nameof(Path.GetFileName), Path_GetFileName);
            builder.Add(nameof(Path.GetDirectoryName), Path_GetDirectoryName);
            builder.Add(nameof(Path.GetFileNameWithoutExtension), Path_GetFileNameWithoutExtension);
        }

        private static bool Path_Combine(ReadOnlySpan<object?> args, out object? result)
        {
            // Combine has fast implementations for up to 4 parameters: https://github.com/dotnet/corefx/blob/2c55db90d622fa6279184e6243f0470a3755d13c/src/Common/src/CoreLib/System/IO/Path.cs#L293-L317
            switch (args)
            {
                case []:
                    result = Path.Combine([]);
                    return true;

                case [string path1, string path2]:
                    result = Path.Combine(path1, path2);
                    return true;

                case [string path1, string path2, string path3]:
                    result = Path.Combine(path1, path2, path3);
                    return true;

                case [string path1, string path2, string path3, string path4]:
                    result = Path.Combine(path1, path2, path3, path4);
                    return true;

                default:
                    if (TryConvertToStringArray(args, out string[]? paths))
                    {
                        result = Path.Combine(paths);
                        return true;
                    }

                    result = null;
                    return false;
            }
        }

        private static bool Path_DirectorySeparatorChar(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = Path.DirectorySeparatorChar;
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetFullPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [string path])
            {
                result = Path.GetFullPath(path);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_IsPathRooted(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && arg0 is string or null)
            {
                var path = (string?)arg0;
                result = Path.IsPathRooted(path);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetTempPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [])
            {
                result = Path.GetTempPath();
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetFileName(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && arg0 is string or null)
            {
                var path = (string?)arg0;
                result = Path.GetFileName(path);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetDirectoryName(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && arg0 is string or null)
            {
                var path = (string?)arg0;
                result = Path.GetDirectoryName(path);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetFileNameWithoutExtension(ReadOnlySpan<object?> args, out object? result)
        {
            if (args is [var arg0] && arg0 is string or null)
            {
                var path = (string?)arg0;
                result = Path.GetFileNameWithoutExtension(path);
                return true;
            }

            result = false;
            return false;
        }
    }
}
