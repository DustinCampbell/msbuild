// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
    private enum VersionFunction : byte
    {
        None,
        Build,
        Major,
        MajorRevision,
        Minor,
        MinorRevision,
        Parse,
        Revision,
        ToString,
    }

    internal static bool TryExecuteStaticVersionFunction(
        StringSegment methodName,
        ref FunctionArguments args,
        out object? result)
    {
        if (GetVersionFunction(methodName) == VersionFunction.Parse && args.TryGetArg(out Version? version))
        {
            result = version;
            return true;
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteVersionFunction(
        StringSegment methodName,
        Version version,
        ref FunctionArguments args,
        out object? result)
    {
        switch (GetVersionFunction(methodName))
        {
            case VersionFunction.Major when args.Length == 0:
                result = version.Major;
                return true;

            case VersionFunction.Minor when args.Length == 0:
                result = version.Minor;
                return true;

            case VersionFunction.Build when args.Length == 0:
                result = version.Build;
                return true;

            case VersionFunction.Revision when args.Length == 0:
                result = version.Revision;
                return true;

            case VersionFunction.MajorRevision when args.Length == 0:
                result = version.MajorRevision;
                return true;

            case VersionFunction.MinorRevision when args.Length == 0:
                result = version.MinorRevision;
                return true;

            case VersionFunction.ToString:
                if (args.Length == 0)
                {
                    result = version.ToString();
                    return true;
                }

                if (args.TryGetArg(out int fieldCount))
                {
                    result = version.ToString(fieldCount);
                    return true;
                }

                break;
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteVersionConstructor(ref FunctionArguments args, out object? result)
    {
        if (args.Length == 0)
        {
            result = new Version();
            return true;
        }

        if (args.TryGetArg(out Version? version))
        {
            result = version;
            return true;
        }

        result = null;
        return false;
    }

    private static VersionFunction GetVersionFunction(StringSegment name)
    {
        switch (name.Length)
        {
            case 5:
                if (name.Equals(nameof(Version.Build), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.Build;
                }

                if (name.Equals(nameof(Version.Major), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.Major;
                }

                if (name.Equals(nameof(Version.Minor), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.Minor;
                }

                if (name.Equals(nameof(Version.Parse), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.Parse;
                }

                break;
            case 8:
                if (name.Equals(nameof(Version.Revision), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.Revision;
                }

                if (name.Equals(nameof(Version.ToString), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.ToString;
                }

                break;
            case 13:
                if (name.Equals(nameof(Version.MajorRevision), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.MajorRevision;
                }

                if (name.Equals(nameof(Version.MinorRevision), StringComparison.OrdinalIgnoreCase))
                {
                    return VersionFunction.MinorRevision;
                }

                break;
        }

        return VersionFunction.None;
    }
}
