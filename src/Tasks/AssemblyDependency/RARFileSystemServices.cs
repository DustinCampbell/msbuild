// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.AssemblyDependency;
#if FEATURE_WIN32_REGISTRY
using System.Collections.Generic;
using Microsoft.Win32;
#endif

#nullable disable

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Provides file system and assembly services for ResolveAssemblyReference.
    /// This class encapsulates all I/O operations and assembly metadata retrieval,
    /// enabling tests to override specific operations by inheriting from this class.
    /// </summary>
    internal class RARFileSystemServices
    {
        /// <summary>
        /// Singleton instance for production use.
        /// </summary>
        private static RARFileSystemServices s_instance;

        /// <summary>
        /// Gets the default instance for production use.
        /// </summary>
        internal static RARFileSystemServices Default => s_instance ??= new RARFileSystemServices();

        /// <summary>
        /// Initializes a new instance of the <see cref="RARFileSystemServices"/> class.
        /// Protected constructor to allow inheritance for testing.
        /// </summary>
        protected RARFileSystemServices()
        {
        }

        /// <summary>
        /// Checks whether a file exists at the specified path.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the file exists; otherwise, false.</returns>
        public virtual bool FileExists(string path)
        {
            return FileUtilities.FileExistsNoThrow(path);
        }

        /// <summary>
        /// Checks whether a directory exists at the specified path.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns>True if the directory exists; otherwise, false.</returns>
        public virtual bool DirectoryExists(string path)
        {
            return FileUtilities.DirectoryExistsNoThrow(path);
        }

        /// <summary>
        /// Gets the subdirectories of the specified directory that match the search pattern.
        /// </summary>
        /// <param name="path">The path to search.</param>
        /// <param name="searchPattern">The search pattern to match.</param>
        /// <returns>An array of directory paths, sorted in ordinal order for determinism.</returns>
        public virtual string[] GetDirectories(string path, string searchPattern)
        {
            return FileSystems.Default.EnumerateDirectories(path, searchPattern)
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Gets the assembly name from the specified assembly file.
        /// </summary>
        /// <param name="path">The path to the assembly file.</param>
        /// <returns>The assembly name extension, or null if the file is not a valid assembly.</returns>
        public virtual AssemblyNameExtension GetAssemblyName(string path)
        {
            return AssemblyNameExtension.GetAssemblyNameEx(path);
        }

        /// <summary>
        /// Gets the assembly metadata from the specified assembly file.
        /// </summary>
        /// <param name="path">The path to the assembly file.</param>
        /// <param name="assemblyMetadataCache">A cache of previously retrieved assembly metadata.</param>
        /// <param name="dependencies">Receives the list of dependencies.</param>
        /// <param name="scatterFiles">Receives the list of scatter files.</param>
        /// <param name="frameworkNameAttribute">Receives the target framework name.</param>
        public virtual void GetAssemblyMetadata(
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkNameAttribute)
        {
            AssemblyInformation.GetAssemblyMetadata(path, assemblyMetadataCache, out dependencies, out scatterFiles, out frameworkNameAttribute);
        }

        /// <summary>
        /// Gets the last write time of the specified file.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>The last write time in UTC.</returns>
        public virtual DateTime GetLastWriteTime(string path)
        {
            return NativeMethodsShared.GetLastWriteFileUtcTime(path);
        }

        /// <summary>
        /// Gets the CLR runtime version of the specified assembly.
        /// </summary>
        /// <param name="path">The path to the assembly file.</param>
        /// <returns>The runtime version string.</returns>
        public virtual string GetAssemblyRuntimeVersion(string path)
        {
            return AssemblyInformation.GetRuntimeVersion(path);
        }

        /// <summary>
        /// Determines whether the specified file is a WinMD file.
        /// </summary>
        /// <param name="fullPath">The full path to the file.</param>
        /// <param name="imageRuntimeVersion">Receives the image runtime version if applicable.</param>
        /// <param name="isManagedWinmd">Receives a value indicating whether the file is a managed WinMD.</param>
        /// <returns>True if the file is a WinMD file; otherwise, false.</returns>
        public virtual bool IsWinMDFile(
            string fullPath,
            out string imageRuntimeVersion,
            out bool isManagedWinmd)
        {
            return AssemblyInformation.IsWinMDFile(
                fullPath,
                GetAssemblyRuntimeVersion,
                FileExists,
                out imageRuntimeVersion,
                out isManagedWinmd);
        }

        /// <summary>
        /// Reads the machine type from the PE header of the specified file.
        /// </summary>
        /// <param name="dllPath">The path to the DLL file.</param>
        /// <returns>The machine type value from the PE header.</returns>
        public virtual ushort ReadMachineTypeFromPEHeader(string dllPath)
        {
            return ReferenceTable.ReadMachineTypeFromPEHeader(dllPath);
        }

        /// <summary>
        /// Gets the path to an assembly in the Global Assembly Cache.
        /// </summary>
        /// <param name="assemblyName">The assembly name to look up.</param>
        /// <param name="targetProcessorArchitecture">The target processor architecture.</param>
        /// <param name="targetedRuntimeVersion">The targeted runtime version.</param>
        /// <param name="fullFusionName">Whether to match the full fusion name.</param>
        /// <param name="specificVersion">Whether to match a specific version.</param>
        /// <returns>The path to the assembly in the GAC, or null if not found.</returns>
        public virtual string GetAssemblyPathInGac(
            AssemblyNameExtension assemblyName,
            System.Reflection.ProcessorArchitecture targetProcessorArchitecture,
            Version targetedRuntimeVersion,
            bool fullFusionName,
            bool specificVersion)
        {
#if FEATURE_GAC
            return GlobalAssemblyCache.GetLocation(
                assemblyName,
                targetProcessorArchitecture,
                GetAssemblyRuntimeVersion,
                targetedRuntimeVersion,
                fullFusionName,
                FileExists,
                GlobalAssemblyCache.pathFromFusionName,
                GlobalAssemblyCache.gacEnumerator,
                specificVersion);
#else
            return string.Empty;
#endif
        }

#if FEATURE_WIN32_REGISTRY
        /// <summary>
        /// Opens a base registry key.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="view">The registry view.</param>
        /// <returns>The opened registry key.</returns>
        public virtual RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
        {
            return RegistryHelper.OpenBaseKey(hive, view);
        }

        /// <summary>
        /// Gets the subkey names under the specified registry key.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey path.</param>
        /// <returns>An enumeration of subkey names.</returns>
        public virtual IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey)
        {
            return RegistryHelper.GetSubKeyNames(baseKey, subKey);
        }

        /// <summary>
        /// Gets the default value of the specified registry subkey.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey path.</param>
        /// <returns>The default value of the subkey.</returns>
        public virtual string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey)
        {
            return RegistryHelper.GetDefaultValue(baseKey, subKey);
        }
