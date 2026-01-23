// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Shared.FileSystem;

/// <summary>
/// Factory for <see cref="IFileSystem"/>.
/// </summary>
internal static class FileSystems
{
    public static readonly MSBuildTaskHostFileSystem Default = MSBuildTaskHostFileSystem.Instance;
}
