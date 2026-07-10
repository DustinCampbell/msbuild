// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.Utilities;

/// <summary>
///  Describes how <see cref="ProcessLauncher"/> should start a process.
/// </summary>
internal readonly struct ProcessLaunchInfo
{
    /// <summary>
    ///  Gets the executable to launch (for example a native app host, MSBuild.exe, or the dotnet host).
    ///  This becomes <c>lpApplicationName</c> on Windows and <see cref="ProcessStartInfo.FileName"/> elsewhere.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///  Gets the command-line arguments passed after the executable name. On Windows the launcher composes the
    ///  full command line as <c>"FileName" Arguments</c>; on other platforms these become
    ///  <see cref="ProcessStartInfo.Arguments"/>.
    /// </summary>
    public string Arguments { get; init; }

    /// <summary>
    ///  Gets optional environment variable overrides. A non-null value sets or overrides that variable; a null
    ///  value removes it from the child process environment. When null, the child inherits the current
    ///  environment unchanged.
    /// </summary>
    public IDictionary<string, string?>? EnvironmentOverrides { get; init; }

    /// <summary>
    ///  Gets a value indicating whether the child does not inherit the current process's stdin/stdout/stderr
    ///  handles (they are set to null / redirected) and no console window is created. This isolates the
    ///  child's standard handles from the launching process.
    /// </summary>
    public bool DetachStandardHandles { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to create the child process in a new console window (Windows only).
    /// </summary>
    public bool CreateNewConsole { get; init; }

    /// <summary>
    ///  Gets a value indicating whether to start the child with <c>NORMAL_PRIORITY_CLASS</c> explicitly (Windows only).
    /// </summary>
    public bool UseNormalPriorityClass { get; init; }
}