#endif

        #region Delegate Adapters

        /// <summary>
        /// Creates a FileExists delegate that calls this instance's FileExists method.
        /// </summary>
        internal FileExists CreateFileExistsDelegate() => FileExists;

        /// <summary>
        /// Creates a DirectoryExists delegate that calls this instance's DirectoryExists method.
        /// </summary>
        internal DirectoryExists CreateDirectoryExistsDelegate() => DirectoryExists;

        /// <summary>
        /// Creates a GetDirectories delegate that calls this instance's GetDirectories method.
        /// </summary>
        internal Tasks.GetDirectories CreateGetDirectoriesDelegate() => GetDirectories;

        /// <summary>
        /// Creates a GetAssemblyName delegate that calls this instance's GetAssemblyName method.
        /// </summary>
        internal GetAssemblyName CreateGetAssemblyNameDelegate() => GetAssemblyName;

        /// <summary>
        /// Creates a GetAssemblyMetadata delegate that calls this instance's GetAssemblyMetadata method.
        /// </summary>
        internal GetAssemblyMetadata CreateGetAssemblyMetadataDelegate() => GetAssemblyMetadata;

        /// <summary>
        /// Creates a GetLastWriteTime delegate that calls this instance's GetLastWriteTime method.
        /// </summary>
        internal Tasks.GetLastWriteTime CreateGetLastWriteTimeDelegate() => GetLastWriteTime;

        /// <summary>
        /// Creates a GetAssemblyRuntimeVersion delegate that calls this instance's GetAssemblyRuntimeVersion method.
        /// </summary>
        internal GetAssemblyRuntimeVersion CreateGetAssemblyRuntimeVersionDelegate() => GetAssemblyRuntimeVersion;

        /// <summary>
        /// Creates an IsWinMDFile delegate that calls this instance's IsWinMDFile method.
        /// </summary>
        internal Tasks.IsWinMDFile CreateIsWinMDFileDelegate()
        {
            return (string fullPath, GetAssemblyRuntimeVersion getAssemblyRuntimeVersion, FileExists fileExists, out string imageRuntimeVersion, out bool isManagedWinmd)
                => IsWinMDFile(fullPath, out imageRuntimeVersion, out isManagedWinmd);
        }

        /// <summary>
        /// Creates a ReadMachineTypeFromPEHeader delegate that calls this instance's method.
        /// </summary>
        internal ReadMachineTypeFromPEHeader CreateReadMachineTypeFromPEHeaderDelegate() => ReadMachineTypeFromPEHeader;

        /// <summary>
        /// Creates a GetAssemblyPathInGac delegate that calls this instance's method.
        /// </summary>
        internal GetAssemblyPathInGac CreateGetAssemblyPathInGacDelegate()
        {
            return (AssemblyNameExtension assemblyName, System.Reflection.ProcessorArchitecture targetProcessorArchitecture, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVersion, FileExists fileExists, bool fullFusionName, bool specificVersion)
                => GetAssemblyPathInGac(assemblyName, targetProcessorArchitecture, targetedRuntimeVersion, fullFusionName, specificVersion);
        }

