// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Provides an abstraction for registry access operations.
/// This interface enables testability and decouples registry-dependent code
/// from direct static method calls.
/// </summary>
[SupportedOSPlatform("windows")]
internal interface IRegistryService
{
    /// <summary>
    /// Opens a base registry key for the specified hive and view.
    /// </summary>
    /// <param name="hive">The registry hive to open.</param>
    /// <param name="view">The registry view (32-bit, 64-bit, or default).</param>
    /// <returns>A <see cref="RegistryKey"/> for the specified hive and view.</returns>
    RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view);

    /// <summary>
    /// Gets the names of all subkeys under the specified base key and subkey path.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The path to the subkey.</param>
    /// <returns>An enumeration of subkey names, or null if the subkey does not exist.</returns>
    IEnumerable<string>? GetSubKeyNames(RegistryKey baseKey, string subKey);

    /// <summary>
    /// Gets the default value of the specified subkey.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The path to the subkey.</param>
    /// <returns>The default value as a string, or null if not found.</returns>
    string? GetDefaultValue(RegistryKey baseKey, string subKey);
}
