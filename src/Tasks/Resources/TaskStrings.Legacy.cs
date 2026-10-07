// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks.Resources;

internal static partial class TaskStrings
{
    internal const string ManifestLegacy_CertNotInStore = "CertNotInStore";

    internal static ResourceString ResolveComReferenceLegacy_BadTargetFrameworkFormat { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "ResolveComReference.BadTargetFrameworkFormat", "MSBuild.");

    internal static ResourceString XamlLegacy_ImproperType { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "ImproperType", "MSBuild.");

    internal static ResourceString XamlLegacy_InvalidLanguage { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "InvalidLanguage", "MSBuild.");

    internal static ResourceString XamlLegacy_InvalidNamespace { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "InvalidNamespace", "MSBuild.");

    internal static ResourceString XamlLegacy_LoadFailed { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "LoadFailed", "MSBuild.");

    internal static ResourceString XamlLegacy_MissingAttribute { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "MissingAttribute", "MSBuild.");

    internal static ResourceString XamlLegacy_MissingRootElement { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "MissingRootElement", "MSBuild.");

    internal static ResourceString XamlLegacy_NoChildren { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "NoChildren", "MSBuild.");

    internal static ResourceString XamlLegacy_XmlError { get; } =
        new ResourceString(AssemblyResources.PrimaryResources, null, "XmlError", "MSBuild.");
}
