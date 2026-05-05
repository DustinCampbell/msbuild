// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Win32;
using Xunit;
using static Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.TestData;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public partial class ResolveAssemblyReferenceTestFixture
{
    /// <summary>
    ///  A test implementation of RARServices that calls the fixture's static methods.
    ///  For tests needing custom behavior, pass Func overrides to the constructor.
    /// </summary>
    internal sealed class TestRARServices : RARServices
    {
        /// <summary>
        /// Default test services instance that calls the fixture's static methods.
        /// Most tests use this. Tests needing custom behavior create their own TestRARServices with overrides.
        /// </summary>
        public static new readonly TestRARServices Default = new(fileExists: null, getAssemblyName: null, existentFiles: []);

        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, AssemblyNameExtension> _getAssemblyName;
        private readonly ImmutableArray<string> _existentFiles;

        private TestRARServices(
            Func<string, bool> fileExists,
            Func<string, AssemblyNameExtension> getAssemblyName,
            ImmutableArray<string> existentFiles)
        {
            _fileExists = fileExists;
            _getAssemblyName = getAssemblyName;
            _existentFiles = existentFiles.IsDefault ? [] : existentFiles;
        }

        public static TestRARServices CreateDefault()
            => new(fileExists: null, getAssemblyName: null, ExistentFiles);

        public TestRARServices WithFileExists(Func<string, bool> fileExists)
            => new(fileExists, _getAssemblyName, _existentFiles);

        public TestRARServices WithGetAssemblyName(Func<string, AssemblyNameExtension> getAssemblyName)
            => new(_fileExists, getAssemblyName, _existentFiles);

        public TestRARServices AddExistentFiles(params ReadOnlySpan<string> paths)
            => new(_fileExists, _getAssemblyName, _existentFiles.AddRange(paths));

        public override bool FileExists(string path)
        {
            if (_fileExists is not null)
            {
                return _fileExists(path);
            }

            // For very long paths, File.Exists just returns false
            if (path.Length > 240)
            {
                return false;
            }

            // Do a real File.Exists to make it throw exceptions for illegal paths.
            if (File.Exists(path) && useFrameworkFileExists)
            {
                return true;
            }

            // Do IO monitoring if needed.
            if (uniqueFileExists != null)
            {
                if (!uniqueFileExists.TryGetValue(path, out int value))
                {
                    value = 0;
                    uniqueFileExists[path] = value;
                }

                uniqueFileExists[path] = ++value;
            }

            // First, MyMissingAssembly doesn't exist anywhere.
            if (path.Contains("MyMissingAssembly", StringComparison.Ordinal))
            {
                return false;
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            foreach (string file in _existentFiles)
            {
                if (string.Equals(path, file, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Everything else doesn't exist.
            return false;
        }

        public override bool DirectoryExists(string path)
            => ExistentDirs.Contains(path);

        public override string[] GetDirectories(string path, string searchPattern)
        {
            if (path.EndsWith(MyVersion20Path, StringComparison.Ordinal))
            {
                return [Path.Combine(path, "en"), Path.Combine(path, "en-GB"), Path.Combine(path, "xx")];
            }
            else if (string.Equals(path, @".", StringComparison.OrdinalIgnoreCase))
            {
                // Pretend the current directory has a few subfolders.
                return [Path.Combine(path, "en"), Path.Combine(path, "en-GB"), Path.Combine(path, "xx")];
            }

            return [];
        }

        public override AssemblyNameExtension GetAssemblyName(string path)
            => _getAssemblyName is not null
                ? _getAssemblyName(path)
                : GetAssemblyNameCore(path);

        /// <summary>
        /// Given a path, return the corresponding AssemblyName.
        /// </summary>
        /// <param name="path">Path to the assembly.</param>
        /// <returns>The assembly name.</returns>
        private static AssemblyNameExtension GetAssemblyNameCore(string path)
        {
            // Do IO monitoring if needed.
            if (uniqueGetAssemblyName != null)
            {
                uniqueGetAssemblyName[path] = uniqueGetAssemblyName.TryGetValue(path, out int value)
                    ? value + 1
                    : 0;
            }

            // For very long paths, GetAssemblyName throws an exception.
            if (path.Length > 240)
            {
                throw new FileNotFoundException(path);
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.GetFullPath(path);
            }

            // Category A: Exception paths
            if (string.Equals(path, @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll", StringComparison.OrdinalIgnoreCase))
            {
                // An older LKG of the CLR could throw a FileLoadException if it doesn't recognize the assembly.
                throw new FileLoadException($"Could not load {path}");
            }

            if (string.Equals(path, Path.Combine(MyVersion20Path, "BadImage.dll"), StringComparison.OrdinalIgnoreCase))
            {
                throw new BadImageFormatException($"The format of the file '{Path.Combine(MyVersion20Path, "BadImage.dll")}' is invalid");
            }

            if (string.Equals(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase))
            {
                // Simulate an assembly that throws an UnauthorizedAccessException upon access.
                throw new UnauthorizedAccessException();
            }

            if (string.Equals(path, UnifyMeDll_V05Path, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException();
            }

            if (path.Contains("MyMissingAssembly"))
            {
                throw new FileNotFoundException(path);
            }

            // Category B: Null returns
            if (AssemblyNameNullPaths.Contains(path))
            {
                return null;
            }

            // Category C: isSimpleName=true (special constructor)
            if (string.Equals(path, @"c:\MyEscapedName\=A=.dll", StringComparison.OrdinalIgnoreCase))
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("\\=A\\=, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089", true);
            }

            if (string.Equals(path, @"c:\MyEscapedName\__'ASP'dw0024ry.dll", StringComparison.OrdinalIgnoreCase))
            {
                // Notice the metadata assembly name does not match the base file name.
                return new AssemblyNameExtension("__\\'ASP\\'dw0024ry", true);
            }

            // Category D: EndsWith patterns (case-sensitive suffix matching)
            if (path.EndsWith(Path.Combine(MyVersion20Path, "MyGacAssembly.dll"), StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("MyGacAssembly, Version=9.2.3401.1, Culture=neutral, PublicKeyToken=a6694b450823df78");
            }

            if (path.EndsWith(MyLibraries_V1_DDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(@"c:\RogueLibraries\v1\D.dll", StringComparison.Ordinal))
            {
                // Version 1 of D, but with a different PKT
                return new AssemblyNameExtension("D, VERsion=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb");
            }

            if (path.EndsWith(MyLibraries_V1_E_EDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("E, Version=0.0.0.0, Culture=neutral, PUBlicKeyToken=null");
            }

            if (path.EndsWith(MyLibraries_V2_DDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("D, VErsion=2.0.0.0, CulturE=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(MyLibraries_V1_GDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("G, Version=1.0.0.0, Culture=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(MyLibraries_V2_GDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("G, Version=2.0.0.0, Culture=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            // Category E: Dictionary lookup
            if (AssemblyNames.TryGetValue(path, out string assemblyName))
            {
                return new AssemblyNameExtension(assemblyName);
            }

            // Default fallback: construct name from filename
            string defaultName = $"{Path.GetFileNameWithoutExtension(path)}, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral";
            return new AssemblyNameExtension(defaultName);
        }

        public override AssemblyMetadata GetAssemblyMetadata(string path)
        {
            AssemblyNameExtension[] dependencies = GetDependencies(path);
            FrameworkName frameworkName = GetTargetFrameworkAttribute(path);
            string[] scatterFiles = path == @"C:\Regress275161\a.dll"
                ? ["m1.netmodule", "m2.netmodule"]
                : null;

            return new AssemblyMetadata(dependencies, scatterFiles, frameworkName);
        }

        /// <summary>
        /// Given an assembly, with optional assemblyName return all of the dependent assemblies.
        /// </summary>
        /// <param name="path">The full path to the parent assembly.</param>
        /// <returns>The array of dependent assembly names.</returns>
        private static AssemblyNameExtension[] GetDependencies(string path)
        {
            // Exception paths
            if (string.Equals(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException();
            }

            if (string.Equals(path, MyMissingAssemblyAbsPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException(path);
            }

            // Empty dependency paths
            if (DependenciesEmptyPaths.Contains(path))
            {
                return [];
            }

            // StartsWith pattern for FakeSDK
            if (path.StartsWith(@"C:\FakeSDK\", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            // Dynamic dependency calls (call GetAssemblyName)
            if (string.Equals(path, PortableDllPath, StringComparison.OrdinalIgnoreCase))
            {
                // Simulate a portable assembly with a reference to System.Runtime
                return [GetAssemblyNameCore(SystemRuntimeDllPath)];
            }

            if (string.Equals(path, NetstandardLibraryDllPath, StringComparison.OrdinalIgnoreCase))
            {
                // Simulate a .NET Standard assembly
                return [GetAssemblyNameCore(NetstandardDllPath)];
            }

            if (string.Equals(path, @"C:\DirectoryTest\A.dll", StringComparison.OrdinalIgnoreCase))
            {
                return [GetAssemblyNameCore(@"C:\DirectoryTest\B.dll")];
            }

            // Multiple dependency lookup
            if (MultipleDependencies.TryGetValue(path, out string[] multiDeps))
            {
                return [.. multiDeps.Select(x => new AssemblyNameExtension(x))];
            }

            // Single dependency lookup
            if (SingleDependencies.TryGetValue(path, out string singleDep))
            {
                return [new AssemblyNameExtension(singleDep)];
            }

            // Default dependencies
            return
            [
                new AssemblyNameExtension("SysTem, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77A5c561934e089"),
                new AssemblyNameExtension("msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089")
            ];
        }

        private static FrameworkName GetTargetFrameworkAttribute(string path)
            => TargetFrameworks.TryGetValue(path, out FrameworkName result)
                ? result
                : null;

        public override DateTime GetLastWriteTime(string path)
            => FileExists(path)
                ? DateTime.FromFileTimeUtc(1)
                : DateTime.FromFileTimeUtc(0);

        public override string GetAssemblyRuntimeVersion(string path)
        {
            // Check explicit path mappings first
            if (RuntimeVersions.TryGetValue(path, out string version))
            {
                return version;
            }

            // StartsWith patterns
            if (path.StartsWith(@"C:\MyWinMDComponents", StringComparison.OrdinalIgnoreCase))
            {
                return "Windows Runtime";
            }

            if (path.StartsWith(@"C:\WinMDArchVerification", StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".winmd", StringComparison.Ordinal))
            {
                return "WindowsRuntime 1.0";
            }

            // Default for assemblies
            if (path.EndsWith(".dll", StringComparison.Ordinal) ||
                path.EndsWith(".exe", StringComparison.Ordinal) ||
                path.EndsWith(".winmd", StringComparison.Ordinal))
            {
                return "v2.0.50727";
            }

            return "";
        }

        public override ushort ReadMachineTypeFromPEHeader(string dllPath)
            => MachineTypes.TryGetValue(dllPath, out ushort machineType)
                ? machineType
                : NativeMethods.IMAGE_FILE_MACHINE_INVALID;

        public override bool IsWinMDFile(string fullPath, out string imageRuntimeVersion, out bool isManagedWinMD)
        {
            imageRuntimeVersion = string.Empty;
            isManagedWinMD = false;

            // May be null or empty if the file was never resolved to a path on disk.
            if (string.IsNullOrEmpty(fullPath))
            {
                return false;
            }

            imageRuntimeVersion = GetAssemblyRuntimeVersion(fullPath);

            // Managed WinMD (special case)
            if (string.Equals(fullPath, @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd", StringComparison.OrdinalIgnoreCase))
            {
                isManagedWinMD = true;
                return true;
            }

            // Simple WinMD paths
            if (WinMDPaths.Contains(fullPath))
            {
                return true;
            }

            // StartsWith patterns - C:\MyWinMDComponents doesn't require .winmd extension check
            if (fullPath.StartsWith(@"C:\MyWinMDComponents", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // C:\DirectoryContains* and C:\WinMDArchVerification* require .winmd extension
            return Path.GetExtension(fullPath).Equals(".winmd", StringComparison.OrdinalIgnoreCase) &&
                (fullPath.StartsWith(@"C:\DirectoryContains", StringComparison.OrdinalIgnoreCase) ||
                 fullPath.StartsWith(@"C:\WinMDArchVerification", StringComparison.OrdinalIgnoreCase));
        }

        public override string GetAssemblyPathInGac(AssemblyNameExtension assemblyName, ProcessorArchitecture targetProcessorArchitecture, Version targetedRuntimeVersion, bool fullFusionName, bool specificVersion)
        {
            if (GacPaths.TryGetValue(assemblyName.FullName, out string gacPath))
            {
                return gacPath;
            }

#if FEATURE_GAC
            if (assemblyName.Version != null)
            {
                return GlobalAssemblyCache.GetLocation(
                    assemblyName,
                    targetProcessorArchitecture,
                    services: this,
                    targetedRuntimeVersion,
                    fullFusionName,
                    getPathFromFusionName: null,
                    getGacEnumerator: null,
                    specificVersion);
            }
#endif
            return null;
        }

#if FEATURE_WIN32_REGISTRY

        public override RegistryKey OpenBaseKey(RegistryHive hive, RegistryView view)
            => hive switch
            {
                RegistryHive.CurrentUser => Registry.CurrentUser,
                RegistryHive.LocalMachine => Registry.LocalMachine,
                _ => null,
            };

        public override IEnumerable<string> GetSubKeyNames(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (RegistrySubKeyNames_CurrentUser.TryGetValue(subKey, out string[] result))
                {
                    return result;
                }
            }
            else if (baseKey == Registry.LocalMachine)
            {
                if (RegistrySubKeyNames_LocalMachine.TryGetValue(subKey, out string[] result))
                {
                    return result;
                }
            }

            Assert.Fail($"New GetRegistrySubKeyNames parameters encountered, need to add unittesting support for subKey={subKey}");
            return null;
        }

        public override string GetDefaultValue(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (RegistrySubKeyDefaultValue_CurrentUser.TryGetValue(subKey, out string result))
                {
                    return result;
                }
            }
            else if (baseKey == Registry.LocalMachine)
            {
                if (RegistrySubKeyDefaultValue_LocalMachine.TryGetValue(subKey, out string result))
                {
                    return result;
                }
            }

            Assert.Fail($"New GetRegistrySubKeyDefaultValue parameters encountered, need to add unittesting support for subKey={subKey}");
            return null;
        }

#endif
    }
}
