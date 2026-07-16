// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BackEnd;

/// <summary>
///  Build-engine-only compatibility shim used by the shared-source task-logging files
///  (<c>TaskLoggingHelper</c>, <c>TaskLoggingHelperExtension</c>, <c>TaskParameter</c>) when they
///  are compiled into Microsoft.Build. It maps the small set of legacy resource-name-based
///  <c>ErrorUtilities</c> throw helpers onto the new <see cref="Framework.ResourceString"/>-based helpers,
///  resolving the resource names against Microsoft.Build's <see cref="SR"/> catalog.
/// </summary>
/// <remarks>
///  Aliased as <c>ErrorUtilities</c> under <c>#if BUILD_ENGINE</c> so those shared files need no
///  per-call-site changes; the public Microsoft.Build.Utilities/Tasks copies of the same files are
///  compiled without <c>BUILD_ENGINE</c> and continue to use the real shared <c>ErrorUtilities</c>.
/// </remarks>
internal static class TaskEngineErrorUtilities
{
    public static void VerifyThrowInvalidOperation(bool condition, string resourceName, params object?[]? args)
        => InvalidOperationException.ThrowIfFalse(condition, SR.Resource(resourceName), args);

    public static void ThrowInvalidOperation(string resourceName, params object?[]? args)
        => InvalidOperationException.Throw(SR.Resource(resourceName), args);

    public static void VerifyThrowArgument(bool condition, string resourceName, params object?[]? args)
        => ArgumentException.ThrowIfFalse(condition, SR.Resource(resourceName), args);
}
