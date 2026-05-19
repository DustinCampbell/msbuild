// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Microsoft.Build.Shared
{
    /// <summary>
    ///  Helper methods that simplify registry access.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal sealed class RegistryService : IRegistryService
    {
        public static IRegistryService Default { get; } = new RegistryService();

        private RegistryService()
        {
        }

        public RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
            => RegistryKey.OpenBaseKey(hive, view);

        public IEnumerable<string>? GetSubKeyNames(RegistryKey baseKey, string subKey)
        {
            using (RegistryKey? key = baseKey.OpenSubKey(subKey))
            {
                if (key != null)
                {
                    return key.GetSubKeyNames();
                }
            }

            return null;
        }

        public string? GetDefaultValue(RegistryKey baseKey, string subKey)
        {
            using (RegistryKey? key = baseKey.OpenSubKey(subKey))
            {
                if (key?.ValueCount > 0)
                {
                    return (string?)key.GetValue("");
                }
            }

            return null;
        }
    }
}
