// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownMembers
{
    private sealed class FileHandler
    {
        internal WellKnownMemberResult TryInvokeStatic(StringSegment name, ref FunctionArguments arguments)
            => name.Length switch
            {
                4 when name.Equals(nameof(File.Copy), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCopy(ref arguments),
                4 when name.Equals(nameof(File.Move), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeMove(ref arguments),
                6 when name.Equals(nameof(File.Delete), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeDelete(ref arguments),
                6 when name.Equals(nameof(File.Exists), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeExists(ref arguments),
                11 when name.Equals(nameof(File.ReadAllText), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReadAllText(ref arguments),
                12 when name.Equals(nameof(File.ReadAllBytes), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReadAllBytes(ref arguments),
                12 when name.Equals(nameof(File.ReadAllLines), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeReadAllLines(ref arguments),
                12 when name.Equals(nameof(File.WriteAllText), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeWriteAllText(ref arguments),
                13 when name.Equals(nameof(File.AppendAllText), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeAppendAllText(ref arguments),
                13 when name.Equals(nameof(File.GetAttributes), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetAttributes(ref arguments),
                15 when name.Equals(nameof(File.GetCreationTime), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetCreationTime(ref arguments),
                16 when name.Equals(nameof(File.GetLastWriteTime), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLastWriteTime(ref arguments),
                17 when name.Equals(nameof(File.GetLastAccessTime), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLastAccessTime(ref arguments),
                18 when name.Equals(nameof(File.GetCreationTimeUtc), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetCreationTimeUtc(ref arguments),
                19 when name.Equals(nameof(File.GetLastWriteTimeUtc), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLastWriteTimeUtc(ref arguments),
                20 when name.Equals(nameof(File.GetLastAccessTimeUtc), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeGetLastAccessTimeUtc(ref arguments),

                _ => NotRecognized,
            };

        private static WellKnownMemberResult TryInvokeAppendAllText(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? contents))
            {
                File.AppendAllText(path, contents);
                return Handled(null);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeCopy(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? sourceFileName) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? destFileName))
            {
                File.Copy(sourceFileName, destFileName);
                return Handled(null);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeDelete(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                File.Delete(path);
                return Handled(null);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeExists(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.Exists(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetAttributes(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetAttributes(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetCreationTime(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetCreationTime(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetCreationTimeUtc(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetCreationTimeUtc(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetLastAccessTime(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetLastAccessTime(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetLastAccessTimeUtc(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetLastAccessTimeUtc(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetLastWriteTime(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetLastWriteTime(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeGetLastWriteTimeUtc(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.GetLastWriteTimeUtc(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeMove(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? sourceFileName) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? destFileName))
            {
                File.Move(sourceFileName, destFileName);
                return Handled(null);
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeReadAllBytes(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.ReadAllBytes(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeReadAllLines(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.ReadAllLines(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeReadAllText(ref FunctionArguments arguments)
        {
            if (arguments.Count == 1 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path))
            {
                return Handled(File.ReadAllText(path));
            }

            return NotRecognized;
        }

        private static WellKnownMemberResult TryInvokeWriteAllText(ref FunctionArguments arguments)
        {
            if (arguments.Count == 2 &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(0), out string? path) &&
                FunctionArgumentCoercion.TryCoerce(arguments.GetValue(1), out string? contents))
            {
                File.WriteAllText(path, contents);
                return Handled(null);
            }

            return NotRecognized;
        }
    }
}
