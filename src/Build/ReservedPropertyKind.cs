// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  Identifies a reserved MSBuild property.
/// </summary>
internal enum ReservedPropertyKind
{
    /// <summary>
    ///  Does not identify a reserved property.
    /// </summary>
    None,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectDirectory"/> property.
    /// </summary>
    ProjectDirectory,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectDirectoryNoRoot"/> property.
    /// </summary>
    ProjectDirectoryNoRoot,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectFile"/> property.
    /// </summary>
    ProjectFile,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectExtension"/> property.
    /// </summary>
    ProjectExtension,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectFullPath"/> property.
    /// </summary>
    ProjectFullPath,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectName"/> property.
    /// </summary>
    ProjectName,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ThisFileDirectory"/> property.
    /// </summary>
    ThisFileDirectory,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ThisFileDirectoryNoRoot"/> property.
    /// </summary>
    ThisFileDirectoryNoRoot,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ThisFile"/> property.
    /// </summary>
    ThisFile,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ThisFileExtension"/> property.
    /// </summary>
    ThisFileExtension,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ThisFileFullPath"/> property.
    /// </summary>
    ThisFileFullPath,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ThisFileName"/> property.
    /// </summary>
    ThisFileName,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.BinPath"/> property.
    /// </summary>
    BinPath,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProjectDefaultTargets"/> property.
    /// </summary>
    ProjectDefaultTargets,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ToolsPath"/> property.
    /// </summary>
    ToolsPath,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ToolsVersion"/> property.
    /// </summary>
    ToolsVersion,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.MSBuildRuntimeType"/> property.
    /// </summary>
    RuntimeType,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.StartupDirectory"/> property.
    /// </summary>
    StartupDirectory,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.BuildNodeCount"/> property.
    /// </summary>
    BuildNodeCount,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.LastTaskResult"/> property.
    /// </summary>
    LastTaskResult,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.ProgramFiles32"/> property.
    /// </summary>
    ProgramFiles32,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.AssemblyVersion"/> property.
    /// </summary>
    AssemblyVersion,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.Version"/> property.
    /// </summary>
    Version,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.Interactive"/> property.
    /// </summary>
    Interactive,

    /// <summary>
    ///  Identifies the <see cref="ReservedPropertyNames.MSBuildDisableFeaturesFromVersion"/> property.
    /// </summary>
    DisableFeaturesFromVersion,
}
