// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// File.GetAttributes delegate.
    /// </summary>
    /// <param name="path">The path get attributes for.</param>
    internal delegate FileAttributes GetAttributes(string path);

    /// <summary>
    /// File SetAttributes delegate.
    /// </summary>
    /// <param name="path">The path to set attributes for.</param>
    /// <param name="attributes">The actual file attributes.</param>
    internal delegate void SetAttributes(string path, FileAttributes attributes);

    /// <summary>
    /// File SetLastAccessTime delegate.
    /// </summary>
    internal delegate void SetLastAccessTime(string path, DateTime timestamp);

    /// <summary>
    /// File SetLastWriteTime delegate.
    /// </summary>
    internal delegate void SetLastWriteTime(string path, DateTime timestamp);

    /// <summary>
    /// GetAssemblyRuntimeVersion delegate to get the clr runtime version of a file.
    /// </summary>
    /// <param name="path">The path to the file</param>
    /// <returns>The clr runtime version for the file</returns>
    internal delegate string GetAssemblyRuntimeVersion(string path);

    /// <summary>
    /// GetGacEnumerator delegate to get the enumerator which will enumerate over the GAC
    /// </summary>
    /// <param name="strongName">StrongName to get an enumerator for</param>
    /// <returns>The enumerator for the gac</returns>
    internal delegate IEnumerable<AssemblyNameExtension> GetGacEnumerator(string strongName);

    /// <summary>
    /// GetPathFromFusionName delegate to get path to a file based on the fusion name
    /// </summary>
    /// <param name="strongName">StrongName to get a path for</param>
    /// <returns>The path to the assembly</returns>
    internal delegate string GetPathFromFusionName(string strongName);

    /// <summary>
    /// CreateFileString delegate. Creates a stream on top of a file.
    /// </summary>
    /// <param name="path">Path to the file</param>
    /// <param name="mode">File mode</param>
    /// <param name="access">Access type</param>
    /// <returns>The Stream</returns>
    internal delegate Stream CreateFileStream(AbsolutePath path, FileMode mode, FileAccess access);
}
