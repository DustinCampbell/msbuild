// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class DirectoryHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                4 when name.Equals(nameof(Directory.Move), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMove(ref arguments),
                6 when name.Equals(nameof(Directory.Delete), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDelete(ref arguments),
                6 when name.Equals(nameof(Directory.Exists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeExists(ref arguments),
                8 when name.Equals(nameof(Directory.GetFiles), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetFiles(ref arguments),
                9 when name.Equals(nameof(Directory.GetParent), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetParent(ref arguments),
                14 when name.Equals(nameof(Directory.GetDirectories), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetDirectories(ref arguments),
                15 when name.Equals(nameof(Directory.CreateDirectory), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCreateDirectory(ref arguments),
                16 when name.Equals(nameof(Directory.GetLastWriteTime), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLastWriteTime(ref arguments),
                17 when name.Equals(nameof(Directory.GetLastAccessTime), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLastAccessTime(ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeCreateDirectory(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.CreateDirectory(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeDelete(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                Directory.Delete(path);
                return Handled(null);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeExists(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.Exists(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetDirectories(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.GetDirectories(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetFiles(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.GetFiles(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetLastAccessTime(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.GetLastAccessTime(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetLastWriteTime(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.GetLastWriteTime(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetParent(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(Directory.GetParent(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeMove(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? sourceDirName) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? destDirName))
            {
                Directory.Move(sourceDirName, destDirName);
                return Handled(null);
            }

            return NotRecognized;
        }
    }
}
