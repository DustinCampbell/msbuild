// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

#if FEATURE_WINDOWSINTEROP
using System.Globalization;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
#endif

namespace Microsoft.Build.Utilities;

/// <summary>
///  Launches processes with precise control over how the child inherits (or does not inherit) the
///  launching process's standard handles.
/// </summary>
/// <remarks>
///  The primary use case is launching a process whose standard handles are detached from the current
///  process so that the child does not keep the parent's console alive and is unaffected by the parent
///  exiting. On Windows this is achieved with <c>CreateProcess(bInheritHandles = false)</c> and a
///  <see cref="STARTUPINFOW"/> whose standard handles are set to <see cref="HANDLE.INVALID_HANDLE_VALUE"/>); on
///  other platforms the standard streams are redirected instead.
/// </remarks>
internal static class ProcessLauncher
{
    /// <summary>
    ///  Starts a process using the specified launch configuration.
    /// </summary>
    /// <param name="launchInfo">The configuration describing the process to launch.</param>
    /// <returns>
    ///  The started <see cref="Process"/>.
    /// </returns>
    /// <exception cref="System.ComponentModel.Win32Exception">The process could not be started.</exception>
    public static Process Start(ProcessLaunchInfo launchInfo)
    {
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows)
        {
            return StartProcessWindows(launchInfo);
        }
#endif
        return NativeMethods.IsUnixLike
            ? StartProcessUnix(launchInfo)
            : throw new PlatformNotSupportedException();
    }

    [UnsupportedOSPlatform("windows")]
    private static Process StartProcessUnix(ProcessLaunchInfo launchInfo)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = launchInfo.FileName,
            Arguments = launchInfo.Arguments,
            UseShellExecute = false,
            RedirectStandardInput = launchInfo.DetachStandardHandles,
            RedirectStandardOutput = launchInfo.DetachStandardHandles,
            RedirectStandardError = launchInfo.DetachStandardHandles,
            CreateNoWindow = launchInfo.DetachStandardHandles,
        };

        // ProcessStartInfo.Environment is IDictionary<string, string?> on .NET and IDictionary<string, string>
        // on .NET Framework; bridge that difference with an oblivious nullability context.
#nullable disable
        DotnetHostEnvironmentHelper.ApplyEnvironmentOverrides(processStartInfo.Environment, launchInfo.EnvironmentOverrides);
#nullable restore

        Process process = Process.Start(processStartInfo)!;
        CommunicationsUtilities.Trace($"Successfully launched process '{launchInfo.FileName}' with PID {process.Id}");
        return process;
    }

