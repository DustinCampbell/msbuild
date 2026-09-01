// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal;

/// <summary>
///  Contains a list of the special (reserved) properties that are settable by MSBuild code only.
/// </summary>
internal static class ReservedPropertyNames
{
    private const string PropertyPrefix = "MSBuild";
    private const string ProjectPropertyPrefix = $"{PropertyPrefix}Project";
    private const string ThisFilePropertyPrefix = $"{PropertyPrefix}ThisFile";

    public const string ProjectDirectory = $"{ProjectPropertyPrefix}Directory";
    public const string ProjectDirectoryNoRoot = $"{ProjectPropertyPrefix}DirectoryNoRoot";
    public const string ProjectFile = $"{ProjectPropertyPrefix}File";
    public const string ProjectExtension = $"{ProjectPropertyPrefix}Extension";
    public const string ProjectFullPath = $"{ProjectPropertyPrefix}FullPath";
    public const string ProjectName = $"{ProjectPropertyPrefix}Name";

    public const string ThisFileDirectory = $"{ThisFilePropertyPrefix}Directory";
    public const string ThisFileDirectoryNoRoot = $"{ThisFilePropertyPrefix}DirectoryNoRoot";
    public const string ThisFile = $"{ThisFilePropertyPrefix}"; // MSBuildThisFile rather than MSBuildThisFileFile
    public const string ThisFileExtension = $"{ThisFilePropertyPrefix}Extension";
    public const string ThisFileFullPath = $"{ThisFilePropertyPrefix}FullPath";
    public const string ThisFileName = $"{ThisFilePropertyPrefix}Name";

    public const string BinPath = $"{PropertyPrefix}BinPath";
    public const string ProjectDefaultTargets = $"{PropertyPrefix}ProjectDefaultTargets";
    public const string ToolsPath = MSBuildConstants.ToolsPath;
    public const string ToolsVersion = $"{PropertyPrefix}ToolsVersion";
    public const string MSBuildRuntimeType = $"{PropertyPrefix}RuntimeType";
    public const string StartupDirectory = $"{PropertyPrefix}StartupDirectory";
    public const string BuildNodeCount = $"{PropertyPrefix}NodeCount";
    public const string LastTaskResult = $"{PropertyPrefix}LastTaskResult";
    public const string ProgramFiles32 = $"{PropertyPrefix}ProgramFiles32";
    public const string AssemblyVersion = $"{PropertyPrefix}AssemblyVersion";
    public const string Version = $"{PropertyPrefix}Version";
    public const string Interactive = $"{PropertyPrefix}Interactive";
    public const string MSBuildDisableFeaturesFromVersion = $"{PropertyPrefix}DisableFeaturesFromVersion";

    // These property names are intentionally not reserved. In particular, MSBuildExtensionsPath* and
    // MSBuildUserExtensionsPath must remain settable because tasks need to be able to override them.
    public const string ExtensionsPath = $"{PropertyPrefix}ExtensionsPath";
    public const string ExtensionsPath32 = $"{PropertyPrefix}ExtensionsPath32";
    public const string ExtensionsPath64 = $"{PropertyPrefix}ExtensionsPath64";
    public const string UserExtensionsPath = $"{PropertyPrefix}UserExtensionsPath";
    public const string OverrideTasksPath = $"{PropertyPrefix}OverrideTasksPath";
    public const string DefaultOverrideToolsVersion = "DefaultOverrideToolsVersion";
    public const string ExtensionsPathSuffix = "MSBuild";
    public const string UserExtensionsPathSuffix = "Microsoft\\MSBuild";
    public const string LocalAppData = "LocalAppData";
    public const string FileVersion = $"{PropertyPrefix}FileVersion";
    public const string SemanticVersion = $"{PropertyPrefix}SemanticVersion";
    public const string OSName = "OS";
    public const string FrameworkToolsRoot = $"{PropertyPrefix}FrameworkToolsRoot";

