// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Resources;

namespace Microsoft.Build.Shared;

/// <summary>
///  This class provides access to the assembly's resources.
/// </summary>
internal static class AssemblyResources
{
    /// <summary>
    ///  Gets the assembly's primary resources, i.e. the resources exclusively owned by this assembly.
    /// </summary>
    internal static ResourceManager PrimaryResources { get; } = new ResourceManager("MSBuild.Strings", typeof(AssemblyResources).Assembly);
}
