// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.TaskHost.FileSystem;

internal static class FileSystems
{
    public static readonly MSBuildTaskHostFileSystem Default = MSBuildTaskHostFileSystem.Instance;
}
