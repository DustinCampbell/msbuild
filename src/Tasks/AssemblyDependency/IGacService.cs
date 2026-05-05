// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Tasks;

/// <summary>
/// Provides an abstraction for Global Assembly Cache (GAC) operations.
/// This interface enables testability and decouples GAC-dependent code
/// from direct static method calls.
/// </summary>
[SupportedOSPlatform("windows")]
internal interface IGacService
{
    /// <summary>
    /// Enumerates assemblies in the GAC that match the specified strong name.
    /// </summary>
    /// <param name="strongName">The strong name to match.</param>
    /// <returns>An enumeration of matching assembly names, or <see langword="null"/> if none found.</returns>
    IEnumerable<AssemblyNameExtension> GetGacEnumerator(string strongName);

    /// <summary>
    /// Gets the file system path for an assembly given its fusion name.
    /// </summary>
    /// <param name="strongName">The fusion name of the assembly.</param>
    /// <returns>The path to the assembly, or <see langword="null"/> if not found.</returns>
    string GetPathFromFusionName(string strongName);
}
