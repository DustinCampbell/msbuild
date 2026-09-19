// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class PathHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(
            StringSegment name,
            ref FunctionArguments arguments)
        {
            switch (name.Length)
            {
                case 7 when name.Equals(nameof(Path.Combine), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeCombine(ref arguments);

                case 11:
                    if (name.Equals(nameof(Path.GetFileName), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetFileName(ref arguments);
                    }

                    if (name.Equals(nameof(Path.GetFullPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetFullPath(ref arguments);
                    }

                    if (name.Equals(nameof(Path.GetTempPath), StringComparison.OrdinalIgnoreCase))
                    {
                        return TryInvokeGetTempPath(ref arguments);
                    }

                    break;

                case 12 when name.Equals(nameof(Path.IsPathRooted), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeIsPathRooted(ref arguments);

                case 16 when name.Equals(nameof(Path.GetDirectoryName), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeGetDirectoryName(ref arguments);

                case 27 when name.Equals(nameof(Path.GetFileNameWithoutExtension), StringComparison.OrdinalIgnoreCase):
                    return TryInvokeGetFileNameWithoutExtension(ref arguments);
            }

            return NotRecognized;
        }

        internal WellKnownMemberResult TryGetStatic(StringSegment name)
            => name.Length == 22 && name.Equals(nameof(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                ? Handled(Path.DirectorySeparatorChar)
                : NotRecognized;

        private static WellKnownMemberResult TryInvokeCombine(ref FunctionArguments arguments)
        {
            // Combine has specialized implementations for up to four arguments.
            switch (arguments.Count)
            {
                case 1 when FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? arg0):
                    return Handled(Path.Combine(arg0));

                case 2 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? arg0) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? arg1):
                    return Handled(Path.Combine(arg0, arg1));

                case 3 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? arg0) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? arg1) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out string? arg2):
                    return Handled(Path.Combine(arg0, arg1, arg2));

                case 4 when
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? arg0) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? arg1) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(2), out string? arg2) &&
                    FunctionArgumentCoercion.TryCoerce(arguments.GetValue(3), out string? arg3):
                    return Handled(Path.Combine(arg0, arg1, arg2, arg3));

                case > 4:
                    string[] paths = new string[arguments.Count];
                    for (int i = 0; i < paths.Length; i++)
                    {
                        if (!FunctionArgumentCoercion.TryCoerce(arguments.GetValue(i), out string? path))
                        {
                            return NotRecognized;
                        }

                        paths[i] = path;
                    }

                    return Handled(Path.Combine(paths));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetFullPath(
            ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(!string.IsNullOrEmpty(FileUtilities.CurrentThreadWorkingDirectory)
                    ? Path.GetFullPath(Path.Combine(FileUtilities.CurrentThreadWorkingDirectory, path))
                    : Path.GetFullPath(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeIsPathRooted(
            ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Path.IsPathRooted(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetTempPath(
            ref FunctionArguments arguments)
        {
            if (arguments.Count == 0)
            {
                return Handled(Path.GetTempPath());
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetFileName(
            ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Path.GetFileName(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetDirectoryName(
            ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Path.GetDirectoryName(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetFileNameWithoutExtension(
            ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Path.GetFileNameWithoutExtension(path));
            }

            return NotRecognized;
        }
    }
}
