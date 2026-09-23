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
            => methodName.Length switch
            {
                7 when methodName.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCombine(args),
                22 when methodName.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDirectorySeparatorChar(args),
                11 when methodName.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetFullPath(args),
                12 when methodName.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeIsPathRooted(args),
                11 when methodName.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetTempPath(args),
                11 when methodName.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetFileName(args),
                16 when methodName.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetDirectoryName(args),
                27 when methodName.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetFileNameWithoutExtension(args),

                _ => NotHandled,
            };

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

        private static WellKnownFunctionResult TryInvokeDirectorySeparatorChar(object?[] args)
            => args.Length == 0
                ? Invoked(Path.DirectorySeparatorChar)
                : NotHandled;

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
            if (args.Length == 1 && args.TryGetArg(0, out string? arg0))
            {
                string fullPath = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, arg0))
                    : Path.GetFullPath(arg0);

                return Invoked(fullPath);
            }

            return NotHandled;
        }

        private static WellKnownFunctionResult TryInvokeGetTempPath(object?[] args)
            => args.Length == 0
                ? Invoked(Path.GetTempPath())
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeIsPathRooted(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Path.IsPathRooted(arg0))
                : NotHandled;
    }
}
