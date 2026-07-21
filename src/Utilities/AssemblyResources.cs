// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build.Shared;

/// <summary>
///  This class provides access to the assembly's resources.
/// </summary>
internal static class AssemblyResources
{
    private const string PrimaryResourcesName = "Microsoft.Build.Utilities.Core.Strings";

    private static readonly ResourceProvider s_provider = new(
        primaryResources: new ResourceManager(PrimaryResourcesName, typeof(AssemblyResources).Assembly),
        sharedResources: Framework.Resources.SR.ResourceManager);

    /// <summary>
    ///  Gets the assembly's primary resources, i.e. the resources exclusively owned by this assembly.
    /// </summary>
    internal static ResourceManager PrimaryResources => s_provider.PrimaryResources;

    /// <summary>
    ///  Gets the assembly's shared resources, i.e. the resources this assembly shares with other assemblies.
    /// </summary>
    internal static ResourceManager SharedResources => s_provider.SharedResources!;

    /// <inheritdoc cref="ResourceProvider.GetString(string, CultureInfo?)" />
    internal static string GetString(string name, CultureInfo? culture = null)
        => s_provider.GetString(name, culture);

    private static ResourceString Create([NotNull] ref ResourceString? field, [CallerMemberName] string? name = null, CultureInfo? culture = null)
    {
        Assumed.NotNull(name);
        return field ?? InterlockedOperations.Initialize(ref field, new ResourceString(s_provider, name, culture));
    }

