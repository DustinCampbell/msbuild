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
internal class RegistryService : IRegistryService
{
    /// <summary>
    /// Gets the singleton instance of <see cref="RegistryService"/>.
    /// </summary>
    public static RegistryService Instance { get; } = new RegistryService();

    /// <summary>
    /// Initializes a new instance of the <see cref="RegistryService"/> class.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Instance"/> for production code. This constructor is
    /// available for testing scenarios where a fresh instance is needed.
    /// </remarks>
    protected RegistryService()
    {
    }

    /// <inheritdoc/>
    public virtual RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
    {
        return RegistryKey.OpenBaseKey(hive, view);
    }

    /// <inheritdoc/>
    public virtual IEnumerable<string>? GetSubKeyNames(RegistryKey baseKey, string subKey)
    {
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        return key?.GetSubKeyNames();
    }

    /// <inheritdoc/>
    public virtual string? GetDefaultValue(RegistryKey baseKey, string subKey)
    {
        using RegistryKey? key = baseKey.OpenSubKey(subKey);
        if (key?.ValueCount > 0)
        {
            return (string?)key.GetValue("");
        }

        return null;
    }
}
