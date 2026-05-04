// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Tasks.AssemblyDependency;
#if FEATURE_WIN32_REGISTRY
using System.Collections.Generic;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
#endif

#nullable disable

namespace Microsoft.Build.Tasks;

/// <summary>
/// Provides file system, assembly, and registry services for ResolveAssemblyReference.
/// This class encapsulates all I/O operations and assembly metadata retrieval,
/// enabling tests to override specific operations by inheriting from this class.
/// </summary>
internal class RARServices
#if FEATURE_WIN32_REGISTRY
    : IRegistryService
#endif
{
    // PE header constants for ReadMachineTypeFromPEHeader
    private const int PEHeaderOffset = 0x3c;
    private const uint PEHeaderSignature = 0x00004550; // "PE\0\0"

    /// <summary>
    /// Singleton instance for production use.
    /// </summary>
    private static RARServices s_instance;

    /// <summary>
    /// Gets the default instance for production use.
    /// </summary>
    internal static RARServices Default => s_instance ??= new RARServices();

    /// <summary>
    /// Initializes a new instance of the <see cref="RARServices"/> class.
    /// Protected constructor to allow inheritance for testing.
    /// </summary>
    protected RARServices()
    {
    }

    /// <summary>
    /// Checks whether a file exists at the specified path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><see langword="true"/> if the file exists; otherwise, <see langword="false"/>.</returns>
    public virtual bool FileExists(string path)
        => FileUtilities.FileExistsNoThrow(path);

    /// <summary>
    /// Checks whether a directory exists at the specified path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns><see langword="true"/> if the directory exists; otherwise, <see langword="false"/>.</returns>
    public virtual bool DirectoryExists(string path)
        => FileUtilities.DirectoryExistsNoThrow(path);

    /// <summary>
    /// Gets the subdirectories of the specified directory that match the search pattern.
    /// </summary>
    /// <param name="path">The path to search.</param>
    /// <param name="searchPattern">The search pattern to match.</param>
    /// <returns>An array of directory paths, sorted in ordinal order for determinism.</returns>
    public virtual string[] GetDirectories(string path, string searchPattern)
        => FileSystems.Default.EnumerateDirectories(path, searchPattern)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Gets the assembly name from the specified assembly file.
    /// </summary>
    /// <param name="path">The path to the assembly file.</param>
    /// <returns>The assembly name extension, or <see langword="null"/> if the file is not a valid assembly.</returns>
    public virtual AssemblyNameExtension GetAssemblyName(string path)
        => AssemblyNameExtension.GetAssemblyNameEx(path);

    /// <summary>
    /// Gets the assembly metadata from the specified assembly file.
    /// </summary>
    /// <param name="path">The path to the assembly file.</param>
    /// <returns>The <see cref="AssemblyMetadata"/> for the assembly.</returns>
    public virtual AssemblyMetadata GetAssemblyMetadata(string path)
        => new AssemblyMetadata(path);

    /// <summary>
    /// Gets the last write time of the specified file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>The last write time in UTC.</returns>
    public virtual DateTime GetLastWriteTime(string path)
        => NativeMethodsShared.GetLastWriteFileUtcTime(path);

    /// <summary>
    /// Gets the CLR runtime version of the specified assembly.
    /// </summary>
    /// <param name="path">The path to the assembly file.</param>
    /// <returns>The runtime version string.</returns>
    public virtual string GetAssemblyRuntimeVersion(string path)
        => AssemblyInformation.GetRuntimeVersion(path);

    /// <summary>
    /// Determines whether the specified file is a WinMD file.
    /// </summary>
    /// <param name="fullPath">The full path to the file.</param>
    /// <param name="imageRuntimeVersion">Receives the image runtime version if applicable.</param>
    /// <param name="isManagedWinmd">Receives a value indicating whether the file is a managed WinMD.</param>
    /// <returns><see langword="true"/> if the file is a WinMD file; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// A WinMD file is identified by having "WindowsRuntime" in its image runtime version.
    /// A managed WinMD also contains "CLR" in the runtime version string.
    /// </remarks>
    public virtual bool IsWinMDFile(
        string fullPath,
        out string imageRuntimeVersion,
        out bool isManagedWinmd)
    {
        imageRuntimeVersion = String.Empty;
        isManagedWinmd = false;

        if (!NativeMethodsShared.IsWindows)
        {
            return false;
        }

        // May be null or empty if the file was never resolved to a path on disk.
        if (!String.IsNullOrEmpty(fullPath) && FileExists(fullPath))
        {
            imageRuntimeVersion = GetAssemblyRuntimeVersion(fullPath);
            if (!String.IsNullOrEmpty(imageRuntimeVersion))
            {
                bool containsWindowsRuntime = imageRuntimeVersion.IndexOf(
                    "WindowsRuntime",
                    StringComparison.OrdinalIgnoreCase) >= 0;

                if (containsWindowsRuntime)
                {
                    isManagedWinmd = imageRuntimeVersion.IndexOf("CLR", StringComparison.OrdinalIgnoreCase) >= 0;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the machine type from the PE header of the specified file.
    /// </summary>
    /// <param name="dllPath">The path to the DLL file.</param>
    /// <returns>The machine type value from the PE header.</returns>
    /// <remarks>
    /// PE header layout:
    /// - At offset 0x3c is the file offset to the PE signature
    /// - The PE signature is "PE\0\0" (0x00004550)
    /// - After the signature is the COFF header where the first 2 bytes are the machine type
    /// 
    /// Machine type values:
    /// - IMAGE_FILE_MACHINE_UNKNOWN (0x0): Any machine type
    /// - IMAGE_FILE_MACHINE_AMD64 (0x8664): x64
    /// - IMAGE_FILE_MACHINE_ARM (0x1c0): ARM little endian
    /// - IMAGE_FILE_MACHINE_I386 (0x14c): Intel 386 or later
    /// - IMAGE_FILE_MACHINE_IA64 (0x200): Intel Itanium
    /// </remarks>
    public virtual ushort ReadMachineTypeFromPEHeader(string dllPath)
    {
        ushort machineType = NativeMethods.IMAGE_FILE_MACHINE_INVALID;
        using (FileStream stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
        {
            stream.Seek(PEHeaderOffset, SeekOrigin.Begin);

            using (BinaryReader reader = new BinaryReader(stream))
            {
                int peOffset = reader.ReadInt32();
                stream.Seek(peOffset, SeekOrigin.Begin);

                uint peSignature = reader.ReadUInt32();
                if (peSignature == PEHeaderSignature)
                {
                    machineType = reader.ReadUInt16();
                }
            }
        }

        return machineType;
    }

    /// <summary>
    /// Sets up callbacks for handling immutable (SDK) files.
    /// Override in subclasses that need to optimize handling of SDK files.
    /// </summary>
    /// <param name="isImmutableFile">Callback to check if a file is immutable.</param>
    /// <param name="getImmutableFileAssemblyName">Callback to get assembly name for immutable files.</param>
    public virtual void SetImmutableFileCallbacks(
        Func<string, bool> isImmutableFile,
        Func<string, AssemblyNameExtension> getImmutableFileAssemblyName)
    {
        // Default implementation does nothing - subclasses can override to use these callbacks
    }

    /// <summary>
    /// Gets the path to an assembly in the Global Assembly Cache.
    /// </summary>
    /// <param name="assemblyName">The assembly name to look up.</param>
    /// <param name="targetProcessorArchitecture">The target processor architecture.</param>
    /// <param name="targetedRuntimeVersion">The targeted runtime version.</param>
    /// <param name="fullFusionName">Whether to match the full fusion name.</param>
    /// <param name="specificVersion">Whether to match a specific version.</param>
    /// <returns>The path to the assembly in the GAC, or <see langword="null"/> if not found.</returns>
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
        => RegistryService.Instance.OpenBaseKey(hive, view);

    /// <summary>
    /// Gets the subkey names under the specified registry key.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The subkey path.</param>
    /// <returns>An enumeration of subkey names.</returns>
    public virtual IEnumerable<string> GetSubKeyNames(RegistryKey baseKey, string subKey)
        => RegistryService.Instance.GetSubKeyNames(baseKey, subKey);

    /// <summary>
    /// Gets the default value of the specified registry subkey.
    /// </summary>
    /// <param name="baseKey">The base registry key.</param>
    /// <param name="subKey">The subkey path.</param>
    /// <returns>The default value of the subkey, or <see langword="null"/> if not found.</returns>
    public virtual string GetDefaultValue(RegistryKey baseKey, string subKey)
        => RegistryService.Instance.GetDefaultValue(baseKey, subKey);
#endif
}