#if FEATURE_WINDOWSINTEROP
    [SupportedOSPlatform("windows6.1")]
    private static unsafe Process StartProcessWindows(ProcessLaunchInfo launchInfo)
    {
        PROCESS_CREATION_FLAGS creationFlags = 0;
        if (launchInfo.UseNormalPriorityClass)
        {
            creationFlags |= PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS;
        }

        if (launchInfo.CreateNewConsole)
        {
            creationFlags |= PROCESS_CREATION_FLAGS.CREATE_NEW_CONSOLE;
        }
        else if (launchInfo.DetachStandardHandles)
        {
            creationFlags |= PROCESS_CREATION_FLAGS.CREATE_NO_WINDOW;
        }

        STARTUPINFOW startInfo = CreateStartupInfo(launchInfo.DetachStandardHandles);

        string fileName = launchInfo.FileName;

        // CreateProcessW requires a writable PWSTR for lpCommandLine. Build it into a ValueStringBuilder
        // we can pin directly. The executable name is repeated as the first token of the command line
        // because CreateProcess treats lpCommandLine as the full argument vector (including argv[0]).
        ValueStringBuilder commandLine = new(stackalloc char[256]);
        ValueStringBuilder environmentBlock = new(stackalloc char[512]);
        try
        {
            commandLine.Append('"');
            commandLine.Append(fileName);
            commandLine.Append('"');
            if (!string.IsNullOrEmpty(launchInfo.Arguments))
            {
                commandLine.Append(' ');
                commandLine.Append(launchInfo.Arguments);
            }

            bool hasEnvironmentBlock = BuildEnvironmentBlock(ref environmentBlock, launchInfo.EnvironmentOverrides);

            // When passing a Unicode environment block, we must set CREATE_UNICODE_ENVIRONMENT.
            // Without this flag, CreateProcess interprets the block as ANSI, causing error 87.
            if (hasEnvironmentBlock)
            {
                creationFlags |= PROCESS_CREATION_FLAGS.CREATE_UNICODE_ENVIRONMENT;
            }

            PROCESS_INFORMATION processInfo;
            BOOL result;
            fixed (char* pCommandLine = commandLine)
            {
                fixed (char* pFileName = fileName)
                {
                    fixed (char* pEnvironmentBlock = environmentBlock)
                    {
                        // Note: CreateProcess is documented to be allowed to modify lpCommandLine in-place
                        // (it may insert a null terminator to split the exe from the args). The buffer must
                        // not be read after a successful call. We only read commandLine again on failure
                        // (for tracing), where in practice the OS does not mutate the buffer.
                        result = PInvoke.CreateProcess(
                            lpApplicationName: pFileName,
                            lpCommandLine: pCommandLine,
                            lpProcessAttributes: null,
                            lpThreadAttributes: null,
                            bInheritHandles: false,
                            dwCreationFlags: creationFlags,
                            lpEnvironment: hasEnvironmentBlock ? pEnvironmentBlock : null,
                            lpCurrentDirectory: (PCWSTR)null,
                            lpStartupInfo: &startInfo,
                            lpProcessInformation: &processInfo);
                    }
                }
            }

            if (!result)
            {
                var e = new System.ComponentModel.Win32Exception();

                string commandLineForTrace = commandLine.ToString();
                CommunicationsUtilities.Trace(
                    $"Failed to launch process '{fileName}'. System32 Error code {e.NativeErrorCode.ToString(CultureInfo.InvariantCulture)}. Description {e.Message}. CommandLine: {commandLineForTrace}");

                throw e;
            }

            CloseProcessHandles(processInfo);

            CommunicationsUtilities.Trace($"Successfully launched process '{fileName}' with PID {(int)processInfo.dwProcessId}");
            return Process.GetProcessById((int)processInfo.dwProcessId);
        }
        finally
        {
            commandLine.Dispose();
            environmentBlock.Dispose();
        }

        static void CloseProcessHandles(PROCESS_INFORMATION processInfo)
        {
#pragma warning disable CA1416 // static local functions don't inherit [SupportedOSPlatform] (analyzer limitation)
            if (processInfo.hProcess != HANDLE.Null && processInfo.hProcess != HANDLE.INVALID_HANDLE_VALUE)
            {
                PInvoke.CloseHandle(processInfo.hProcess);
            }

            if (processInfo.hThread != HANDLE.Null && processInfo.hThread != HANDLE.INVALID_HANDLE_VALUE)
            {
                PInvoke.CloseHandle(processInfo.hThread);
            }
#pragma warning restore CA1416
        }
    }

    [SupportedOSPlatform("windows6.1")]
    private static STARTUPINFOW CreateStartupInfo(bool detachStandardHandles)
    {
        var startInfo = new STARTUPINFOW
        {
            cb = (uint)Marshal.SizeOf<STARTUPINFOW>(),
        };

        if (detachStandardHandles)
        {
            startInfo.hStdError = HANDLE.INVALID_HANDLE_VALUE;
            startInfo.hStdInput = HANDLE.INVALID_HANDLE_VALUE;
            startInfo.hStdOutput = HANDLE.INVALID_HANDLE_VALUE;
            startInfo.dwFlags = STARTUPINFOW_FLAGS.STARTF_USESTDHANDLES;
        }

        return startInfo;
    }

    /// <summary>
    ///  Builds a Windows environment block for CreateProcess into the supplied builder.
    /// </summary>
    /// <param name="builder">Builder that receives the null-separated, double-null-terminated environment block.</param>
    /// <param name="environmentOverrides">Environment variable overrides. Null values remove variables.</param>
    /// <returns>
    ///  <see langword="true"/> if a block was written; <see langword="false"/> when no overrides were supplied
    ///  (caller passes the inherited environment).
    /// </returns>
    [SupportedOSPlatform("windows")]
    private static bool BuildEnvironmentBlock(ref ValueStringBuilder builder, IDictionary<string, string?>? environmentOverrides)
    {
        if (environmentOverrides == null || environmentOverrides.Count == 0)
        {
            return false;
        }

        var environment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = (string)entry.Value!;
        }

        DotnetHostEnvironmentHelper.ApplyEnvironmentOverrides(environment, environmentOverrides);

        // Build the environment block: "key=value\0key=value\0\0"
        // Windows CreateProcess requires the environment block to be sorted alphabetically by name (case-insensitive).
        var sortedKeys = new List<string>(environment.Keys);
        sortedKeys.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (string key in sortedKeys)
        {
            builder.Append(key);
            builder.Append('=');
            builder.Append(environment[key]);
            builder.Append('\0');
        }

        builder.Append('\0');

        return true;
    }
#endif
}