#if FEATURE_WIN32_REGISTRY
        /// <summary>
        /// Creates an OpenBaseKey delegate that calls this instance's method.
        /// </summary>
        internal OpenBaseKey CreateOpenBaseKeyDelegate() => OpenBaseKey;

        /// <summary>
        /// Creates a GetRegistrySubKeyNames delegate that calls this instance's method.
        /// </summary>
        internal Shared.GetRegistrySubKeyNames CreateGetRegistrySubKeyNamesDelegate() => GetRegistrySubKeyNames;

        /// <summary>
        /// Creates a GetRegistrySubKeyDefaultValue delegate that calls this instance's method.
        /// </summary>
        internal Shared.GetRegistrySubKeyDefaultValue CreateGetRegistrySubKeyDefaultValueDelegate() => GetRegistrySubKeyDefaultValue;
#endif

        #endregion
    }

    /// <summary>
    /// A wrapper around RARFileSystemServices that uses cached delegates for I/O operations.
    /// This allows the caching infrastructure in ResolveAssemblyReference to be used
    /// while still providing a services-based API to internal classes.
    /// </summary>
    internal sealed class CachedRARFileSystemServices : RARFileSystemServices
    {
        private readonly FileExists _fileExists;
        private readonly DirectoryExists _directoryExists;
        private readonly Tasks.GetDirectories _getDirectories;
        private readonly GetAssemblyName _getAssemblyName;
        private readonly GetAssemblyMetadata _getAssemblyMetadata;
        private readonly Tasks.GetLastWriteTime _getLastWriteTime;
        private readonly GetAssemblyRuntimeVersion _getRuntimeVersion;
        private readonly Tasks.IsWinMDFile _isWinMDFile;
        private readonly ReadMachineTypeFromPEHeader _readMachineTypeFromPEHeader;
        private readonly GetAssemblyPathInGac _getAssemblyPathInGac;
#if FEATURE_WIN32_REGISTRY
        private readonly OpenBaseKey _openBaseKey;
        private readonly Shared.GetRegistrySubKeyNames _getRegistrySubKeyNames;
        private readonly Shared.GetRegistrySubKeyDefaultValue _getRegistrySubKeyDefaultValue;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="CachedRARFileSystemServices"/> class
        /// with the specified cached delegates.
        /// </summary>
        internal CachedRARFileSystemServices(
            FileExists fileExists,
            DirectoryExists directoryExists,
            Tasks.GetDirectories getDirectories,
            GetAssemblyName getAssemblyName,
            GetAssemblyMetadata getAssemblyMetadata,
            Tasks.GetLastWriteTime getLastWriteTime,
            GetAssemblyRuntimeVersion getRuntimeVersion,
            Tasks.IsWinMDFile isWinMDFile,
            ReadMachineTypeFromPEHeader readMachineTypeFromPEHeader,
#if FEATURE_WIN32_REGISTRY
            GetAssemblyPathInGac getAssemblyPathInGac,
            OpenBaseKey openBaseKey,
            Shared.GetRegistrySubKeyNames getRegistrySubKeyNames,
            Shared.GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue)
#else
            GetAssemblyPathInGac getAssemblyPathInGac)
