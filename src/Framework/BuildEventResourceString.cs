// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Framework;

/// <summary>
///  Identifies resources used to lazily format build event messages.
/// </summary>
internal enum BuildEventResourceString
{
    ProjectFinishedFailure,
    ProjectFinishedSuccess,
    ProjectStartedPrefixForTopLevelProjectWithDefaultTargets,
    ProjectStartedPrefixForTopLevelProjectWithTargetNames,
    PropertyAssignment,
    PropertyReassignment,
    SkipTargetBecauseOutputsUpToDate,
    TargetAlreadyCompleteFailure,
    TargetAlreadyCompleteSuccess,
    TargetFinishedFailure,
    TargetFinishedSuccess,
    TargetSkippedFalseCondition,
    TargetStartedFileProjectDepends,
    TargetStartedFileProjectEntry,
    TargetStartedProjectDepends,
    TargetStartedProjectEntry,
    TaskAssemblyLoaded,
    TaskAssemblyLoadedWithAssemblyLoadContext,
    TaskFinishedFailure,
    TaskFinishedSuccess,
    TaskStarted,
    UninitializedPropertyRead,
}
