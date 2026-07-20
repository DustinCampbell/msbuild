// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Resources;

namespace Microsoft.Build.Framework.Utilities;

/// <summary>
///  Loads named resource strings from a primary <see cref="ResourceManager"/>, falling back to an optional
///  shared <see cref="ResourceManager"/> when a resource is not found in the primary one.
/// </summary>
/// <param name="primaryResources">The resource manager consulted first when loading a resource.</param>
/// <param name="sharedResources">
///  An optional resource manager consulted when a resource is not found in <paramref name="primaryResources"/>.
/// </param>
internal sealed class ResourceProvider(ResourceManager primaryResources, ResourceManager? sharedResources = null)
{
    /// <summary>
    ///  Gets the underlying primary <see cref="ResourceManager"/>.
    /// </summary>
    public ResourceManager PrimaryResources => primaryResources;

    /// <summary>
    ///  Gets the underlying shared <see cref="ResourceManager"/>, if any.
    /// </summary>
    public ResourceManager? SharedResources => sharedResources;

    /// <summary>
    ///  Loads the specified resource string from the primary resources, falling back to the shared resources.
    /// </summary>
    /// <param name="name">The name of the resource to retrieve.</param>
    /// <param name="culture">The culture for which the resource is localized, or <see langword="null"/> to use the current culture.</param>
    /// <returns>
    ///  The resource string.
    /// </returns>
    /// <exception cref="InternalErrorException">Thrown if the resource is not found.</exception>
    public string GetString(string name, CultureInfo? culture = null)
    {
        string? resource = GetStringOrNull(name, culture);

        Assumed.NotNull(resource, $"Missing resource '{name}'");

        return resource;
    }

    /// <summary>
    ///  Loads the specified resource string from the primary resources, falling back to the shared resources.
    /// </summary>
    /// <param name="name">The name of the resource to retrieve.</param>
    /// <param name="culture">The culture for which the resource is localized, or <see langword="null"/> to use the current culture.</param>
    /// <returns>
    ///  The resource string if found; otherwise, <see langword="null"/>.
    /// </returns>
    public string? GetStringOrNull(string name, CultureInfo? culture = null)
        => primaryResources.GetString(name, culture) ?? sharedResources?.GetString(name, culture);
}
