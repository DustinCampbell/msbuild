// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;

#if RUNTIME_TYPE_NETCORE
using System.IO;
#endif

using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal sealed class NodeLauncher : INodeLauncher, IBuildComponent
    {
        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ArgumentOutOfRangeException.ThrowIfNotEqual(type, BuildComponentType.NodeLauncher);
            return new NodeLauncher();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
        }

        public void ShutdownComponent()
        {
        }

        /// <summary>
        /// Creates a new MSBuild process using the specified launch configuration.
        /// </summary>
        public Process Start(NodeLaunchData launchData, int nodeId)
        {
            // Disable MSBuild server for a child process.
            // In case of starting msbuild server it prevents an infinite recursion. In case of starting msbuild node we also do not want this variable to be set.
            return DisableMSBuildServer(() => StartInternal(launchData));
        }

        /// <summary>
        /// Creates new MSBuild or dotnet process.
        /// </summary>
        private Process StartInternal(NodeLaunchData nodeLaunchData)
        {
            ValidateMSBuildLocation(nodeLaunchData.MSBuildLocation);

            string exeName = ResolveExecutableName(nodeLaunchData.MSBuildLocation, out bool isNativeAppHost);
            bool ensureStdOut = Traits.Instance.EscapeHatches.EnsureStdOutForChildNodesIsPrimaryStdout;
            bool showNodeWindow = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW"));

            // The MSBuild path is passed as the first "real" argument because the MSBuild argument parser
            // expects it and will otherwise skip the first argument. On .NET Core the dotnet host runs the
            // managed assembly, so the assembly path is passed as an argument; native app hosts run directly.
            string arguments =
#if RUNTIME_TYPE_NETCORE
                isNativeAppHost ? nodeLaunchData.CommandLineArgs : $"\"{nodeLaunchData.MSBuildLocation}\" {nodeLaunchData.CommandLineArgs}";
#else
                nodeLaunchData.CommandLineArgs;
#endif

            var launchInfo = new ProcessLaunchInfo
            {
                FileName = exeName,
                Arguments = arguments,
                EnvironmentOverrides = nodeLaunchData.EnvironmentOverrides,

                // Detaching the standard handles (so the child does not inherit ours) is only appropriate when
                // we are neither ensuring stdout flows through nor showing a dedicated node window.
                DetachStandardHandles = !ensureStdOut && !showNodeWindow,
                CreateNewConsole = showNodeWindow,
                UseNormalPriorityClass = ensureStdOut,
            };

            CommunicationsUtilities.Trace($"Launching node from {nodeLaunchData.MSBuildLocation}");

            try
            {
                return ProcessLauncher.Start(launchInfo);
            }
            catch (Exception ex) when (ex is not PlatformNotSupportedException)
            {
                CommunicationsUtilities.Trace(
                    $"Failed to launch node from {nodeLaunchData.MSBuildLocation}. CommandLine: {arguments}{Environment.NewLine}{ex}");

                throw ex is System.ComponentModel.Win32Exception win32
                    ? new NodeFailedToLaunchException(win32.NativeErrorCode.ToString(CultureInfo.InvariantCulture), win32.Message)
                    : new NodeFailedToLaunchException(ex);
            }

            static void ValidateMSBuildLocation(string msbuildLocation)
            {
                // Should always have been set already.
                Assumed.NotNullOrEmpty(msbuildLocation);

                if (!FileSystems.Default.FileExists(msbuildLocation))
                {
                    throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotFindMSBuildExe", msbuildLocation));
                }
            }
        }

        private string ResolveExecutableName(string msbuildLocation, out bool isNativeAppHost)
        {
            isNativeAppHost = false;

#if RUNTIME_TYPE_NETCORE
            string fileName = Path.GetFileName(msbuildLocation);

            // Only managed assemblies (.dll) need dotnet.exe as a host.
            // All native executables — MSBuild app host, MSBuildTaskHost.exe, etc. — run directly.
            if (fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return CurrentHost.GetCurrentHost();
            }

            // Any .exe or extensionless binary (Linux app host) is a native executable.
            isNativeAppHost = true;
#endif
            return msbuildLocation;
        }

        private static Process DisableMSBuildServer(Func<Process> func)
        {
            string useMSBuildServerEnvVarValue = Environment.GetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName);
            try
            {
                if (useMSBuildServerEnvVarValue is not null)
                {
                    Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "0");
                }
                return func();
            }
            finally
            {
                if (useMSBuildServerEnvVarValue is not null)
                {
                    Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, useMSBuildServerEnvVarValue);
                }
            }
        }
    }
}
