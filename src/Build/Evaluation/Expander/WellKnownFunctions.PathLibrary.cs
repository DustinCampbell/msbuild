// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using ParseArgs = Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class PathLibrary : FunctionLibrary
    {
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
            switch (args.Length)
            {
                case 0:
                    result = null;
                    return false;

                case 1:
                    {
                        if (ParseArgs.TryGetArg(args, out string? arg0))
                        {
                            result = Path.Combine(arg0);
                            return true;
                        }
                    }

                    break;
                case 2:
                    {
                        if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1))
                        {
                            result = Path.Combine(arg0, arg1);
                            return true;
                        }
                    }

                    break;

                case 3:
                    {
                        if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1, out string? arg2))
                        {
                            result = Path.Combine(arg0, arg1, arg2);
                            return true;
                        }
                    }

                    break;

                case 4:
                    {
                        if (ParseArgs.TryGetArgs(args, out string? arg0, out string? arg1, out string? arg2, out string? arg3))
                        {
                            result = Path.Combine(arg0, arg1, arg2, arg3);
                            return true;
                        }
                    }

                    break;

                default:
                    string[] paths = new string[args.Length];

                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i] is not string stringArg)
                        {
                            result = null;
                            return false;
                        }

                        paths[i] = stringArg;
                    }

                    result = Path.Combine(paths);
                    return true;
            }

            result = null;
            return false;
        }

        private static bool Path_DirectorySeparatorChar(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = Path.DirectorySeparatorChar;
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetFullPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = Path.GetFullPath(arg0);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_IsPathRooted(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = Path.IsPathRooted(arg0);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetTempPath(ReadOnlySpan<object?> args, out object? result)
        {
            if (args.Length == 0)
            {
                result = Path.GetTempPath();
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetFileName(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = Path.GetFileName(arg0);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetDirectoryName(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = Path.GetDirectoryName(arg0);
                return true;
            }

            result = false;
            return false;
        }

        private static bool Path_GetFileNameWithoutExtension(ReadOnlySpan<object?> args, out object? result)
        {
            if (ParseArgs.TryGetArg(args, out string? arg0))
            {
                result = Path.GetFileNameWithoutExtension(arg0);
                return true;
            }

            result = false;
            return false;
        }
    }
}
