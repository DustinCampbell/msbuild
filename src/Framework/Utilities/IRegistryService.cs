// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Win32;

namespace Microsoft.Build.Shared;

internal interface IRegistryService
{
    /// <inheritdoc cref="RegistryKey.OpenBaseKey(RegistryHive, RegistryView)"/>
    RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view);

    /// <summary>
    ///  Get a base <see cref="RegistryKey"/> and a sub-key name, get all of its sub-key names.
    /// </summary>
    /// <param name="baseKey">The base <see cref="RegistryKey"/>.</param>
    /// <param name="subKey">The name of the sub-key.</param>
    /// <returns>
    ///  An enumerable of sub-key names, or <see langword="null"/> if the sub-key does not exist.
    /// </returns>
    IEnumerable<string>? GetSubKeyNames(RegistryKey baseKey, string subKey);

    /// <summary>
    ///  Get the default value of a sub-key.
    /// </summary>
    /// <param name="baseKey">The base <see cref="RegistryKey"/>.</param>
    /// <param name="subKey">The name of the sub-key.</param>
    /// <returns>
    ///  The default value of the sub-key, or <see langword="null"/> if the sub-key does not exist.
    /// </returns>
    string? GetDefaultValue(RegistryKey baseKey, string subKey);
}