    internal static ResourceString CannotChangeItemSpecModifiers => Create(ref field);
    internal static ResourceString DebugPathTooLong => Create(ref field);
    internal static ResourceString FailedDeletingTempFile => Create(ref field);
    internal static ResourceString General_InvalidToolSwitch => Create(ref field, "General.InvalidToolSwitch");
    internal static ResourceString General_QuotesNotAllowedInThisKindOfTaskParameter => Create(ref field, "General.QuotesNotAllowedInThisKindOfTaskParameter");
    internal static ResourceString General_QuotesNotAllowedInThisKindOfTaskParameterNoSwitchName => Create(ref field, "General.QuotesNotAllowedInThisKindOfTaskParameterNoSwitchName");
    internal static ResourceString General_ToolCommandFailedNoErrorCode => Create(ref field, "General.ToolCommandFailedNoErrorCode");
    internal static ResourceString KillingProcess => Create(ref field);
    internal static ResourceString KillingProcessByCancellation => Create(ref field);
    internal static ResourceString LockCheck_FileLocked => Create(ref field, "LockCheck.FileLocked");
    internal static ResourceString LoggingBeforeTaskInitialization => Create(ref field);
    internal static ResourceString Message_InvalidImportance => Create(ref field);
    internal static ResourceString MuxLogger_BuildFinishedFailure => Create(ref field);
    internal static ResourceString MuxLogger_BuildFinishedSuccess => Create(ref field);
    internal static ResourceString PlatformManifest_MissingPlatformXml => Create(ref field, "PlatformManifest.MissingPlatformXml");
    internal static ResourceString TaskResourceNotFound => Create(ref field);
    internal static ResourceString TaskResourcesNotRegistered => Create(ref field);
    internal static ResourceString ToolLocationHelper_UnsupportedFrameworkVersion => Create(ref field, "ToolLocationHelper.UnsupportedFrameworkVersion");
    internal static ResourceString ToolLocationHelper_UnsupportedFrameworkVersionForWindowsSdk => Create(ref field, "ToolLocationHelper.UnsupportedFrameworkVersionForWindowsSdk");
    internal static ResourceString ToolLocationHelper_UnsupportedVisualStudioVersion => Create(ref field, "ToolLocationHelper.UnsupportedVisualStudioVersion");
    internal static ResourceString ToolsLocationHelper_CouldNotCreateChain => Create(ref field, "ToolsLocationHelper.CouldNotCreateChain");
    internal static ResourceString ToolsLocationHelper_CouldNotGenerateReferenceAssemblyDirectory => Create(ref field, "ToolsLocationHelper.CouldNotGenerateReferenceAssemblyDirectory");
    internal static ResourceString ToolsLocationHelper_InvalidRedistFile => Create(ref field, "ToolsLocationHelper.InvalidRedistFile");
    internal static ResourceString ToolTask_CommandTooLong => Create(ref field, "ToolTask.CommandTooLong");
    internal static ResourceString ToolTask_CouldNotStartToolExecutable => Create(ref field, "ToolTask.CouldNotStartToolExecutable");
    internal static ResourceString ToolTask_EnvironmentVariableHeader => Create(ref field, "ToolTask.EnvironmentVariableHeader");
    internal static ResourceString ToolTask_InvalidEnvironmentParameter => Create(ref field, "ToolTask.InvalidEnvironmentParameter");
    internal static ResourceString ToolTask_InvalidTerminationTimeout => Create(ref field, "ToolTask.InvalidTerminationTimeout");
    internal static ResourceString ToolTask_NotUpToDate => Create(ref field, "ToolTask.NotUpToDate");
    internal static ResourceString ToolTask_PipeEOFTimeout => Create(ref field, "ToolTask.PipeEOFTimeout");
    internal static ResourceString ToolTask_ToolCommandExitedZeroWithErrors => Create(ref field, "ToolTask.ToolCommandExitedZeroWithErrors");
    internal static ResourceString ToolTask_ToolCommandFailed => Create(ref field, "ToolTask.ToolCommandFailed");
    internal static ResourceString ToolTask_ToolExecutableNotFound => Create(ref field, "ToolTask.ToolExecutableNotFound");
    internal static ResourceString ToolTask_ValidateParametersFailed => Create(ref field, "ToolTask.ValidateParametersFailed");
    internal static ResourceString Tracking_AllOutputsAreUpToDate => Create(ref field);
    internal static ResourceString Tracking_DependenciesForRootNotFound => Create(ref field);
    internal static ResourceString Tracking_DependencyWasModifiedAt => Create(ref field);
    internal static ResourceString Tracking_InputNewerThanOutput => Create(ref field);
    internal static ResourceString Tracking_InputsFor => Create(ref field);
    internal static ResourceString Tracking_InputsNotShown => Create(ref field);
    internal static ResourceString Tracking_LogFilesNotAvailable => Create(ref field);
    internal static ResourceString Tracking_MissingInputs => Create(ref field);
    internal static ResourceString Tracking_MissingOutputs => Create(ref field);
    internal static ResourceString Tracking_OutputDoesNotExist => Create(ref field);
    internal static ResourceString Tracking_OutputForRootNotFound => Create(ref field);
    internal static ResourceString Tracking_OutputsFor => Create(ref field);
    internal static ResourceString Tracking_OutputsNotShown => Create(ref field);
    internal static ResourceString Tracking_ReadLogEntryNotFound => Create(ref field);
    internal static ResourceString Tracking_ReadTrackingCached => Create(ref field);
    internal static ResourceString Tracking_ReadTrackingLogs => Create(ref field);
    internal static ResourceString Tracking_RebuildingDueToInvalidTLog => Create(ref field);
    internal static ResourceString Tracking_RebuildingDueToInvalidTLogContents => Create(ref field);
    internal static ResourceString Tracking_SingleLogFileNotAvailable => Create(ref field);
    internal static ResourceString Tracking_SourceNotInTrackingLog => Create(ref field);
    internal static ResourceString Tracking_SourceOutputsNotAvailable => Create(ref field);
    internal static ResourceString Tracking_SourcesAndCorrespondingOutputMismatch => Create(ref field);
    internal static ResourceString Tracking_SourceWillBeCompiled => Create(ref field);
    internal static ResourceString Tracking_SourceWillBeCompiledAsNoTrackingLog => Create(ref field);
    internal static ResourceString Tracking_SourceWillBeCompiledDependencyWasModifiedAt => Create(ref field);
    internal static ResourceString Tracking_SourceWillBeCompiledMissingDependency => Create(ref field);
    internal static ResourceString Tracking_SourceWillBeCompiledOutputDoesNotExist => Create(ref field);
    internal static ResourceString Tracking_TrackingCached => Create(ref field);
    internal static ResourceString Tracking_TrackingLogNotAvailable => Create(ref field);
    internal static ResourceString Tracking_TrackingLogs => Create(ref field);
    internal static ResourceString Tracking_UpToDate => Create(ref field);
    internal static ResourceString Tracking_WriteLogEntryNotFound => Create(ref field);
    internal static ResourceString Tracking_WriteTrackingCached => Create(ref field);
    internal static ResourceString Tracking_WriteTrackingLogs => Create(ref field);
}
