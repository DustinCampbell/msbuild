// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Frozen;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

/// <summary>
///  Provides the names of reserved properties that can be set only by MSBuild.
/// </summary>
internal static class ReservedPropertyNames
{
    // NOTE: if you add to this list, update the ReservedProperties hashtable below
    internal const string projectDirectory = "MSBuildProjectDirectory";
    internal const string projectDirectoryNoRoot = "MSBuildProjectDirectoryNoRoot";
    internal const string projectFile = "MSBuildProjectFile";
    internal const string projectExtension = "MSBuildProjectExtension";
    internal const string projectFullPath = "MSBuildProjectFullPath";
    internal const string projectName = "MSBuildProjectName";
    internal const string thisFileDirectory = "MSBuildThisFileDirectory";
    internal const string thisFileDirectoryNoRoot = "MSBuildThisFileDirectoryNoRoot";
    internal const string thisFile = "MSBuildThisFile"; // "MSBuildThisFileFile" sounds silly!
    internal const string thisFileExtension = "MSBuildThisFileExtension";
    internal const string thisFileFullPath = "MSBuildThisFileFullPath";
    internal const string thisFileName = "MSBuildThisFileName";
    internal const string binPath = "MSBuildBinPath";
    internal const string projectDefaultTargets = "MSBuildProjectDefaultTargets";
    internal const string extensionsPath = "MSBuildExtensionsPath";
    internal const string extensionsPath32 = "MSBuildExtensionsPath32";
    internal const string extensionsPath64 = "MSBuildExtensionsPath64";
    internal const string userExtensionsPath = "MSBuildUserExtensionsPath";
    internal const string toolsPath = MSBuildConstants.ToolsPath;
    internal const string toolsVersion = "MSBuildToolsVersion";
    internal const string msbuildRuntimeType = "MSBuildRuntimeType";
    internal const string overrideTasksPath = "MSBuildOverrideTasksPath";
    internal const string defaultOverrideToolsVersion = "DefaultOverrideToolsVersion";
    internal const string startupDirectory = "MSBuildStartupDirectory";
    internal const string buildNodeCount = "MSBuildNodeCount";
    internal const string lastTaskResult = "MSBuildLastTaskResult";
    internal const string extensionsPathSuffix = "MSBuild";
    internal const string userExtensionsPathSuffix = "Microsoft\\MSBuild";
    internal const string programFiles32 = "MSBuildProgramFiles32";
    internal const string localAppData = "LocalAppData";
    internal const string assemblyVersion = "MSBuildAssemblyVersion";
    internal const string fileVersion = "MSBuildFileVersion";
    internal const string semanticVersion = "MSBuildSemanticVersion";
    internal const string version = "MSBuildVersion";
    internal const string osName = "OS";
    internal const string frameworkToolsRoot = "MSBuildFrameworkToolsRoot";
    internal const string interactive = "MSBuildInteractive";
    internal const string msbuilddisablefeaturesfromversion = "MSBuildDisableFeaturesFromVersion";

    /// <summary>
    ///  The set of reserved property names.
    /// </summary>
    /// <remarks>
    ///  This intentionally excludes <c>MSBuildExtensionsPath*</c> and <c>MSBuildUserExtensionsPath</c>
    ///  properties because tasks must be able to override them.
    /// </remarks>
    private static readonly FrozenSet<string> s_reservedProperties = FrozenSet.Create(
        MSBuildNameIgnoreCaseComparer.Default,
        projectDirectory,
        projectDirectoryNoRoot,
        projectFile,
        projectExtension,
        projectFullPath,
        projectName,

        thisFileDirectory,
        thisFileDirectoryNoRoot,
        thisFile,
        thisFileExtension,
        thisFileFullPath,
        thisFileName,

        binPath,
        projectDefaultTargets,
        toolsPath,
        toolsVersion,
        msbuildRuntimeType,
        startupDirectory,
        buildNodeCount,
        lastTaskResult,
        programFiles32,
        assemblyVersion,
        version,
        interactive,
        msbuilddisablefeaturesfromversion);

    /// <summary>
    ///  Indicates whether the specified property name is reserved.
    /// </summary>
    /// <param name="name">The property name to test.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="name"/> is reserved; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsReservedProperty(string name)
        => s_reservedProperties.Contains(name);
}
