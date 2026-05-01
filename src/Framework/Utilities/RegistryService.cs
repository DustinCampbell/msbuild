// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.Build.Utilities;

/// <summary>
/// Default implementation of <see cref="IRegistryService"/> that performs
/// actual registry operations using the Windows Registry API.
/// </summary>
/// <remarks>
/// This class provides a singleton <see cref="Instance"/> for production use.
/// For testing, create a mock implementation of <see cref="IRegistryService"/>.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed class RegistryService : IRegistryService
{
    /// <summary>
    /// Gets the singleton instance of <see cref="RegistryService"/>.
    /// </summary>
    public static RegistryService Instance { get; } = new RegistryService();

    private RegistryService()
    {
    }

    /// <inheritdoc/>
    public RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
        => RegistryKey.OpenBaseKey(hive, view);

    /// <inheritdoc/>
    public IEnumerable<string>? GetSubKeyNames(RegistryKey baseKey, string subKey)
    {
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key?.GetSubKeyNames();
    }

    /// <inheritdoc/>
    public string? GetDefaultValue(RegistryKey baseKey, string subKey)
    {
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key?.ValueCount > 0 ? (string?)key.GetValue("") : null;
    }
}
