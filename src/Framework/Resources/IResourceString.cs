// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework.Resources;

/// <summary>
///  Represents a resource string whose culture lookup and formatting are deferred until it is needed.
/// </summary>
internal interface IResourceString
{
    /// <summary>
    ///  Resolves the resource string for the effective culture.
    /// </summary>
    /// <returns>
    ///  The resolved resource string.
    /// </returns>
    ResolvedResourceString Resolve();
}
