// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Resources;

/// <summary>
///  Provides common operations for deferred resource strings.
/// </summary>
internal static class ResourceStringUtilities
{
    /// <summary>
    ///  Resolves a resource string and returns its localized text with any MSBuild diagnostic code removed.
    /// </summary>
    /// <typeparam name="TResource">The concrete resource-string carrier type.</typeparam>
    /// <param name="resource">The resource string to resolve.</param>
    /// <returns>
    ///  The localized resource text.
    /// </returns>
    public static string GetText<TResource>(TResource resource)
        where TResource : IResourceString
        => resource.Resolve().Text;
}
