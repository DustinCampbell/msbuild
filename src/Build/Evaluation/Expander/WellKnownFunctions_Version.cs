// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

internal static partial class WellKnownFunctions
{
    private sealed class VersionHandler
    {
        public WellKnownFunctionResult TryInvokeConstructor(object?[] args)
            => args switch
            {
                [] => Invoked(new Version()),
                [string version] => Invoked(new Version(version)),
                [var major, var minor]
                    when ArgumentParser.TryConvertToInt(major, out int convertedMajor) &&
                         ArgumentParser.TryConvertToInt(minor, out int convertedMinor)
                    => Invoked(new Version(convertedMajor, convertedMinor)),
                [var major, var minor, var build]
                    when ArgumentParser.TryConvertToInt(major, out int convertedMajor) &&
                         ArgumentParser.TryConvertToInt(minor, out int convertedMinor) &&
                         ArgumentParser.TryConvertToInt(build, out int convertedBuild)
                    => Invoked(new Version(convertedMajor, convertedMinor, convertedBuild)),
                [var major, var minor, var build, var revision]
                    when ArgumentParser.TryConvertToInt(major, out int convertedMajor) &&
                         ArgumentParser.TryConvertToInt(minor, out int convertedMinor) &&
                         ArgumentParser.TryConvertToInt(build, out int convertedBuild) &&
                         ArgumentParser.TryConvertToInt(revision, out int convertedRevision)
                    => Invoked(new Version(convertedMajor, convertedMinor, convertedBuild, convertedRevision)),
                _ => NotHandled,
            };

        public WellKnownFunctionResult TryInvokeStatic(string methodName, object?[] args)
        {
            if (methodName.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
            {
                return TryInvokeParse(args);
            }

            if (args is [Version left, Version right])
            {
                if (methodName.Equals("op_Equality", StringComparison.OrdinalIgnoreCase)) return Invoked(left == right);
                if (methodName.Equals("op_Inequality", StringComparison.OrdinalIgnoreCase)) return Invoked(left != right);
                if (methodName.Equals("op_LessThan", StringComparison.OrdinalIgnoreCase)) return Invoked(left < right);
                if (methodName.Equals("op_LessThanOrEqual", StringComparison.OrdinalIgnoreCase)) return Invoked(left <= right);
                if (methodName.Equals("op_GreaterThan", StringComparison.OrdinalIgnoreCase)) return Invoked(left > right);
                if (methodName.Equals("op_GreaterThanOrEqual", StringComparison.OrdinalIgnoreCase)) return Invoked(left >= right);
            }

            return NotHandled;
        }

        public WellKnownFunctionResult TryInvokeInstance(string methodName, Version version, object?[] args)
            => methodName.Length switch
            {
                5 when methodName.Equals(nameof(Version.Major), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.Major) : NotHandled,
                5 when methodName.Equals(nameof(Version.Minor), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.Minor) : NotHandled,
                5 when methodName.Equals(nameof(Version.Build), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.Build) : NotHandled,
                8 when methodName.Equals(nameof(Version.Revision), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.Revision) : NotHandled,
                9 when methodName.Equals(nameof(Version.CompareTo), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeCompareTo(version, args),
                6 when methodName.Equals(nameof(Version.Equals), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeEquals(version, args),
                11 when methodName.Equals(nameof(Version.GetHashCode), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.GetHashCode()) : NotHandled,
                13 when methodName.Equals(nameof(Version.MajorRevision), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.MajorRevision) : NotHandled,
                13 when methodName.Equals(nameof(Version.MinorRevision), StringComparison.OrdinalIgnoreCase)
                    => args is [] ? Invoked(version.MinorRevision) : NotHandled,
                8 when methodName.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase)
                    => TryInvokeToString(version, args),

                _ => NotHandled,
            };

        private static WellKnownFunctionResult TryInvokeCompareTo(Version version, object?[] args)
            => args is [Version other]
                ? Invoked(version.CompareTo(other))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeEquals(Version version, object?[] args)
            => args is [var other]
                ? Invoked(version.Equals(other))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeParse(object?[] args)
            => args.Length == 1 && args.TryGetArg(0, out string? arg0)
                ? Invoked(Version.Parse(arg0))
                : NotHandled;

        private static WellKnownFunctionResult TryInvokeToString(Version version, object?[] args)
            => args switch
            {
                [] => Invoked(version.ToString()),
                [var fieldCount] when ArgumentParser.TryConvertToInt(fieldCount, out int convertedFieldCount)
                    => Invoked(version.ToString(convertedFieldCount)),
                _ => NotHandled,
            };
    }
}
