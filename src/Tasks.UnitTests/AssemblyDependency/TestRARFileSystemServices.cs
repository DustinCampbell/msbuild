// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
#if FEATURE_WIN32_REGISTRY
using System.Collections.Generic;
using Microsoft.Win32;
#endif

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Test implementation of RARFileSystemServices that delegates to
    /// the static mock delegates in ResolveAssemblyReferenceTestFixture.
    /// </summary>
    internal sealed class TestRARFileSystemServices : RARFileSystemServices
    {
        /// <summary>
        /// Singleton test instance.
        /// </summary>
        internal static TestRARFileSystemServices Instance { get; } = new TestRARFileSystemServices();

        /// <summary>
        /// Private constructor.
        /// </summary>
        private TestRARFileSystemServices()
        {
        }

        /// <inheritdoc/>
        public override bool FileExists(string path)
        {
            return ResolveAssemblyReferenceTestFixture.fileExists(path);
        }

        /// <inheritdoc/>
        public override bool DirectoryExists(string path)
        {
            return ResolveAssemblyReferenceTestFixture.directoryExists(path);
        }

        /// <inheritdoc/>
        public override string[] GetDirectories(string path, string searchPattern)
        {
            return ResolveAssemblyReferenceTestFixture.getDirectories(path, searchPattern);
        }

        /// <inheritdoc/>
        public override AssemblyNameExtension GetAssemblyName(string path)
        {
            return ResolveAssemblyReferenceTestFixture.getAssemblyName(path);
        }

        /// <inheritdoc/>
        public override void GetAssemblyMetadata(
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkNameAttribute)
        {
            ResolveAssemblyReferenceTestFixture.getAssemblyMetadata(path, assemblyMetadataCache, out dependencies, out scatterFiles, out frameworkNameAttribute);
        }

        /// <inheritdoc/>
        public override DateTime GetLastWriteTime(string path)
        {
            return ResolveAssemblyReferenceTestFixture.getLastWriteTime(path);
        }

        /// <inheritdoc/>
        public override string GetAssemblyRuntimeVersion(string path)
        {
            return ResolveAssemblyReferenceTestFixture.getRuntimeVersion(path);
        }

        /// <inheritdoc/>
        public override bool IsWinMDFile(
            string fullPath,
            out string imageRuntimeVersion,
            out bool isManagedWinmd)
        {
            return ResolveAssemblyReferenceTestFixture.isWinMDFile(fullPath, GetAssemblyRuntimeVersion, FileExists, out imageRuntimeVersion, out isManagedWinmd);
        }

        /// <inheritdoc/>
        public override ushort ReadMachineTypeFromPEHeader(string dllPath)
        {
            return ResolveAssemblyReferenceTestFixture.readMachineTypeFromPEHeader(dllPath);
        }

        /// <inheritdoc/>
        public override string GetAssemblyPathInGac(
            AssemblyNameExtension assemblyName,
            System.Reflection.ProcessorArchitecture targetProcessorArchitecture,
            Version targetedRuntimeVersion,
            bool fullFusionName,
            bool specificVersion)
        {
            return ResolveAssemblyReferenceTestFixture.checkIfAssemblyIsInGac(
                assemblyName,
                targetProcessorArchitecture,
                GetAssemblyRuntimeVersion,
                targetedRuntimeVersion,
                FileExists,
                fullFusionName,
                specificVersion);
        }

#if FEATURE_WIN32_REGISTRY
        /// <inheritdoc/>
        public override RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
        {
            return ResolveAssemblyReferenceTestFixture.openBaseKey(hive, view);
        }

        /// <inheritdoc/>
        public override IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey)
        {
            return ResolveAssemblyReferenceTestFixture.getRegistrySubKeyNames(baseKey, subKey);
        }

        /// <inheritdoc/>
        public override string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey)
        {
            return ResolveAssemblyReferenceTestFixture.getRegistrySubKeyDefaultValue(baseKey, subKey);
        }
#endif
    }
}