#endif
        {
            _fileExists = fileExists;
            _directoryExists = directoryExists;
            _getDirectories = getDirectories;
            _getAssemblyName = getAssemblyName;
            _getAssemblyMetadata = getAssemblyMetadata;
            _getLastWriteTime = getLastWriteTime;
            _getRuntimeVersion = getRuntimeVersion;
            _isWinMDFile = isWinMDFile;
            _readMachineTypeFromPEHeader = readMachineTypeFromPEHeader;
            _getAssemblyPathInGac = getAssemblyPathInGac;
#if FEATURE_WIN32_REGISTRY
            _openBaseKey = openBaseKey;
            _getRegistrySubKeyNames = getRegistrySubKeyNames;
            _getRegistrySubKeyDefaultValue = getRegistrySubKeyDefaultValue;
#endif
        }

        /// <inheritdoc />
        public override bool FileExists(string path) => _fileExists(path);

        /// <inheritdoc />
        public override bool DirectoryExists(string path) => _directoryExists(path);

        /// <inheritdoc />
        public override string[] GetDirectories(string path, string searchPattern) => _getDirectories(path, searchPattern);

        /// <inheritdoc />
        public override AssemblyNameExtension GetAssemblyName(string path) => _getAssemblyName(path);

        /// <inheritdoc />
        public override void GetAssemblyMetadata(
            string path,
            ConcurrentDictionary<string, AssemblyMetadata> assemblyMetadataCache,
            out AssemblyNameExtension[] dependencies,
            out string[] scatterFiles,
            out FrameworkName frameworkNameAttribute)
        {
            _getAssemblyMetadata(path, assemblyMetadataCache, out dependencies, out scatterFiles, out frameworkNameAttribute);
        }

        /// <inheritdoc />
        public override DateTime GetLastWriteTime(string path) => _getLastWriteTime(path);

        /// <inheritdoc />
        public override string GetAssemblyRuntimeVersion(string path) => _getRuntimeVersion(path);

        /// <inheritdoc />
        public override bool IsWinMDFile(string fullPath, out string imageRuntimeVersion, out bool isManagedWinmd)
        {
            return _isWinMDFile(fullPath, _getRuntimeVersion, _fileExists, out imageRuntimeVersion, out isManagedWinmd);
        }

        /// <inheritdoc />
        public override ushort ReadMachineTypeFromPEHeader(string dllPath) => _readMachineTypeFromPEHeader(dllPath);

        /// <inheritdoc />
        public override string GetAssemblyPathInGac(
            AssemblyNameExtension assemblyName,
            System.Reflection.ProcessorArchitecture targetProcessorArchitecture,
            Version targetedRuntimeVersion,
            bool fullFusionName,
            bool specificVersion)
        {
            return _getAssemblyPathInGac(assemblyName, targetProcessorArchitecture, _getRuntimeVersion, targetedRuntimeVersion, _fileExists, fullFusionName, specificVersion);
        }

#if FEATURE_WIN32_REGISTRY
        /// <inheritdoc />
        public override RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view) => _openBaseKey(hive, view);

        /// <inheritdoc />
        public override IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey) => _getRegistrySubKeyNames(baseKey, subKey);

        /// <inheritdoc />
        public override string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey) => _getRegistrySubKeyDefaultValue(baseKey, subKey);
#endif
    }
}
