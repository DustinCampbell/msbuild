// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.TaskHost;

internal static class MSBuildRuntime
{
    private const string CLR2 = "CLR2";
    private const string CLR4 = "CLR4";
    private const string NET = "NET";

    public static bool TryGetClrVersion(string msbuildRuntime, out int clrVersion)
    {
        if (string.Equals(msbuildRuntime, CLR2, System.StringComparison.OrdinalIgnoreCase))
        {
            clrVersion = 2;
            return true;
        }

        if (string.Equals(msbuildRuntime, CLR4, System.StringComparison.OrdinalIgnoreCase))
        {
            clrVersion = 4;
            return true;
        }

        if (string.Equals(msbuildRuntime, NET, System.StringComparison.OrdinalIgnoreCase))
        {
            clrVersion = 5;
            return true;
        }

        clrVersion = default;
        return false;
    }
}
