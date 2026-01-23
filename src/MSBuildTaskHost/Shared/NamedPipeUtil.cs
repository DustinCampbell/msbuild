// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Shared;

internal static class NamedPipeUtil
{
    internal static string GetPlatformSpecificPipeName(int? processId = null)
    {
        processId ??= EnvironmentUtilities.CurrentProcessId;
        string pipeName = $"MSBuild{processId}";

        return pipeName;
    }
}