    /// <summary>
    ///  Attempts to classify a reserved property name within a region of a string.
    /// </summary>
    /// <param name="propertyName">The string containing the property name.</param>
    /// <param name="offset">The zero-based offset at which the property name begins.</param>
    /// <param name="length">The number of characters in the property name.</param>
    /// <param name="result">
    ///  When this method returns <see langword="true"/>, the reserved property kind; otherwise,
    ///  <see cref="ReservedPropertyKind.None"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when the specified region identifies a reserved property; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool TryGetReservedPropertyKind(string propertyName, int offset, int length, out ReservedPropertyKind result)
    {
        int prefixLength = PropertyPrefix.Length;

        if (length <= prefixLength)
        {
            result = ReservedPropertyKind.None;
            return false;
        }

        char charAfterPrefix = propertyName[offset + prefixLength];

        if (charAfterPrefix is 'P' or 'p' &&
            TryGetProjectProperty(propertyName, offset, length, out result))
        {
            return true;
        }

        if (charAfterPrefix is 'T' or 't' &&
            length > prefixLength + 1 &&
            propertyName[offset + prefixLength + 1] is 'H' or 'h' &&
            TryGetThisFileProperty(propertyName, offset, length, out result))
        {
            return true;
        }

        result = length switch
        {
            // MSBuildBinPath
            14 when charAfterPrefix is 'B' or 'b' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(BinPath, propertyName, offset, length)
                => ReservedPropertyKind.BinPath,

            // MSBuildVersion
            14 when charAfterPrefix is 'V' or 'v' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(Version, propertyName, offset, length)
                => ReservedPropertyKind.Version,

            // MSBuildNodeCount
            16 when charAfterPrefix is 'N' or 'n' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(BuildNodeCount, propertyName, offset, length)
                => ReservedPropertyKind.BuildNodeCount,

            // MSBuildToolsPath
            16 when charAfterPrefix is 'T' or 't' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ToolsPath, propertyName, offset, length)
                => ReservedPropertyKind.ToolsPath,

            // MSBuildInteractive
            18 when charAfterPrefix is 'I' or 'i' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(Interactive, propertyName, offset, length)
                => ReservedPropertyKind.Interactive,

            // MSBuildRuntimeType
            18 when charAfterPrefix is 'R' or 'r' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(MSBuildRuntimeType, propertyName, offset, length)
                => ReservedPropertyKind.RuntimeType,

            // MSBuildToolsVersion
            19 when charAfterPrefix is 'T' or 't' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ToolsVersion, propertyName, offset, length)
                => ReservedPropertyKind.ToolsVersion,

            // MSBuildLastTaskResult
            21 when charAfterPrefix is 'L' or 'l' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(LastTaskResult, propertyName, offset, length)
                => ReservedPropertyKind.LastTaskResult,

