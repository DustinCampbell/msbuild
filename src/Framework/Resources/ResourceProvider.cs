// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.Build.Framework;

/// <summary>
///  Resolves localized strings for an assembly from its primary and (optional) shared resource
///  managers and produces <see cref="ResourceString"/> handles that defer both lookup and
///  formatting until a value is actually needed.
/// </summary>
/// <remarks>
///  This is the relocated, allocation-conscious replacement for each assembly's
///  <c>AssemblyResources</c> class. Each assembly constructs a single instance bound to its own
///  <see cref="ResourceManager"/>s and exposes strongly-typed <see cref="ResourceString"/> fields.
/// </remarks>
internal sealed class ResourceProvider
{
    private readonly ResourceManager _primary;
    private readonly ResourceManager? _shared;

    public ResourceProvider(ResourceManager primary, ResourceManager? shared = null)
    {
        _primary = primary;
        _shared = shared;
    }

    public ResourceProvider(string primaryBaseName, string? sharedBaseName, Assembly assembly)
        : this(
            new ResourceManager(primaryBaseName, assembly),
            sharedBaseName is null ? null : new ResourceManager(sharedBaseName, assembly))
    {
    }

    /// <summary>
    ///  Gets the underlying primary <see cref="ResourceManager"/>.
    /// </summary>
    public ResourceManager PrimaryResources => _primary;

    /// <summary>
    ///  Gets the underlying shared <see cref="ResourceManager"/>, if any.
    /// </summary>
    public ResourceManager? SharedResources => _shared;

    /// <summary>
    ///  Creates a <see cref="ResourceString"/> handle for the named resource. The lookup is
    ///  deferred until the handle is formatted.
    /// </summary>
    public ResourceString this[string name] => new(this, name);

    /// <summary>
    ///  Loads the specified resource string from the primary resources, falling back to the shared
    ///  resources. Throws an <see cref="InternalErrorException"/> if the resource is not found.
    /// </summary>
    /// <remarks>This method is thread-safe.</remarks>
    public string GetString(string name)
    {
        string? resource = GetStringOrNull(name);

        Assumed.NotNull(resource, $"Missing resource '{name}'");

        return resource;
    }

    /// <summary>
    ///  Loads the specified resource string from the primary resources, falling back to the shared
    ///  resources. Returns <see langword="null"/> if the resource is not found.
    /// </summary>
    /// <remarks>This method is thread-safe.</remarks>
    public string? GetStringOrNull(string name)
    {
        // NOTE: the ResourceManager.GetString() method is thread-safe.
        string? resource = _primary.GetString(name, CultureInfo.CurrentUICulture);

        if (resource is null && _shared is not null)
        {
            resource = _shared.GetString(name, CultureInfo.CurrentUICulture);
        }

        return resource;
    }
}
