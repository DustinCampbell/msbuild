// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using static Microsoft.Build.Evaluation.Expander.ArgumentParser;

namespace Microsoft.Build.Evaluation.Expander
{
    internal static partial class WellKnownFunctions
    {
        private sealed class VersionLibrary : FunctionLibrary
        {
            public static readonly VersionLibrary Instance = new();

            private VersionLibrary()
            {
            }

            protected override void Initialize(ref Builder builder)
            {
                builder.Add(nameof(Version.Parse), Version_Parse);

                builder.Add<Version>(nameof(Version.CompareTo), Version_CompareTo);
                builder.Add<Version>(nameof(Version.Revision), Version_Revision);
                builder.Add<Version>(nameof(Version.ToString), Version_ToString);
            }

            private static bool Version_Parse(ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [string input])
                {
                    result = Version.Parse(input);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Version_CompareTo(Version v, ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [var arg0] && arg0 is Version or null)
                {
                    var value = (Version?)arg0;
                    result = v.CompareTo(value);
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Version_Revision(Version v, ReadOnlySpan<object?> args, out object? result)
            {
                if (args is [])
                {
                    result = v.Revision;
                    return true;
                }

                result = null;
                return false;
            }

            private static bool Version_ToString(Version v, ReadOnlySpan<object?> args, out object? result)
            {
                switch (args)
                {
                    case []:
                        result = v.ToString();
                        return true;

                    case [var arg0] when TryConvertToInt(arg0, out var fieldCount):
                        result = v.ToString(fieldCount);
                        return true;

                    default:
                        result = null;
                        return false;
                }
            }
        }
    }
}
