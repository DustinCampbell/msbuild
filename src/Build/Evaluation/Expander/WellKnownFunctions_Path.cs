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
        internal WellKnownFunctionResult TryInvokeStatic(
            string name,
            ref FunctionArguments arguments,
            out object? result)
        {
            switch (name.Length)
            {
                case 7 when name.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeCombine(ref arguments, out result);

                case 11:
                    if (name.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetFileName(ref arguments, out result);
                    }

                    if (name.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetFullPath(ref arguments, out result);
                    }

                    if (name.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetTempPath(ref arguments, out result);
                    }

                    break;

                case 12 when name.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeIsPathRooted(ref arguments, out result);

                case 16 when name.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeGetDirectoryName(ref arguments, out result);

                case 22 when name.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeDirectorySeparatorChar(ref arguments, out result);

                case 27 when name.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeGetFileNameWithoutExtension(ref arguments, out result);
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeCombine(ref FunctionArguments arguments, out object? result)
        {
            // Combine has specialized implementations for up to four arguments.
            switch (arguments.Count)
            {
                case 1 when arguments.TryGetArg(out string? arg0):
                    result = Path.Combine(arg0);
                    return WellKnownFunctionResult.Handled;

                case 2 when arguments.TryGetArgs(out string? arg0, out string? arg1):
                    result = Path.Combine(arg0, arg1);
                    return WellKnownFunctionResult.Handled;

                case 3 when arguments.TryGetArgs(out string? arg0, out string? arg1, out string? arg2):
                    result = Path.Combine(arg0, arg1, arg2);
                    return WellKnownFunctionResult.Handled;

                case 4 when arguments.TryGetArgs(out string? arg0, out string? arg1, out string? arg2, out string? arg3):
                    result = Path.Combine(arg0, arg1, arg2, arg3);
                    return WellKnownFunctionResult.Handled;

                case > 4 when arguments.TryGetArgs(out string[]? paths):
                    result = Path.Combine(paths);
                    return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeDirectorySeparatorChar(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = Path.DirectorySeparatorChar;
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetFullPath(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? path))
            {
                result = !string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, path))
                    : Path.GetFullPath(path);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeIsPathRooted(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? path))
            {
                result = Path.IsPathRooted(path);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetTempPath(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.Count == 0)
            {
                result = Path.GetTempPath();
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetFileName(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? path))
            {
                result = Path.GetFileName(path);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetDirectoryName(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? path))
            {
                result = Path.GetDirectoryName(path);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }

        private static WellKnownFunctionResult TryInvokeGetFileNameWithoutExtension(
            ref FunctionArguments arguments,
            out object? result)
        {
            if (arguments.TryGetArg(out string? path))
            {
                result = Path.GetFileNameWithoutExtension(path);
                return WellKnownFunctionResult.Handled;
            }

            return NotRecognized(out result);
        }
    }
}