            // MSBuildProgramFiles32
            21 when charAfterPrefix is 'P' or 'p' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ProgramFiles32, propertyName, offset, length)
                => ReservedPropertyKind.ProgramFiles32,

            // MSBuildAssemblyVersion
            22 when charAfterPrefix is 'A' or 'a' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(AssemblyVersion, propertyName, offset, length)
                => ReservedPropertyKind.AssemblyVersion,

            // MSBuildStartupDirectory
            23 when charAfterPrefix is 'S' or 's' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(StartupDirectory, propertyName, offset, length)
                => ReservedPropertyKind.StartupDirectory,

            // MSBuildProjectDefaultTargets
            28 when charAfterPrefix is 'P' or 'p' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectDefaultTargets, propertyName, offset, length)
                => ReservedPropertyKind.ProjectDefaultTargets,

            // MSBuildDisableFeaturesFromVersion
            33 when charAfterPrefix is 'D' or 'd' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(MSBuildDisableFeaturesFromVersion, propertyName, offset, length)
                => ReservedPropertyKind.DisableFeaturesFromVersion,

            _ => ReservedPropertyKind.None,
        };

        return result != ReservedPropertyKind.None;
    }

    /// <summary>
    ///  Attempts to classify a reserved property whose value describes the current project file.
    /// </summary>
    /// <param name="propertyName">The string containing the property name.</param>
    /// <param name="offset">The zero-based offset at which the property name begins.</param>
    /// <param name="length">The number of characters in the property name.</param>
    /// <param name="result">
    ///  When this method returns <see langword="true"/>, the reserved property kind; otherwise,
    ///  <see cref="ReservedPropertyKind.None"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when the specified region identifies a project-file property; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool TryGetProjectProperty(string propertyName, int offset, int length, out ReservedPropertyKind result)
    {
        int prefixLength = ProjectPropertyPrefix.Length;

        // If length is less than "MSBuildProject", we're done.
        if (length < prefixLength)
        {
            result = ReservedPropertyKind.None;
            return false;
        }

        result = length switch
        {
            // MSBuildProjectFile
            18 when propertyName[offset + prefixLength] is 'F' or 'f' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectFile, propertyName, offset, length)
                => ReservedPropertyKind.ProjectFile,

            // MSBuildProjectName
            18 when propertyName[offset + prefixLength] is 'N' or 'n' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectName, propertyName, offset, length)
                => ReservedPropertyKind.ProjectName,

            // MSBuildProjectFullPath
            22 when MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectFullPath, propertyName, offset, length)
                => ReservedPropertyKind.ProjectFullPath,

            // MSBuildProjectDirectory
            23 when propertyName[offset + prefixLength] is 'D' or 'd' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectDirectory, propertyName, offset, length)
                => ReservedPropertyKind.ProjectDirectory,

            // MSBuildProjectExtension
            23 when propertyName[offset + prefixLength] is 'E' or 'e' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectExtension, propertyName, offset, length)
                => ReservedPropertyKind.ProjectExtension,

            // MSBuildProjectDirectoryNoRoot
            29 when MSBuildNameIgnoreCaseComparer.Default.Equals(ProjectDirectoryNoRoot, propertyName, offset, length)
                => ReservedPropertyKind.ProjectDirectoryNoRoot,

            _ => ReservedPropertyKind.None,
        };

        return result != ReservedPropertyKind.None;
    }

    /// <summary>
    ///  Attempts to classify a reserved property whose value describes the current imported file.
    /// </summary>
    /// <param name="propertyName">The string containing the property name.</param>
    /// <param name="offset">The zero-based offset at which the property name begins.</param>
    /// <param name="length">The number of characters in the property name.</param>
    /// <param name="result">
    ///  When this method returns <see langword="true"/>, the reserved property kind; otherwise,
    ///  <see cref="ReservedPropertyKind.None"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when the specified region identifies an imported-file property; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool TryGetThisFileProperty(string propertyName, int offset, int length, out ReservedPropertyKind result)
    {
        int prefixLength = ThisFilePropertyPrefix.Length;

        // If length is less than "MSBuildThisFile", we're done.
        if (length < prefixLength)
        {
            result = ReservedPropertyKind.None;
            return false;
        }

        result = length switch
        {
            // MSBuildThisFile
            15 when MSBuildNameIgnoreCaseComparer.Default.Equals(ThisFile, propertyName, offset, length)
                => ReservedPropertyKind.ThisFile,

            // MSBuildThisFileName
            19 when MSBuildNameIgnoreCaseComparer.Default.Equals(ThisFileName, propertyName, offset, length)
                => ReservedPropertyKind.ThisFileName,

            // MSBuildThisFileFullPath
            23 when MSBuildNameIgnoreCaseComparer.Default.Equals(ThisFileFullPath, propertyName, offset, length)
                => ReservedPropertyKind.ThisFileFullPath,

            // MSBuildThisFileDirectory
            24 when propertyName[offset + prefixLength] is 'D' or 'd' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ThisFileDirectory, propertyName, offset, length)
                => ReservedPropertyKind.ThisFileDirectory,

            // MSBuildThisFileExtension
            24 when propertyName[offset + prefixLength] is 'E' or 'e' &&
                    MSBuildNameIgnoreCaseComparer.Default.Equals(ThisFileExtension, propertyName, offset, length)
                => ReservedPropertyKind.ThisFileExtension,

            // MSBuildThisFileDirectoryNoRoot
            30 when MSBuildNameIgnoreCaseComparer.Default.Equals(ThisFileDirectoryNoRoot, propertyName, offset, length)
                => ReservedPropertyKind.ThisFileDirectoryNoRoot,

            _ => ReservedPropertyKind.None,
        };

        return result != ReservedPropertyKind.None;
    }

    /// <summary>
    ///  Determines whether the given property is a reserved property.
    /// </summary>
    /// <param name="propertyName">The property name to inspect.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="propertyName"/> is reserved; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsReservedProperty(string propertyName)
        => IsReservedProperty(propertyName, offset: 0, propertyName.Length);

    /// <summary>
    ///  Determines whether the given property is a reserved property.
    /// </summary>
    /// <param name="propertyName">The string containing the property name.</param>
    /// <param name="offset">The zero-based offset at which the property name begins.</param>
    /// <param name="length">The number of characters in the property name.</param>
    /// <returns>
    ///  <see langword="true"/> if the specified region identifies a reserved property; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool IsReservedProperty(string propertyName, int offset, int length)
        => propertyName is not null &&
           TryGetReservedPropertyKind(propertyName, offset, length, out _);
}
