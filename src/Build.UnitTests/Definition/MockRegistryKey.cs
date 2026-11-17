// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WIN32_REGISTRY

using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;
using Microsoft.Win32;

namespace Microsoft.Build.UnitTests.Definition;

/// <summary>
///  Provides a registry wrapper that can simulate missing subkeys and registry-access failures.
/// </summary>
internal sealed class MockRegistryKey : RegistryKeyWrapper
{
    public enum WhereToThrow
    {
        None,
        Name,
        GetValue,
        GetValueNames,
        GetSubKeyNames,
        OpenSubKey
    }

    private WhereToThrow _whereToThrow = WhereToThrow.None;
    private string? _subKeyThatDoesNotExist;

    /// <summary>
    ///  Constructs a wrapper around the specified key.
    /// </summary>
    /// <param name="wrappedKey">The key to wrap, or <see langword="null"/> to represent a missing key.</param>
    /// <param name="registryHive">The registry hive containing the key.</param>
    private MockRegistryKey(RegistryKey? wrappedKey, RegistryKey registryHive)
        : base(wrappedKey, registryHive)
    {
    }

    /// <summary>
    ///  Constructs a wrapper for a path under <see cref="Registry.CurrentUser"/>.
    /// </summary>
    /// <param name="path">The path relative to the current-user registry hive.</param>
    public MockRegistryKey(string path)
        : base(path, Registry.CurrentUser)
    {
    }

    /// <summary>
    ///  Constructs a wrapper with a designated member that throws.
    /// </summary>
    /// <param name="path">The path relative to the current-user registry hive.</param>
    /// <param name="whereToThrow">The member that throws when accessed.</param>
    public MockRegistryKey(string path, WhereToThrow whereToThrow)
        : base(path, Registry.CurrentUser)
    {
        _whereToThrow = whereToThrow;
    }

    /// <summary>
    ///  Constructs a wrapper that treats a designated subkey as missing.
    /// </summary>
    /// <param name="path">The path relative to the current-user registry hive.</param>
    /// <param name="subKeyThatDoesNotExist">The name of the subkey to treat as missing.</param>
    public MockRegistryKey(string path, string subKeyThatDoesNotExist)
        : base(path, Registry.CurrentUser)
    {
        _subKeyThatDoesNotExist = subKeyThatDoesNotExist;
    }

    /// <summary>
    ///  Gets the name of the wrapped registry key.
    /// </summary>
    public override string Name
    {
        get
        {
            if (_whereToThrow == WhereToThrow.Name)
            {
                throw new RegistryException("registryException", "registry");
            }

            return base.Name;
        }
    }

    /// <summary>
    ///  Gets the value with the specified <paramref name="name"/> from the registry key.
    /// </summary>
    /// <param name="name">The value name, or <see langword="null"/> for the default value.</param>
    /// <returns>
    ///  The value, or <see langword="null"/> if the key or value does not exist.
    /// </returns>
    public override object? GetValue(string? name)
    {
        if (_whereToThrow == WhereToThrow.GetValue)
        {
            throw new RegistryException("registryException", "registry");
        }

        return base.GetValue(name);
    }

    /// <summary>
    ///  Gets the names of all values under the registry key.
    /// </summary>
    /// <returns>
    ///  The value names, or an empty array if the key does not exist.
    /// </returns>
    public override string[] GetValueNames()
    {
        if (_whereToThrow == WhereToThrow.GetValueNames)
        {
            throw new RegistryException("registryException", "registry");
        }

        return base.GetValueNames();
    }

    /// <summary>
    ///  Gets the names of all immediate subkeys.
    /// </summary>
    /// <returns>
    ///  The subkey names, or an empty array if the key does not exist.
    /// </returns>
    public override string[] GetSubKeyNames()
    {
        if (_whereToThrow == WhereToThrow.GetSubKeyNames)
        {
            throw new RegistryException("registryException", "registry");
        }

        return base.GetSubKeyNames();
    }

    /// <summary>
    ///  Opens the subkey with the specified <paramref name="name"/> for read-only access.
    /// </summary>
    /// <param name="name">The name of the subkey to open.</param>
    /// <returns>
    ///  A wrapper around the subkey, or a missing key if the subkey is configured as missing.
    /// </returns>
    public override RegistryKeyWrapper OpenSubKey(string name)
    {
        if (_whereToThrow == WhereToThrow.OpenSubKey)
        {
            throw new RegistryException("registryException", "registry");
        }

        if (_subKeyThatDoesNotExist == name)
        {
            // Return wrapper around null key
            return new MockRegistryKey(wrappedKey: null, registryHive: Registry.LocalMachine);
        }

        return base.OpenSubKey(name);
    }
}

#endif
