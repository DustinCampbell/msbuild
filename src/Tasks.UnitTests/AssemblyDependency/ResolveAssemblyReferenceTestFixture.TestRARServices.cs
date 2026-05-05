// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Tasks.AssemblyDependency;
using Microsoft.Win32;
using Xunit;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using NativeMethods = Microsoft.Build.Tasks.NativeMethods;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public partial class ResolveAssemblyReferenceTestFixture
{
    /// <summary>
    ///  A test implementation of RARServices that calls the fixture's static methods.
    ///  For tests needing custom behavior, pass Func overrides to the constructor.
    /// </summary>
    internal sealed class TestRARServices(
        Func<string, bool> fileExists = null,
        Func<string, AssemblyNameExtension> getAssemblyName = null) : RARServices
    {
        /// <summary>
        /// Default test services instance that calls the fixture's static methods.
        /// Most tests use this. Tests needing custom behavior create their own TestRARServices with overrides.
        /// </summary>
        public static new readonly TestRARServices Default = new TestRARServices();

        public override bool FileExists(string path)
        {
            if (fileExists is not null)
            {
                return fileExists(path);
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

            foreach (string file in s_existentFiles)
            {
                if (string.Equals(path, file, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // Everything else doesn't exist.
            return false;
        }

        /// <summary>
        /// Directories that are considered to exist.
        /// </summary>
        private static readonly FrozenSet<string> s_existentDirs = FrozenSet.Create(
            StringComparer.OrdinalIgnoreCase,
            [
                s_myVersion20Path,
                @"c:\SGenDependeicies",
                Path.GetTempPath()
            ]);

        public override bool DirectoryExists(string path)
            => s_existentDirs.Contains(path);

        public override string[] GetDirectories(string path, string searchPattern)
        {
            if (path.EndsWith(s_myVersion20Path, StringComparison.Ordinal))
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
            => getAssemblyName is not null
                ? getAssemblyName(path)
                : GetAssemblyNameCore(path);

        /// <summary>
        /// Assembly name mappings for GetAssemblyName (path → assembly name string).
        /// </summary>
        private static readonly FrozenDictionary<string, string> s_assemblyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll")] = "DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral",
            [Path.Combine(s_frameworksPath, "DependsOnFoo4Framework.dll")] = "DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [Path.Combine(s_frameworksPath, "DependsOnFoo35Framework.dll")] = "DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral",
            [@"c:\Regress315619\A\MyAssembly.dll"] = "MyAssembly, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"c:\Regress315619\B\MyAssembly.dll"] = "MyAssembly, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [s_regress442570_ADllPath] = "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
            [@"c:\Regress387218\v1\D.dll"] = "D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [s_regress442570_BDllPath] = "B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
            [@"c:\Regress387218\v2\D.dll"] = "D, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"c:\Regress390219\v1\D.dll"] = "D, Version=1.0.0.0, Culture=fr, PublicKeyToken=b77a5c561934e089",
            [@"c:\Regress390219\v2\D.dll"] = "D, Version=2.0.0.0, Culture=en, PublicKeyToken=b77a5c561934e089",
            [@"c:\MyStronglyNamed\A.dll"] = "A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089",
            [@"c:\MyNameMismatch\Foo.dll"] = "A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll")] = AssemblyRef.SystemXml,
            [Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll")] = AssemblyRef.SystemXml,
            [Path.Combine(s_myVersion20Path, "System.XML.dll")] = AssemblyRef.SystemXml,
            [Path.Combine(s_myProjectPath, "System.Xml.dll")] = AssemblyRef.SystemXml,
            [Path.Combine(s_myProjectPath, "System.Data.dll")] = "System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=A77a5c561934e089",
            [Path.Combine(s_myVersion20Path, "System.dll")] = "System, VeRSion=2.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myVersion40Path, "System.dll")] = "System, VeRSion=4.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myVersion90Path, "System.dll")] = "System, VeRSion=9.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089",
            [@"C:\Framework\Everett\System.dll"] = "System, Version=1.0.5000.0, Culture=neutral, PublICKeyToken=" + AssemblyRef.EcmaPublicKey,
            [@"C:\Framework\Whidbey\System.dll"] = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey,
            [Path.Combine(s_myVersion20Path, "System.Data.dll")] = AssemblyRef.SystemData,
            [s_unifyMeDll_V10Path] = "UnifyMe, Version=1.0.0.0, Culture=nEUtral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL",
            [Path.Combine(s_myApp_V10Path, "DependsOnEverettSystem.dll")] = "DependsOnEverettSystem, VersION=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe",
            [Path.Combine(s_myApp_V05Path, "DependsOnUnified.dll")] = "DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\Regress339786\FolderA\C.dll"] = "C, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [@"C:\Regress339786\FolderB\C.dll"] = "C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll")] = "DependsOnUnified, VERSion=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myApp_V20Path, "DependsOnUnified.dll")] = "DependsOnUnified, VeRSIon=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myApp_V30Path, "DependsOnUnified.dll")] = "DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKEYToken=b77a5c561934e089",
            [s_unifyMeDll_V20Path] = "UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyTOKEn=b77a5c561934e089",
            [s_unifyMeDll_V30Path] = "UnifyMe, Version=3.0.0.0, Culture=neutral, PublICkeyToken=b77a5c561934e089",
            [@"C:\Regress317975\a.dll"] = "A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            [@"C:\Regress317975\b.dll"] = "B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            [@"C:\Regress317975\v2\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [s_40ComponentDependsOnOnlyv4AssembliesDllPath] = "DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
            [Path.Combine(s_myComponentsMiscPath, "ReferenceVersion9.dll")] = "ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
            [Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll")] = "DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
            [Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll")] = "DependsOn9Also, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
            [Path.Combine(s_myComponents10Path, "DependsOn9.dll")] = "DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
            [Path.Combine(s_myComponents20Path, "DependsOn9.dll")] = "DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
            [s_regress444809_ADllPath] = "A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [s_regress444809_V2_ADllPath] = "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [s_regress444809_BDllPath] = "B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [s_regress444809_CDllPath] = "C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [s_regress444809_DDllPath] = "D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "X.pdb")] = "X, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\Regress714052\X86\a.dll"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
            [@"C:\Regress714052\Mix\a.dll"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
            [@"C:\Regress714052\Mix\a.winmd"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
            [@"C:\Regress714052\MSIL\a.dll"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
            [@"C:\Regress714052\None\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [@"C:\Regress714052\X86\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
            [@"C:\Regress714052\Mix\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
            [@"C:\Regress714052\Mix\b.winmd"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
            [@"C:\Regress714052\MSIL\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
            [Path.Combine(s_myComponentsRootPath, "V.dll")] = "V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponents2RootPath, "W.dll")] = "W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "X.dll")] = "X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "Z.dll")] = "Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "Y.dll")] = "Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "Microsoft.Build.dll")] = "Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
            [Path.Combine(s_myComponentsRootPath, "DependsOnMSBuild12.dll")] = "DependsOnMSBuild12, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [@"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll"] = "DotNetAssemblyDependsOnWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll"] = "DotNetAssemblyDependsOn255WinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd"] = "SampleWindowsRuntimeOnly, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd"] = "DependsOnInvalidPeHeader, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnAmd64.Winmd"] = "DependsOnAmd64, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnArm.Winmd"] = "DependsOnArm, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnIA64.Winmd"] = "DependsOnIA64, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnArmv7.Winmd"] = "DependsOnArmv7, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnX86.Winmd"] = "DependsOnX86, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnUnknown.Winmd"] = "DependsOnUnknown, Version=1.0.0.0",
            [@"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd"] = "DependsOnAnyCPUUnknown, Version=1.0.0.0",
            [@"C:\WinMD\WinMDWithVersion255.Winmd"] = "WinMDWithVersion255, Version=255.255.255.255",
            [@"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd"] = "SampleWindowsRuntimeOnly2, Version=1.0.0.0",
            [@"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd"] = "SampleWindowsRuntimeOnly3, Version=1.0.0.0",
            [@"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd"] = "SampleWindowsRuntimeOnly4, Version=1.0.0.0",
            [@"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd"] = "SampleWindowsRuntimeReferencingSystem, Version=1.0.0.0",
            [@"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd"] = "SampleWindowsRuntimeReferencingSystemDNE, Version=1.0.0.0",
            [@"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd"] = "SampleWindowsRuntimeAndCLR, Version=1.0.0.0",
            [@"C:\WinMD\v4\MsCorlib.dll"] = "mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\WinMD\v255\MsCorlib.dll"] = "mscorlib, Version=255.255.255.255, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\MyWinMDComponents\MyGridWinMD.winmd"] = "MyGridWinMD, Version=1.0.0.0",
            [@"C:\MyWinMDComponents2\MyGridWinMD.winmd"] = "MyGridWinMD, Version=2.0.0.0",
            [@"C:\MyWinMDComponent7s\MyGridWinMD.winmd"] = "MyGridWinMD, Version=1.0.0.0",
            [@"C:\MyWinMDComponents9\MyGridWinMD.winmd"] = "MyGridWinMD, Version=1.0.0.0",
            [@"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd"] = "MyGridWinMD2, Version=1.0.0.0",
            [@"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd"] = "MyGridWinMD3, Version=1.0.0.0",
            [@"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd"] = "DebugX86SDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd"] = "DebugNeutralSDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd"] = "X86SDKWinMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd"] = "NeutralSDKWINMD, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll"] = "Debugx86SDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll"] = "DebugNeutralSDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll"] = "X86SDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll"] = "NeutralSDKRA, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\FakeSDK\References\Debug\X86\SDKReference.dll"] = "SDKReference, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\DirectoryContainsOnlyDll\a.dll"] = "A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\DirectoryContainsdllAndWinmd\b.dll"] = "b, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\DirectoryContainsdllAndWinmd\c.winmd"] = "C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\DirectoryContainstwoWinmd\a.winmd"] = "A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"C:\DirectoryContainstwoWinmd\c.winmd"] = "C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
            [@"c:\assemblyfromconfig\folder_x64\assemblyfromconfig_common.dll"] = "assemblyfromconfig_common, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=AMD64",
            [@"c:\assemblyfromconfig\folder_x86\assemblyfromconfig_common.dll"] = "assemblyfromconfig_common, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
            [@"c:\assemblyfromconfig\folder5010x64\v5assembly.dll"] = "v5assembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=AMD64",
            [@"c:\assemblyfromconfig\folder501000x86\v5assembly.dll"] = "v5assembly, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
            [s_dependsOnNuGet_ADllPath] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [s_nugetCache_N_Lib_NDllPath] = "N, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Paths for which GetAssemblyName returns null (mscorlib with no metadata).
        /// </summary>
        private static readonly FrozenSet<string> s_assemblyNameNullPaths = FrozenSet.Create(
            StringComparer.OrdinalIgnoreCase,
            [
                @"c:\Regress313086\mscorlib.dll",
                Path.Combine(s_myProjectPath, "mscorlib.dll"),
                Path.Combine(s_myVersion20Path, "mscorlib.dll"),
                Path.Combine(s_myVersionPocket20Path, "mscorlib.dll")
            ]);

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

            if (string.Equals(path, Path.Combine(s_myVersion20Path, "BadImage.dll"), StringComparison.OrdinalIgnoreCase))
            {
                throw new BadImageFormatException($"The format of the file '{Path.Combine(s_myVersion20Path, "BadImage.dll")}' is invalid");
            }

            if (string.Equals(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase))
            {
                // Simulate an assembly that throws an UnauthorizedAccessException upon access.
                throw new UnauthorizedAccessException();
            }

            if (string.Equals(path, s_unifyMeDll_V05Path, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException();
            }

            if (path.Contains("MyMissingAssembly"))
            {
                throw new FileNotFoundException(path);
            }

            // Category B: Null returns
            if (s_assemblyNameNullPaths.Contains(path))
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
            if (path.EndsWith(Path.Combine(s_myVersion20Path, "MyGacAssembly.dll"), StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("MyGacAssembly, Version=9.2.3401.1, Culture=neutral, PublicKeyToken=a6694b450823df78");
            }

            if (path.EndsWith(s_myLibraries_V1_DDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("D, Version=1.0.0.0, CulTUre=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(@"c:\RogueLibraries\v1\D.dll", StringComparison.Ordinal))
            {
                // Version 1 of D, but with a different PKT
                return new AssemblyNameExtension("D, VERsion=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb");
            }

            if (path.EndsWith(s_myLibraries_V1_E_EDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("E, Version=0.0.0.0, Culture=neutral, PUBlicKeyToken=null");
            }

            if (path.EndsWith(s_myLibraries_V2_DDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("D, VErsion=2.0.0.0, CulturE=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(s_myLibraries_V1_GDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("G, Version=1.0.0.0, Culture=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            if (path.EndsWith(s_myLibraries_V2_GDllPath, StringComparison.Ordinal))
            {
                return new AssemblyNameExtension("G, Version=2.0.0.0, Culture=neutral, PublicKEyToken=aaaaaaaaaaaaaaaa");
            }

            // Category E: Dictionary lookup
            if (s_assemblyNames.TryGetValue(path, out string assemblyName))
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
            FrameworkNameVersioning frameworkName = GetTargetFrameworkAttribute(path);
            string[] scatterFiles = path == @"C:\Regress275161\a.dll"
                ? ["m1.netmodule", "m2.netmodule"]
                : null;

            return new AssemblyMetadata(dependencies, scatterFiles, frameworkName);
        }

        /// <summary>
        /// Paths that return empty dependency arrays from GetDependencies.
        /// </summary>
        private static readonly FrozenSet<string> s_dependenciesEmptyPaths = FrozenSet.Create(
            StringComparer.OrdinalIgnoreCase,
            [
                @"c:\Regress313086\mscorlib.dll",
                @"MyRelativeAssembly.dll",
                s_myLibraries_V1_E_EDllPath,
                Path.Combine(s_myComponents2RootPath, "W.dll"),
                Path.Combine(s_myComponentsRootPath, "Z.dll"),
                Path.Combine(s_myComponentsRootPath, "Microsoft.Build.dll"),
                @"C:\DirectoryContainsdllAndWinmd\c.winmd",
                @"C:\DirectoryContainstwoWinmd\c.winmd",
                @"C:\DirectoryTest\B.dll",
                Path.Combine(s_myVersion20Path, "mscorlib.dll"),
                Path.Combine(s_myVersionPocket20Path, "mscorlib.dll")
            ]);

        /// <summary>
        /// Single dependency mappings (path → dependency assembly name string).
        /// </summary>
        private static readonly FrozenDictionary<string, string> s_singleDependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(s_frameworksPath, "IndirectDependsOnFoo4Framework.dll")] = "DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [Path.Combine(s_frameworksPath, "IndirectDependsOnFoo45Framework.dll")] = "DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral",
            [Path.Combine(s_frameworksPath, "IndirectDependsOnFoo35Framework.dll")] = "DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral",
            [s_regress454863_ADllPath] = "B, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [s_regress442570_BDllPath] = " A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\Regress313747\Microsoft.Office.Interop.Excel.dll"] = " Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c",
            [@"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll"] = " Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5",
            [@"c:\Regress387218\A.dll"] = "D, Version=1.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [@"c:\Regress387218\B.dll"] = "D, Version=2.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [@"c:\Regress390219\A.dll"] = "D, Version=1.0.0.0, PublicKeyToken=b77a5c561934e089, Culture=fr",
            [@"c:\Regress390219\B.dll"] = "D, Version=2.0.0.0,  PublicKeyToken=b77a5c561934e089, Culture=en",
            [@"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll"] = "MyFileLoadExceptionAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [@"c:\Regress563286\DependsOnBadImage.dll"] = "BadImage, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [Path.Combine(s_myVersion20Path, "System.dll")] = "mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll"] = "SampleWindowsRuntimeOnly, Version=1.0.0.0",
            [@"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll"] = "WinMDWithVersion255, Version=255.255.255.255",
            [@"C:\WinMD\SampleWindowsRuntimeAndClr.Winmd"] = "mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd"] = "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\WinMD\WinMDWithVersion255.Winmd"] = "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd"] = "SampleWindowsRuntimeOnly, Version=1.0.0.0",
            [Path.Combine(s_myAppRootPath, "DependsOnSimpleA.dll")] = "A, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral",
            [@"C:\Regress312873\b.dll"] = "A, Version=0.0.0.0, PublickEyToken=null, Culture=Neutral",
            [@"C:\Regress339786\FolderA\a.dll"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
            [@"C:\Regress339786\FolderB\b.dll"] = "C, Version=2.0.0.0, PublickEyToken=null, Culture=Neutral",
            [@"C:\Regress317975\a.dll"] = "B, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
            [@"C:\myassemblies\My.Assembly.dll"] = "mscorlib, Version=2.0.0.0, Culture=NEUtraL, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myComponentsRootPath, "MyGrid.dll")] = "mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089",
            [@"C:\MyRawDropControls\MyRawDropControl.dll"] = "mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089",
            [s_myLibraries_ADllPath] = "D, Version=1.0.0.0, CuLtUrE=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa",
            [s_myLibraries_TDllPath] = "D, VeRsIon=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb",
            [s_myLibraries_V1_DDllPath] = "E, VERSIOn=0.0.0.0, Culture=neutral, PublicKeyToken=null",
            [s_myLibraries_V2_DDllPath] = "E, Version=0.0.0.0, Culture=neutRAL, PUblicKeyToken=null",
            [Path.Combine(s_myApp_V05Path, "DependsOnWeaklyNamedUnified.dll")] = "UnifyMe, Version=0.0.0.0, PUBLICKeyToken=null, CuLTURE=Neutral",
            [Path.Combine(s_myApp_V10Path, "DependsOnEverettSystem.dll")] = "System, VeRsiON=1.0.5000.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey,
            [Path.Combine(s_myApp_V05Path, "DependsOnUnified.dll")] = "UnifyMe, Version=0.5.0.0, CuLTUre=neUTral, PubLICKeyToken=b77a5c561934e089",
            [Path.Combine(s_myApp_V10Path, "DependsOnUnified.dll")] = "UNIFyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myApp_V20Path, "DependsOnUnified.dll")] = "UniFYme, Version=2.0.0.0, Culture=NeutraL, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myApp_V30Path, "DependsOnUnified.dll")] = "UnIfyMe, Version=3.0.0.0, Culture=nEutral, PublicKEyToken=b77a5c561934e089",
            [s_40ComponentDependsOnOnlyv4AssembliesDllPath] = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myComponentsMiscPath, "DependsOn9Also.dll")] = "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myComponents10Path, "DependsOn9.dll")] = "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myComponents20Path, "DependsOn9.dll")] = "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            [s_regress444809_BDllPath] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [s_regress444809_DDllPath] = "A, Version=20.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "V.dll")] = "W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "X.dll")] = "Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "Y.dll")] = "Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
            [Path.Combine(s_myComponentsRootPath, "DependsOnMSBuild12.dll")] = "Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
            [Path.Combine(s_myVersion40Path, "System.dll")] = "msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089",
            [Path.Combine(s_myVersion90Path, "System.dll")] = "msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089",
            [@"C:\DirectoryContainsOnlyDll\a.dll"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
            [@"C:\DirectoryContainsdllAndWinmd\b.dll"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
            [@"C:\DirectoryContainstwoWinmd\a.winmd"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
            [s_dependsOnNuGet_ADllPath] = "N, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Multiple dependency mappings (path → array of dependency assembly name strings).
        /// </summary>
        private static readonly FrozenDictionary<string, string[]> s_multipleDependencies = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd"] =
            [
                "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "SampleWindowsRuntimeOnly, Version=1.0.0.0",
                "SampleWindowsRuntimeReferencingSystem, Version=1.0.0.0",
                "WinMDWithVersion255, Version=255.255.255.255"
            ],
            [@"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd"] =
            [
                "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "SampleWindowsRuntimeReferencingSystemDNE, Version=1.0.0.0"
            ],
            [@"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd"] =
            [
                "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "System, Version=255.255.255.255, Culture=Neutral, PublicKeyToken=b77a5c561934e089"
            ],
            [@"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd"] =
            [
                "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "System.DoesNotExist, Version=255.255.255.255"
            ],
            [s_myLibraries_BDllPath] =
            [
                "D, Version=2.0.0.0, Culture=neutral, PuBlIcKeYToken=aaaaaaaaaaaaaaaa",
                "G, Version=2.0.0.0, Culture=neutral, PuBlIcKeYToken=aaaaaaaaaaaaaaaa"
            ],
            [Path.Combine(s_myComponentsMiscPath, "ReferenceVersion9.dll")] =
            [
                "mscorlib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "RandomAssembly, Version=9.0.0.0, Culture=neutral, PublicKeyToken=c77a5c561934e089"
            ],
            [Path.Combine(s_myComponentsMiscPath, "DependsOn9.dll")] =
            [
                "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
                "System.Data, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
            ],
            [s_regress444809_CDllPath] =
            [
                "B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
                "A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
            ],
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Given an assembly, with optional assemblyName return all of the dependent assemblies.
        /// </summary>
        /// <param name="path">The full path to the parent assembly</param>
        /// <returns>The array of dependent assembly names.</returns>
        private static AssemblyNameExtension[] GetDependencies(string path)
        {
            // Exception paths
            if (string.Equals(path, @"c:\MyInaccessible\A.dll", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException();
            }

            if (string.Equals(path, s_myMissingAssemblyAbsPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new FileNotFoundException(path);
            }

            // Empty dependency paths
            if (s_dependenciesEmptyPaths.Contains(path))
            {
                return [];
            }

            // StartsWith pattern for FakeSDK
            if (path.StartsWith(@"C:\FakeSDK\", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }

            // Dynamic dependency calls (call GetAssemblyName)
            if (string.Equals(path, s_portableDllPath, StringComparison.OrdinalIgnoreCase))
            {
                // Simulate a portable assembly with a reference to System.Runtime
                return [GetAssemblyNameCore(s_systemRuntimeDllPath)];
            }

            if (string.Equals(path, s_netstandardLibraryDllPath, StringComparison.OrdinalIgnoreCase))
            {
                // Simulate a .NET Standard assembly
                return [GetAssemblyNameCore(s_netstandardDllPath)];
            }

            if (string.Equals(path, @"C:\DirectoryTest\A.dll", StringComparison.OrdinalIgnoreCase))
            {
                return [GetAssemblyNameCore(@"C:\DirectoryTest\B.dll")];
            }

            // Multiple dependency lookup
            if (s_multipleDependencies.TryGetValue(path, out string[] multiDeps))
            {
                return [.. multiDeps.Select(x => new AssemblyNameExtension(x))];
            }

            // Single dependency lookup
            if (s_singleDependencies.TryGetValue(path, out string singleDep))
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

        /// <summary>
        /// Cached implementation. Given an assembly name, crack it open and retrieve the TargetFrameworkAttribute
        /// </summary>
        /// <summary>
        /// Target framework attribute mappings for GetTargetFrameworkAttribute.
        /// </summary>
        private static readonly FrozenDictionary<string, FrameworkNameVersioning> s_targetFrameworks = new Dictionary<string, FrameworkNameVersioning>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine(s_frameworksPath, "DependsOnFoo4Framework.dll")] = new("FoO, Version=v4.0"),
            [Path.Combine(s_frameworksPath, "DependsOnFoo45Framework.dll")] = new("FoO, Version=v4.5"),
            [Path.Combine(s_frameworksPath, "DependsOnFoo35Framework.dll")] = new("FoO, Version=v3.5"),
            [Path.Combine(s_frameworksPath, "IndirectDependsOnFoo4Framework.dll")] = new("FoO, Version=v4.0"),
            [Path.Combine(s_frameworksPath, "IndirectDependsOnFoo45Framework.dll")] = new("FoO, Version=v4.0"),
            [Path.Combine(s_frameworksPath, "IndirectDependsOnFoo35Framework.dll")] = new("FoO, Version=v4.0"),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private static FrameworkNameVersioning GetTargetFrameworkAttribute(string path)
            => s_targetFrameworks.TryGetValue(path, out FrameworkNameVersioning result)
                ? result
                : null;

        public override DateTime GetLastWriteTime(string path)
                => FileExists(path)
                    ? DateTime.FromFileTimeUtc(1)
                    : DateTime.FromFileTimeUtc(0);

        /// <summary>
        /// Runtime version mappings for GetRuntimeVersion.
        /// </summary>
        private static readonly FrozenDictionary<string, string> s_runtimeVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd"] = "WindowsRuntime 1.0, CLR V2.0.50727",
            [@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\WinMDWithVersion255.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd"] = "WindowsRuntime 1.0",
            [@"C:\WinMD\SampleClrOnly.Winmd"] = "CLR V2.0.50727",
            [@"C:\WinMD\SampleBadWindowsRuntime.Winmd"] = "Windows Runtime",
            [@"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd"] = "WindowsRuntime 1.0, Other V2.0.50727",
            [@"C:\DirectoryContainsOnlyDll\a.dll"] = "V2.0.50727",
            [@"C:\DirectoryContainsdllAndWinmd\b.dll"] = "V2.0.50727",
            [@"C:\DirectoryContainsdllAndWinmd\c.winmd"] = "WindowsRuntime 1.0",
            [@"C:\DirectoryContainstwoWinmd\a.winmd"] = "WindowsRuntime 1.0",
            [@"C:\DirectoryContainstwoWinmd\c.winmd"] = "WindowsRuntime 1.0",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        public override string GetAssemblyRuntimeVersion(string path)
        {
            // Check explicit path mappings first
            if (s_runtimeVersions.TryGetValue(path, out string version))
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

        /// <summary>
        /// PE header machine type mappings for ReadMachineTypeFromPEHeader.
        /// </summary>
        private static readonly FrozenDictionary<string, ushort> s_machineTypes = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            [@"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll"] = NativeMethods.IMAGE_FILE_MACHINE_INVALID,
            [@"C:\WinMDArchVerification\DependsOnAmd64.dll"] = NativeMethods.IMAGE_FILE_MACHINE_AMD64,
            [@"C:\WinMDArchVerification\DependsOnX86.dll"] = NativeMethods.IMAGE_FILE_MACHINE_I386,
            [@"C:\WinMDArchVerification\DependsOnArm.dll"] = NativeMethods.IMAGE_FILE_MACHINE_ARM,
            [@"C:\WinMDArchVerification\DependsOnArmV7.dll"] = NativeMethods.IMAGE_FILE_MACHINE_ARMV7,
            [@"C:\WinMDArchVerification\DependsOnIA64.dll"] = NativeMethods.IMAGE_FILE_MACHINE_IA64,
            [@"C:\WinMDArchVerification\DependsOnUnknown.dll"] = NativeMethods.IMAGE_FILE_MACHINE_R4000,
            [@"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll"] = NativeMethods.IMAGE_FILE_MACHINE_UNKNOWN,
            [@"C:\WinMD\SampleWindowsRuntimeOnly.dll"] = NativeMethods.IMAGE_FILE_MACHINE_I386,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        public override ushort ReadMachineTypeFromPEHeader(string dllPath)
            => s_machineTypes.TryGetValue(dllPath, out ushort machineType)
                ? machineType
                : NativeMethods.IMAGE_FILE_MACHINE_INVALID;

        /// <summary>
        /// WinMD paths that return true from IsWinMDFile (isManagedWinMD = false).
        /// </summary>
        private static readonly FrozenSet<string> s_winmdPaths = FrozenSet.Create(
            StringComparer.OrdinalIgnoreCase,
            [
                @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd",
                @"C:\WinMD\WinMDWithVersion255.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd",
                @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd",
                @"C:\FakeSDK\WindowsMetadata\SDKWinMD2.Winmd",
                @"C:\FakeSDK\WindowsMetadata\SDKWinMD.Winmd",
                @"C:\WinMDLib\LibWithWinmdAndNoDll.Winmd"
            ]);

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
            if (s_winmdPaths.Contains(fullPath))
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

        /// <summary>
        /// GAC path mappings for specific assembly names. Value is path, or null for assemblies not in GAC.
        /// </summary>
        private static readonly FrozenDictionary<string, string> s_gacPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = null,
            ["W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = @"C:\MyComponents2\W.dll",
            ["X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = @"C:\MyComponents\X.dll",
            ["Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = null,
            ["Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = null,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        public override string GetAssemblyPathInGac(AssemblyNameExtension assemblyName, SystemProcessorArchitecture targetProcessorArchitecture, Version targetedRuntimeVersion, bool fullFusionName, bool specificVersion)
        {
            if (s_gacPaths.TryGetValue(assemblyName.FullName, out string gacPath))
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

        /// <summary>
        /// Registry subkey names for CurrentUser hive.
        /// </summary>
        private static readonly FrozenDictionary<string, string[]> s_registrySubKeyNames_CurrentUser = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [@"Software\Regress714052"] = [],
            [@"Software\Regress714052\v2.0.0\AssemblyFoldersEx"] = [],
            [@"Software\Regress714052\v2.0.0\X86"] = [],
            [@"Software\Regress714052\v2.0.0\MSIL"] = [],
            [@"Software\Regress714052\v2.0.0\Mix"] = [],
            [@"Software\Regress714052\v2.0.0\Mix\Mix"] = [],
            [@"Software\Regress714052\v2.0.0\None"] = [],
            [@"Software\Regress714052\v2.0.0\X86\X86"] = [],
            [@"Software\Regress714052\v2.0.0\MSIL\MSIL"] = [],
            [@"Software\Regress714052\v2.0.0\None\None"] = [],
            [@"Software\Microsoft\.NetFramework"] = ["", "vBogusVersion", "v1.a.2.3", "v1.0", "v3.0", "v2.0.50727", "v2.0.x86chk", "RandomJunk"],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx"] = ["ZControlA", "ZControlB", "Infragistics.GridControl.1.0", "Infragistics.MyHKLMControl.1.0", "Infragistics.MyControlWithFutureTargetNDPVersion.1.0", "Infragistics.MyControlWithPastTargetNDPVersion.1.0", "Infragistics.MyControlWithServicePack.1.0"],
            [@"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx"] = ["RawDropControls"],
            [@"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx"] = ["Infragistics.MyControlWithFutureTargetNDPVersion.1.0"],
            [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx"] = ["Infragistics.MyNDP1Control.1.0", "Infragistics.MyControlWithPastTargetNDPVersion.1.0"],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0"] = ["sp1", "sp2"],
            [@"Software\Microsoft\.NETCompactFramework"] = ["v2.0.3600"],
            [@"Software\Microsoft\.NETCompactFramework\v2.0.3600"] = ["PocketPC"],
            [@"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx"] = ["AFETestDeviceControl"],
            [@"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl"] = ["1234"],
            [@"Software\Microsoft\Microsoft SDKs"] = ["Windows"],
            [@"Software\Microsoft\Microsoft SDKs\Windows"] = ["7.0", "8.0", "v8.0", "9.0"],
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registry subkey names for LocalMachine hive.
        /// </summary>
        private static readonly FrozenDictionary<string, string[]> s_registrySubKeyNames_LocalMachine = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [@"Software\Regress714052"] = ["v2.0.0"],
            [@"Software\Regress714052\v2.0.0\AssemblyFoldersEx"] = ["A", "B"],
            [@"Software\Regress714052\v2.0.0\AssemblyFoldersEx\A"] = [],
            [@"Software\Regress714052\v2.0.0\AssemblyFoldersEx\B"] = [],
            [@"Software\Regress714052\v2.0.0\X86"] = ["X86"],
            [@"Software\Regress714052\v2.0.0\MSIL"] = ["MSIL"],
            [@"Software\Regress714052\v2.0.0\None"] = ["None"],
            [@"Software\Regress714052\v2.0.0\Mix"] = ["Mix"],
            [@"Software\Regress714052\v2.0.0\Mix\Mix"] = [],
            [@"Software\Regress714052\v2.0.0\X86\X86"] = [],
            [@"Software\Regress714052\v2.0.0\MSIL\MSIL"] = [],
            [@"Software\Regress714052\v2.0.0\None\None"] = [],
            [@"Software\Microsoft\.NetFramework"] = ["vBogusVersion", "v2.0.50727"],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx"] = ["Infragistics.FancyControl.1.0", "Infragistics.MyHKLMControl.1.0"],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.FancyControl.1.0"] = [],
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0"] = [],
            [@"Software\Microsoft\.NETCompactFramework"] = ["v2.0.3600"],
            [@"Software\Microsoft\.NETCompactFramework\v2.0.3600"] = ["PocketPC"],
            [@"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx"] = [],
            [@"Software\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl"] = [],
            [@"Software\Microsoft\Microsoft SDKs\Windows"] = ["8.0"],
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        public override IEnumerable<string> GetSubKeyNames(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (s_registrySubKeyNames_CurrentUser.TryGetValue(subKey, out string[] result))
                {
                    return result;
                }
            }
            else if (baseKey == Registry.LocalMachine)
            {
                if (s_registrySubKeyNames_LocalMachine.TryGetValue(subKey, out string[] result))
                {
                    return result;
                }
            }

            Assert.Fail($"New GetRegistrySubKeyNames parameters encountered, need to add unittesting support for subKey={subKey}");
            return null;
        }

        /// <summary>
        /// Registry subkey default values for CurrentUser hive.
        /// </summary>
        private static readonly FrozenDictionary<string, string> s_registrySubKeyDefaultValue_CurrentUser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA"] = @"C:\MyComponentsA",
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB"] = @"C:\MyComponentsB",
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0"] = @"C:\MyComponents",
            [@"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls"] = @"C:\MyRawDropControls",
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0"] = @"C:\MyComponents\HKCU Components",
            [@"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0"] = s_myComponentsV30Path,
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0"] = s_myComponentsV20Path,
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0"] = s_myComponentsV20Path,
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0"] = s_myComponentsV20Path,
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp1"] = @"C:\MyComponentServicePack1",
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp2"] = @"C:\MyComponentServicePack2",
            [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0"] = s_myComponentsV10Path,
            [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0"] = s_myComponentsV10Path,
            [@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl"] = @"C:\V1Control",
            [@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234"] = @"C:\V1ControlSP1",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registry subkey default values for LocalMachine hive.
        /// </summary>
        private static readonly FrozenDictionary<string, string> s_registrySubKeyDefaultValue_LocalMachine = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.FancyControl.1.0"] = @"C:\MyComponents\HKLM Components",
            [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0"] = @"C:\MyComponents\HKLM Components",
            [@"Software\Regress714052\v2.0.0\AssemblyFoldersEx\B"] = @"C:\Regress714052\X86",
            [@"Software\Regress714052\v2.0.0\AssemblyFoldersEx\A"] = @"C:\Regress714052\MSIL",
            [@"Software\Regress714052\v2.0.0\X86\X86"] = @"C:\Regress714052\X86",
            [@"Software\Regress714052\v2.0.0\Mix\Mix"] = @"C:\Regress714052\Mix",
            [@"Software\Regress714052\v2.0.0\MSIL\MSIL"] = @"C:\Regress714052\MSIL",
            [@"Software\Regress714052\v2.0.0\None\None"] = @"C:\Regress714052\None",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        public override string GetDefaultValue(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (s_registrySubKeyDefaultValue_CurrentUser.TryGetValue(subKey, out string result))
                {
                    return result;
                }
            }
            else if (baseKey == Registry.LocalMachine)
            {
                if (s_registrySubKeyDefaultValue_LocalMachine.TryGetValue(subKey, out string result))
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
