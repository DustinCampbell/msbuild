// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Tasks;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

internal static class TestData
{
    public static readonly string RootPathPrefix = NativeMethodsShared.IsWindows ? "C:\\" : Path.VolumeSeparatorChar.ToString();
    public static readonly string MyProjectPath = Path.Combine(RootPathPrefix, "MyProject");

    public static readonly string MyVersion20Path = Path.Combine(RootPathPrefix, "WINNT", "Microsoft.NET", "Framework", "v2.0.MyVersion");
    public static readonly string MyVersion40Path = Path.Combine(RootPathPrefix, "WINNT", "Microsoft.NET", "Framework", "v4.0.MyVersion");
    public static readonly string MyVersion90Path = Path.Combine(RootPathPrefix, "WINNT", "Microsoft.NET", "Framework", "v9.0.MyVersion");

    public static readonly string MyVersionPocket20Path = MyVersion20Path + ".PocketPC";

    public static readonly string MyMissingAssemblyAbsPath = Path.Combine(RootPathPrefix, "MyProject", "MyMissingAssembly.dll");
    public static readonly string MyMissingAssemblyRelPath = Path.Combine("MyProject", "MyMissingAssembly.dll");
    public static readonly string MyPrivateAssemblyRelPath = Path.Combine("MyProject", "MyPrivateAssembly.exe");

    public static readonly string FrameworksPath = Path.Combine(RootPathPrefix, "Frameworks");

    public static readonly string MyComponents2RootPath = Path.Combine(RootPathPrefix, "MyComponents2");
    public static readonly string MyComponentsRootPath = Path.Combine(RootPathPrefix, "MyComponents");
    public static readonly string MyComponents10Path = Path.Combine(MyComponentsRootPath, "1.0");
    public static readonly string MyComponents20Path = Path.Combine(MyComponentsRootPath, "2.0");
    public static readonly string MyComponentsMiscPath = Path.Combine(MyComponentsRootPath, "misc");

    public static readonly string MyComponentsV05Path = Path.Combine(MyComponentsRootPath, "v0.5");
    public static readonly string MyComponentsV10Path = Path.Combine(MyComponentsRootPath, "v1.0");
    public static readonly string MyComponentsV20Path = Path.Combine(MyComponentsRootPath, "v2.0");
    public static readonly string MyComponentsV30Path = Path.Combine(MyComponentsRootPath, "v3.0");

    public static readonly string UnifyMeDll_V05Path = Path.Combine(MyComponentsV05Path, "UnifyMe.dll");
    public static readonly string UnifyMeDll_V10Path = Path.Combine(MyComponentsV10Path, "UnifyMe.dll");
    public static readonly string UnifyMeDll_V20Path = Path.Combine(MyComponentsV20Path, "UnifyMe.dll");
    public static readonly string UnifyMeDll_V30Path = Path.Combine(MyComponentsV30Path, "UnifyMe.dll");

    public static readonly string MyComponents40ComponentPath = Path.Combine(MyComponentsRootPath, "4.0Component");
    public static readonly string MyComponents40ComponentDependsOnOnlyv4AssembliesDllPath = Path.Combine(MyComponents40ComponentPath, "DependsOnOnlyv4Assemblies.dll");

    public static readonly string MyLibrariesRootPath = Path.Combine(RootPathPrefix, "MyLibraries");
    public static readonly string MyLibraries_V1Path = Path.Combine(MyLibrariesRootPath, "v1");
    public static readonly string MyLibraries_V2Path = Path.Combine(MyLibrariesRootPath, "v2");
    public static readonly string MyLibraries_V1_EPath = Path.Combine(MyLibraries_V1Path, "E");

    public static readonly string MyLibraries_ADllPath = Path.Combine(MyLibrariesRootPath, "A.dll");
    public static readonly string MyLibraries_BDllPath = Path.Combine(MyLibrariesRootPath, "B.dll");
    public static readonly string MyLibraries_CDllPath = Path.Combine(MyLibrariesRootPath, "C.dll");
    public static readonly string MyLibraries_TDllPath = Path.Combine(MyLibrariesRootPath, "T.dll");

    public static readonly string MyLibraries_V1_DDllPath = Path.Combine(MyLibraries_V1Path, "D.dll");
    public static readonly string MyLibraries_V1_E_EDllPath = Path.Combine(MyLibraries_V1_EPath, "E.dll");
    public static readonly string MyLibraries_V2_DDllPath = Path.Combine(MyLibraries_V2Path, "D.dll");
    public static readonly string MyLibraries_V1_GDllPath = Path.Combine(MyLibraries_V1Path, "G.dll");
    public static readonly string MyLibraries_V2_GDllPath = Path.Combine(MyLibraries_V2Path, "G.dll");

    public static readonly string Regress454863_ADllPath = Path.Combine(RootPathPrefix, "Regress454863", "A.dll");
    public static readonly string Regress454863_BDllPath = Path.Combine(RootPathPrefix, "Regress454863", "B.dll");

    public static readonly string Regress444809RootPath = Path.Combine(RootPathPrefix, "Regress444809");
    public static readonly string Regress444809_ADllPath = Path.Combine(Regress444809RootPath, "A.dll");
    public static readonly string Regress444809_BDllPath = Path.Combine(Regress444809RootPath, "B.dll");
    public static readonly string Regress444809_CDllPath = Path.Combine(Regress444809RootPath, "C.dll");
    public static readonly string Regress444809_DDllPath = Path.Combine(Regress444809RootPath, "D.dll");

    public static readonly string Regress444809_V2RootPath = Path.Combine(Regress444809RootPath, "v2");
    public static readonly string Regress444809_V2_ADllPath = Path.Combine(Regress444809_V2RootPath, "A.dll");

    public static readonly string Regress442570_RootPath = Path.Combine(RootPathPrefix, "Regress442570");
    public static readonly string Regress442570_ADllPath = Path.Combine(Regress442570_RootPath, "A.dll");
    public static readonly string Regress442570_BDllPath = Path.Combine(Regress442570_RootPath, "B.dll");

    public static readonly string MyAppRootPath = Path.Combine(RootPathPrefix, "MyApp");
    public static readonly string MyApp_V05Path = Path.Combine(MyAppRootPath, "v0.5");
    public static readonly string MyApp_V10Path = Path.Combine(MyAppRootPath, "v1.0");
    public static readonly string MyApp_V20Path = Path.Combine(MyAppRootPath, "v2.0");
    public static readonly string MyApp_V30Path = Path.Combine(MyAppRootPath, "v3.0");

    public static readonly string NetstandardLibraryDllPath = Path.Combine(RootPathPrefix, "NetStandard", "netstandardlibrary.dll");
    public static readonly string NetstandardDllPath = Path.Combine(RootPathPrefix, "NetStandard", "netstandard.dll");

    public static readonly string PortableDllPath = Path.Combine(RootPathPrefix, "SystemRuntime", "Portable.dll");
    public static readonly string SystemRuntimeDllPath = Path.Combine(RootPathPrefix, "SystemRuntime", "System.Runtime.dll");

    public static readonly string DependsOnNuGet_ADllPath = Path.Combine(RootPathPrefix, "DependsOnNuget", "A.dll");
    public static readonly string DependsOnNuGet_NDllPath = Path.Combine(RootPathPrefix, "DependsOnNuget", "N.dll");
    public static readonly string DependsOnNuGet_NExePath = Path.Combine(RootPathPrefix, "DependsOnNuget", "N.exe");
    public static readonly string DependsOnNuGet_NWinMdPath = Path.Combine(RootPathPrefix, "DependsOnNuget", "N.winmd");

    public static readonly string NugetCache_N_Lib_NDllPath = Path.Combine(RootPathPrefix, "NugetCache", "N", "lib", "N.dll");

    public static readonly string AssemblyFolder_RootPath = Path.Combine(RootPathPrefix, "AssemblyFolder");
    public static readonly string AssemblyFolder_SomeAssemblyDllPath = Path.Combine(AssemblyFolder_RootPath, "SomeAssembly.dll");

    /// <summary>
    ///  Directories that are considered to exist.
    /// </summary>
    public static readonly FrozenSet<string> ExistentDirs = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        [
            MyVersion20Path,
            @"c:\SGenDependeicies",
            Path.GetTempPath()
        ]);

    public static readonly ImmutableArray<string> ExistentFiles =
    [
        Path.Combine(FrameworksPath, "DependsOnFoo4Framework.dll"),
        Path.Combine(FrameworksPath, "DependsOnFoo45Framework.dll"),
        Path.Combine(FrameworksPath, "DependsOnFoo35Framework.dll"),
        Path.Combine(FrameworksPath, "IndirectDependsOnFoo45Framework.dll"),
        Path.Combine(FrameworksPath, "IndirectDependsOnFoo4Framework.dll"),
        Path.Combine(FrameworksPath, "IndirectDependsOnFoo35Framework.dll"),
        Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll"),
        Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll"),
        Path.Combine(MyVersion20Path, "System.Data.dll"),
        Path.Combine(MyVersion20Path, "System.Xml.dll"),
        Path.Combine(MyVersion20Path, "System.Xml.pdb"),
        Path.Combine(MyVersion20Path, "System.Xml.xml"),
        Path.Combine(MyVersion20Path, "en", "System.Xml.resources.dll"),
        Path.Combine(MyVersion20Path, "en", "System.Xml.resources.pdb"),
        Path.Combine(MyVersion20Path, "en", "System.Xml.resources.config"),
        Path.Combine(MyVersion20Path, "xx", "System.Xml.resources.dll"),
        Path.Combine(MyVersion20Path, "en-GB", "System.Xml.resources.dll"),
        Path.Combine(MyVersion20Path, "en-GB", "System.Xml.resources.pdb"),
        Path.Combine(MyVersion20Path, "en-GB", "System.Xml.resources.config"),
        Path.Combine(RootPathPrefix, MyPrivateAssemblyRelPath),
        Path.Combine(MyProjectPath, "MyCopyLocalAssembly.dll"),
        Path.Combine(MyProjectPath, "MyDontCopyLocalAssembly.dll"),
        Path.Combine(MyVersion20Path, "BadImage.dll"),            // An assembly that will give a BadImageFormatException from GetAssemblyName
        Path.Combine(MyVersion20Path, "BadImage.pdb"),
        Path.Combine(MyVersion20Path, "MyGacAssembly.dll"),
        Path.Combine(MyVersion20Path, "MyGacAssembly.pdb"),
        Path.Combine(MyVersion20Path, "xx", "MyGacAssembly.resources.dll"),
        Path.Combine(MyVersion20Path, "System.dll"),
        Path.Combine(MyVersion40Path, "System.dll"),
        Path.Combine(MyVersion90Path, "System.dll"),
        Path.Combine(MyVersion20Path, "mscorlib.dll"),
        Path.Combine(MyVersionPocket20Path, "mscorlib.dll"),
        @"C:\myassemblies\My.Assembly.dll",
        Path.Combine(MyProjectPath, "mscorlib.dll"),                           // This is an mscorlib.dll that has no metadata (i.e. GetAssemblyName returns null)
        Path.Combine(MyProjectPath, "System.Data.dll"),                        // This is a System.Data.dll that has the wrong pkt, it shouldn't be matched.
        Path.Combine(MyComponentsRootPath, "MyGrid.dll"),                      // A vendor component that we should find in the registry.
        @"C:\MyComponentsA\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
        @"C:\MyComponentsB\CustomComponent.dll",                                           // A vendor component that we should find in the registry.
        @"C:\MyWinMDComponents7\MyGridWinMD.winmd",
        @"C:\MyWinMDComponents9\MyGridWinMD.winmd",
        @"C:\MyWinMDComponents\MyGridWinMD.winmd",
        @"C:\MyWinMDComponents2\MyGridWinMD.winmd",
        @"C:\MyWinMDComponentsA\CustomComponentWinMD.winmd",
        @"C:\MyWinMDComponentsB\CustomComponentWinMD.winmd",
        @"C:\MyWinMDComponentsVv1\MyGridWinMD2.winmd",
        @"C:\MyWinMDComponentsV1\MyGridWinMD3.winmd",
        @"C:\MyRawDropControls\MyRawDropControl.dll",                             // A control installed by VSREG under v2.0.x86chk
        @"C:\MyComponents\HKLM Components\MyHKLMControl.dll",                    // A vendor component that is installed under HKLM but not HKCU.
        @"C:\MyComponents\HKCU Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
        @"C:\MyComponents\HKLM Components\MyHKLMandHKCUControl.dll",             // A vendor component that is installed under HKLM and HKCU.
        @"C:\MyWinMDComponents\HKLM Components\MyHKLMControlWinMD.winmd",                    // A vendor component that is installed under HKLM but not HKCU.
        @"C:\MyWinMDComponents\HKCU Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
        @"C:\MyWinMDComponents\HKLM Components\MyHKLMandHKCUControlWinMD.winmd",             // A vendor component that is installed under HKLM and HKCU.
        Path.Combine(MyComponentsV30Path, "MyControlWithFutureTargetNDPVersion.dll"),         // The future version of a component.
        Path.Combine(MyComponentsV20Path, "MyControlWithFutureTargetNDPVersion.dll"),         // The current version of a component.
        Path.Combine(MyComponentsV10Path, "MyNDP1Control.dll"),                               // A control that only has an NDP 1.0 version
        Path.Combine(MyComponentsV20Path, "MyControlWithPastTargetNDPVersion.dll"),           // The current version of a component.
        Path.Combine(MyComponentsV10Path, "MyControlWithPastTargetNDPVersion.dll"),           // The past version of a component.
        @"C:\MyComponentServicePack\MyControlWithServicePack.dll",               // The service pack 1 version of the control
        @"C:\MyComponentBase\MyControlWithServicePack.dll",                      // The non-service pack version of the control.
        @"C:\MyComponentServicePack2\MyControlWithServicePack.dll",              // The service pack 1 version of the control
        Path.Combine(MyVersionPocket20Path, "mscorlib.dll"),  // A devices mscorlib.
        MyLibraries_ADllPath,
        @"c:\MyExecutableLibraries\A.exe",
        MyLibraries_BDllPath,
        MyLibraries_CDllPath,
        MyLibraries_V1_DDllPath,
        MyLibraries_V1_E_EDllPath,
        @"c:\RogueLibraries\v1\D.dll",
        MyLibraries_V2_DDllPath,
        MyLibraries_V1_GDllPath,
        MyLibraries_V2_GDllPath,
        @"c:\MyStronglyNamed\A.dll",
        @"c:\MyWeaklyNamed\A.dll",
        @"c:\MyInaccessible\A.dll",
        @"c:\MyNameMismatch\Foo.dll",
        @"c:\MyEscapedName\=A=.dll",
        @"c:\MyEscapedName\__'ASP'dw0024ry.dll",
        Path.Combine(MyAppRootPath, "DependsOnSimpleA.dll"),
        @"C:\Regress312873\a.dll",
        @"C:\Regress312873\b.dll",
        @"C:\Regress312873-2\a.dll",
        @"C:\Regress275161\a.dll",
        @"C:\Regress317975\a.dll",
        @"C:\Regress317975\b.dll",
        @"C:\Regress317975\v2\b.dll",
        @"c:\Regress313086\mscorlib.dll",
        @"c:\V1Control\MyDeviceControlAssembly.dll",
        @"c:\V1ControlSP1\MyDeviceControlAssembly.dll",
        @"C:\Regress339786\FolderA\a.dll",
        @"C:\Regress339786\FolderA\c.dll", // v1 of c
        @"C:\Regress339786\FolderB\b.dll",
        @"C:\Regress339786\FolderB\c.dll", // v2 of c
        @"c:\OldClrBug\MyFileLoadExceptionAssembly.dll",
        @"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll",
        @"c:\Regress563286\DependsOnBadImage.dll",
        @"C:\Regress407623\CrystalReportsAssembly.dll",
        @"C:\Regress435487\microsoft.build.engine.dll",
        @"C:\Regress313747\Microsoft.Office.Interop.Excel.dll",
        @"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll",
        Regress442570_ADllPath,
        Regress442570_BDllPath,
        Regress454863_ADllPath,
        Regress454863_BDllPath,
        @"C:\Regress393931\A.metadata_dll",
        @"c:\Regress387218\A.dll",
        @"c:\Regress387218\B.dll",
        @"c:\Regress387218\v1\D.dll",
        @"c:\Regress387218\v2\D.dll",
        @"c:\Regress390219\A.dll",
        @"c:\Regress390219\B.dll",
        @"c:\Regress390219\v1\D.dll",
        @"c:\Regress390219\v2\D.dll",
        @"c:\Regress315619\A\MyAssembly.dll",
        @"c:\Regress315619\B\MyAssembly.dll",
        @"c:\SGenDependeicies\mycomponent.dll",
        @"c:\SGenDependeicies\mycomponent.XmlSerializers.dll",
        @"c:\SGenDependeicies\mycomponent2.dll",
        @"c:\SGenDependeicies\mycomponent2.XmlSerializers.dll",
        @"c:\Regress315619\A\MyAssembly.dll",
        @"c:\Regress315619\B\MyAssembly.dll",
        @"c:\MyRedist\MyRedistRootAssembly.dll",
        @"c:\MyRedist\MyOtherAssembly.dll",
        @"c:\MyRedist\MyThirdAssembly.dll",
        // ==[Related File Extensions Testing]================================================================================================
        AssemblyFolder_SomeAssemblyDllPath,
        Path.Combine(AssemblyFolder_RootPath, "SomeAssembly.pdb"),
        Path.Combine(AssemblyFolder_RootPath, "SomeAssembly.xml"),
        Path.Combine(AssemblyFolder_RootPath, "SomeAssembly.pri"),
        Path.Combine(AssemblyFolder_RootPath, "SomeAssembly.licenses"),
        Path.Combine(AssemblyFolder_RootPath, "SomeAssembly.config"),
        // ==[Related File Extensions Testing]================================================================================================

        // ==[Unification Testing]============================================================================================================
        // @"C:\MyComponents\v0.5\UnifyMe.dll",                                 // For unification testing, a version that doesn't exist.
        UnifyMeDll_V10Path,
        UnifyMeDll_V20Path,
        UnifyMeDll_V30Path,
        // @"C:\MyComponents\v4.0\UnifyMe.dll",
        Path.Combine(MyApp_V05Path, "DependsOnUnified.dll"),
        Path.Combine(MyApp_V10Path, "DependsOnUnified.dll"),
        Path.Combine(MyApp_V20Path, "DependsOnUnified.dll"),
        Path.Combine(MyApp_V30Path, "DependsOnUnified.dll"),
        Path.Combine(MyAppRootPath, "DependsOnWeaklyNamedUnified.dll"),
        Path.Combine(MyApp_V10Path, "DependsOnEverettSystem.dll"),
        @"C:\Framework\Everett\System.dll",
        @"C:\Framework\Whidbey\System.dll",
        // ==[Unification Testing]============================================================================================================

        // ==[Test assemblies reference higher versions than the current target framework=====================================================
        Path.Combine(MyComponentsMiscPath, "DependsOnOnlyv4Assemblies.dll"),  // Only depends on 4.0.0 assemblies
        Path.Combine(MyComponentsMiscPath, "ReferenceVersion9.dll"), // Is in redist list and is a 9.0 assembly
        Path.Combine(MyComponentsMiscPath, "DependsOn9.dll"), // Depends on 9.0 assemblies
        Path.Combine(MyComponentsMiscPath, "DependsOn9Also.dll"), // Depends on 9.0 assemblies
        Path.Combine(MyComponents10Path, "DependsOn9.dll"), // Depends on 9.0 assemblies
        Path.Combine(MyComponents20Path, "DependsOn9.dll"), // Depends on 9.0 assemblies
        Regress444809_ADllPath,
        Regress444809_V2_ADllPath,
        Regress444809_BDllPath,
        Regress444809_CDllPath,
        Regress444809_DDllPath,
        MyComponents40ComponentDependsOnOnlyv4AssembliesDllPath,
        @"C:\Regress714052\MSIL\a.dll",
        @"C:\Regress714052\X86\a.dll",
        @"C:\Regress714052\NONE\a.dll",
        @"C:\Regress714052\Mix\a.dll",
        @"C:\Regress714052\Mix\a.winmd",
        @"C:\Regress714052\MSIL\b.dll",
        @"C:\Regress714052\X86\b.dll",
        @"C:\Regress714052\NONE\b.dll",
        @"C:\Regress714052\Mix\b.dll",
        @"C:\Regress714052\Mix\b.winmd",

        Path.Combine(MyComponentsRootPath, "V.dll"),
        Path.Combine(MyComponents2RootPath, "W.dll"),
        Path.Combine(MyComponentsRootPath, "X.dll"),
        Path.Combine(MyComponentsRootPath, "X.pdb"),
        Path.Combine(MyComponentsRootPath, "Y.dll"),
        Path.Combine(MyComponentsRootPath, "Z.dll"),

        Path.Combine(MyComponentsRootPath, "Microsoft.Build.dll"),
        Path.Combine(MyComponentsRootPath, "DependsOnMSBuild12.dll"),

        // WinMD sample files
        @"C:\WinMD\v4\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 4
        @"C:\WinMD\v255\mscorlib.dll",  // Fake 4.0 mscorlib so we can actually resolve it for one of the tests. With a version of 255
        @"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll",
        @"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll",
        @"C:\WinMD\SampleWindowsRuntimeAndCLR.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeAndCLR.dll",
        @"C:\WinMD\SampleWindowsRuntimeAndOther.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeOnly.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeOnly.dll",
        @"C:\WinMD\SampleWindowsRuntimeOnly.pri",
        @"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeOnly3.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeOnly4.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeReferencingSystem.Winmd",
        @"C:\WinMD\SampleWindowsRuntimeReferencingSystemDNE.Winmd",
        @"C:\WinMD\SampleClrOnly.Winmd",
        @"C:\WinMD\SampleBadWindowsRuntime.Winmd",
        @"C:\WinMD\WinMDWithVersion255.Winmd",
        @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.Winmd",
        @"C:\WinMDArchVerification\DependsOnInvalidPeHeader.dll",
        @"C:\WinMDArchVerification\DependsOnAmd64.Winmd",
        @"C:\WinMDArchVerification\DependsOnAmd64.dll",
        @"C:\WinMDArchVerification\DependsOnArm.Winmd",
        @"C:\WinMDArchVerification\DependsOnArm.dll",
        @"C:\WinMDArchVerification\DependsOnArmv7.Winmd",
        @"C:\WinMDArchVerification\DependsOnArmv7.dll",
        @"C:\WinMDArchVerification\DependsOnX86.Winmd",
        @"C:\WinMDArchVerification\DependsOnX86.dll",
        @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.Winmd",
        @"C:\WinMDArchVerification\DependsOnAnyCPUUnknown.dll",
        @"C:\WinMDArchVerification\DependsOnIA64.Winmd",
        @"C:\WinMDArchVerification\DependsOnIA64.dll",
        @"C:\WinMDArchVerification\DependsOnUnknown.Winmd",
        @"C:\WinMDArchVerification\DependsOnUnknown.dll",
        @"C:\WinMDLib\LibWithWinmdAndNoDll.lib",
        @"C:\WinMDLib\LibWithWinmdAndNoDll.pri",
        @"C:\WinMDLib\LibWithWinmdAndNoDll.Winmd",
        @"C:\FakeSDK\References\Debug\X86\DebugX86SDKWinMD.Winmd",
        @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKWinMD.Winmd",
        @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKWinMD.Winmd",
        @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKWinMD.Winmd",
        @"C:\FakeSDK\References\Debug\X86\DebugX86SDKRA.dll",
        @"C:\FakeSDK\References\Debug\Neutral\DebugNeutralSDKRA.dll",
        @"C:\FakeSDK\References\CommonConfiguration\x86\x86SDKRA.dll",
        @"C:\FakeSDK\References\CommonConfiguration\Neutral\NeutralSDKRA.dll",
        @"C:\FakeSDK\References\Debug\X86\SDKReference.dll",
        @"C:\DirectoryContainsOnlyDll\a.dll",
        @"C:\DirectoryContainsdllAndWinmd\b.dll",
        @"C:\DirectoryContainsdllAndWinmd\c.winmd",
        @"C:\DirectoryContainstwoWinmd\a.winmd",
        @"C:\DirectoryContainstwoWinmd\c.winmd",
        SystemRuntimeDllPath,
        PortableDllPath,
        NetstandardLibraryDllPath,
        NetstandardDllPath,
        @"C:\SystemRuntime\Regular.dll",
        DependsOnNuGet_ADllPath,
        NugetCache_N_Lib_NDllPath,
        @"C:\DirectoryTest\A.dll",
        @"C:\DirectoryTest\B.dll",
    ];

    /// <summary>
    ///  Assembly name mappings for GetAssemblyName (path → assembly name string).
    /// </summary>
    public static readonly FrozenDictionary<string, string> AssemblyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Path.Combine(FrameworksPath, "DependsOnFoo45Framework.dll")] = "DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral",
        [Path.Combine(FrameworksPath, "DependsOnFoo4Framework.dll")] = "DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [Path.Combine(FrameworksPath, "DependsOnFoo35Framework.dll")] = "DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral",
        [@"c:\Regress315619\A\MyAssembly.dll"] = "MyAssembly, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [@"c:\Regress315619\B\MyAssembly.dll"] = "MyAssembly, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Regress442570_ADllPath] = "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
        [@"c:\Regress387218\v1\D.dll"] = "D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Regress442570_BDllPath] = "B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
        [@"c:\Regress387218\v2\D.dll"] = "D, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [@"c:\Regress390219\v1\D.dll"] = "D, Version=1.0.0.0, Culture=fr, PublicKeyToken=b77a5c561934e089",
        [@"c:\Regress390219\v2\D.dll"] = "D, Version=2.0.0.0, Culture=en, PublicKeyToken=b77a5c561934e089",
        [@"c:\MyStronglyNamed\A.dll"] = "A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089",
        [@"c:\MyNameMismatch\Foo.dll"] = "A, Version=2.0.0.0, Culture=neUtral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(Path.GetTempPath(), @"RawFileNameRelative\System.Xml.dll")] = AssemblyRef.SystemXml,
        [Path.Combine(Path.GetTempPath(), @"RelativeAssemblyFiles\System.Xml.dll")] = AssemblyRef.SystemXml,
        [Path.Combine(MyVersion20Path, "System.XML.dll")] = AssemblyRef.SystemXml,
        [Path.Combine(MyProjectPath, "System.Xml.dll")] = AssemblyRef.SystemXml,
        [Path.Combine(MyProjectPath, "System.Data.dll")] = "System.Data, Version=2.0.0.0, Culture=neutral, PublicKeyToken=A77a5c561934e089",
        [Path.Combine(MyVersion20Path, "System.dll")] = "System, VeRSion=2.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyVersion40Path, "System.dll")] = "System, VeRSion=4.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyVersion90Path, "System.dll")] = "System, VeRSion=9.0.0.0, Culture=neutRAl, PublicKeyToken=b77a5c561934e089",
        [@"C:\Framework\Everett\System.dll"] = "System, Version=1.0.5000.0, Culture=neutral, PublICKeyToken=" + AssemblyRef.EcmaPublicKey,
        [@"C:\Framework\Whidbey\System.dll"] = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey,
        [Path.Combine(MyVersion20Path, "System.Data.dll")] = AssemblyRef.SystemData,
        [UnifyMeDll_V10Path] = "UnifyMe, Version=1.0.0.0, Culture=nEUtral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL",
        [Path.Combine(MyApp_V10Path, "DependsOnEverettSystem.dll")] = "DependsOnEverettSystem, VersION=1.0.5000.0, Culture=neutral, PublicKeyToken=feedbeadbadcadbe",
        [Path.Combine(MyApp_V05Path, "DependsOnUnified.dll")] = "DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\Regress339786\FolderA\C.dll"] = "C, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [@"C:\Regress339786\FolderB\C.dll"] = "C, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyApp_V10Path, "DependsOnUnified.dll")] = "DependsOnUnified, VERSion=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyApp_V20Path, "DependsOnUnified.dll")] = "DependsOnUnified, VeRSIon=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyApp_V30Path, "DependsOnUnified.dll")] = "DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKEYToken=b77a5c561934e089",
        [UnifyMeDll_V20Path] = "UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyTOKEn=b77a5c561934e089",
        [UnifyMeDll_V30Path] = "UnifyMe, Version=3.0.0.0, Culture=neutral, PublICkeyToken=b77a5c561934e089",
        [@"C:\Regress317975\a.dll"] = "A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
        [@"C:\Regress317975\b.dll"] = "B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
        [@"C:\Regress317975\v2\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [MyComponents40ComponentDependsOnOnlyv4AssembliesDllPath] = "DependsOnOnlyv4Assemblies, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
        [Path.Combine(MyComponentsMiscPath, "ReferenceVersion9.dll")] = "ReferenceVersion9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
        [Path.Combine(MyComponentsMiscPath, "DependsOn9.dll")] = "DependsOn9, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
        [Path.Combine(MyComponentsMiscPath, "DependsOn9Also.dll")] = "DependsOn9Also, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
        [Path.Combine(MyComponents10Path, "DependsOn9.dll")] = "DependsOn9, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
        [Path.Combine(MyComponents20Path, "DependsOn9.dll")] = "DependsOn9, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b17a5c561934e089",
        [Regress444809_ADllPath] = "A, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Regress444809_V2_ADllPath] = "A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Regress444809_BDllPath] = "B, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Regress444809_CDllPath] = "C, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Regress444809_DDllPath] = "D, Version=1.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "X.pdb")] = "X, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null",
        [@"C:\Regress714052\X86\a.dll"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
        [@"C:\Regress714052\Mix\a.dll"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
        [@"C:\Regress714052\Mix\a.winmd"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
        [@"C:\Regress714052\MSIL\a.dll"] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
        [@"C:\Regress714052\None\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [@"C:\Regress714052\X86\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
        [@"C:\Regress714052\Mix\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=X86",
        [@"C:\Regress714052\Mix\b.winmd"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
        [@"C:\Regress714052\MSIL\b.dll"] = "B, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL",
        [Path.Combine(MyComponentsRootPath, "V.dll")] = "V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponents2RootPath, "W.dll")] = "W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "X.dll")] = "X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "Z.dll")] = "Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "Y.dll")] = "Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "Microsoft.Build.dll")] = "Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        [Path.Combine(MyComponentsRootPath, "DependsOnMSBuild12.dll")] = "DependsOnMSBuild12, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
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
        [DependsOnNuGet_ADllPath] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [NugetCache_N_Lib_NDllPath] = "N, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Paths for which GetAssemblyName returns null (mscorlib with no metadata).
    /// </summary>
    public static readonly FrozenSet<string> AssemblyNameNullPaths = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        [
            @"c:\Regress313086\mscorlib.dll",
            Path.Combine(MyProjectPath, "mscorlib.dll"),
            Path.Combine(MyVersion20Path, "mscorlib.dll"),
            Path.Combine(MyVersionPocket20Path, "mscorlib.dll")
        ]);

    /// <summary>
    ///  Paths that return empty dependency arrays from GetDependencies.
    /// </summary>
    public static readonly FrozenSet<string> DependenciesEmptyPaths = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        [
            @"c:\Regress313086\mscorlib.dll",
            @"MyRelativeAssembly.dll",
            MyLibraries_V1_E_EDllPath,
            Path.Combine(MyComponents2RootPath, "W.dll"),
            Path.Combine(MyComponentsRootPath, "Z.dll"),
            Path.Combine(MyComponentsRootPath, "Microsoft.Build.dll"),
            @"C:\DirectoryContainsdllAndWinmd\c.winmd",
            @"C:\DirectoryContainstwoWinmd\c.winmd",
            @"C:\DirectoryTest\B.dll",
            Path.Combine(MyVersion20Path, "mscorlib.dll"),
            Path.Combine(MyVersionPocket20Path, "mscorlib.dll")
        ]);

    /// <summary>
    ///  Single dependency mappings (path → dependency assembly name string).
    /// </summary>
    public static readonly FrozenDictionary<string, string> SingleDependencies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [Path.Combine(FrameworksPath, "IndirectDependsOnFoo4Framework.dll")] = "DependsOnFoo4Framework, Version=4.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [Path.Combine(FrameworksPath, "IndirectDependsOnFoo45Framework.dll")] = "DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral",
        [Path.Combine(FrameworksPath, "IndirectDependsOnFoo35Framework.dll")] = "DependsOnFoo35Framework, Version=3.5.0.0, PublicKeyToken=null, Culture=Neutral",
        [Regress454863_ADllPath] = "B, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [Regress442570_BDllPath] = " A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\Regress313747\Microsoft.Office.Interop.Excel.dll"] = " Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c",
        [@"C:\Regress313747\MS.Internal.Test.Automation.Office.Excel.dll"] = " Office, Version=12.0.0.0, Culture=neutral, PublicKeyToken=94de0004b6e3fcc5",
        [@"c:\Regress387218\A.dll"] = "D, Version=1.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [@"c:\Regress387218\B.dll"] = "D, Version=2.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [@"c:\Regress390219\A.dll"] = "D, Version=1.0.0.0, PublicKeyToken=b77a5c561934e089, Culture=fr",
        [@"c:\Regress390219\B.dll"] = "D, Version=2.0.0.0,  PublicKeyToken=b77a5c561934e089, Culture=en",
        [@"c:\OldClrBug\DependsMyFileLoadExceptionAssembly.dll"] = "MyFileLoadExceptionAssembly, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [@"c:\Regress563286\DependsOnBadImage.dll"] = "BadImage, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [Path.Combine(MyVersion20Path, "System.dll")] = "mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\WinMD\DotNetAssemblyDependsOnWinMD.dll"] = "SampleWindowsRuntimeOnly, Version=1.0.0.0",
        [@"C:\WinMD\DotNetAssemblyDependsOn255WinMD.dll"] = "WinMDWithVersion255, Version=255.255.255.255",
        [@"C:\WinMD\SampleWindowsRuntimeAndClr.Winmd"] = "mscorlib, Version=4.0.0.0, Culture=Neutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\WinMD\SampleWindowsRuntimeOnly.Winmd"] = "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\WinMD\WinMDWithVersion255.Winmd"] = "mscorlib, Version=255.255.255.255, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\WinMD\SampleWindowsRuntimeOnly2.Winmd"] = "SampleWindowsRuntimeOnly, Version=1.0.0.0",
        [Path.Combine(MyAppRootPath, "DependsOnSimpleA.dll")] = "A, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral",
        [@"C:\Regress312873\b.dll"] = "A, Version=0.0.0.0, PublickEyToken=null, Culture=Neutral",
        [@"C:\Regress339786\FolderA\a.dll"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
        [@"C:\Regress339786\FolderB\b.dll"] = "C, Version=2.0.0.0, PublickEyToken=null, Culture=Neutral",
        [@"C:\Regress317975\a.dll"] = "B, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
        [@"C:\myassemblies\My.Assembly.dll"] = "mscorlib, Version=2.0.0.0, Culture=NEUtraL, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyComponentsRootPath, "MyGrid.dll")] = "mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089",
        [@"C:\MyRawDropControls\MyRawDropControl.dll"] = "mscorlib, VeRsIon=2.0.0.0, Culture=neuTRal, PublicKeyToken=b77a5c561934e089",
        [MyLibraries_ADllPath] = "D, Version=1.0.0.0, CuLtUrE=neutral, PublicKeyToken=aaaaaaaaaaaaaaaa",
        [MyLibraries_TDllPath] = "D, VeRsIon=1.0.0.0, Culture=neutral, PublicKeyToken=bbbbbbbbbbbbbbbb",
        [MyLibraries_V1_DDllPath] = "E, VERSIOn=0.0.0.0, Culture=neutral, PublicKeyToken=null",
        [MyLibraries_V2_DDllPath] = "E, Version=0.0.0.0, Culture=neutRAL, PUblicKeyToken=null",
        [Path.Combine(MyApp_V05Path, "DependsOnWeaklyNamedUnified.dll")] = "UnifyMe, Version=0.0.0.0, PUBLICKeyToken=null, CuLTURE=Neutral",
        [Path.Combine(MyApp_V10Path, "DependsOnEverettSystem.dll")] = "System, VeRsiON=1.0.5000.0, Culture=neutral, PublicKeyToken=" + AssemblyRef.EcmaPublicKey,
        [Path.Combine(MyApp_V05Path, "DependsOnUnified.dll")] = "UnifyMe, Version=0.5.0.0, CuLTUre=neUTral, PubLICKeyToken=b77a5c561934e089",
        [Path.Combine(MyApp_V10Path, "DependsOnUnified.dll")] = "UNIFyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyApp_V20Path, "DependsOnUnified.dll")] = "UniFYme, Version=2.0.0.0, Culture=NeutraL, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyApp_V30Path, "DependsOnUnified.dll")] = "UnIfyMe, Version=3.0.0.0, Culture=nEutral, PublicKEyToken=b77a5c561934e089",
        [MyComponents40ComponentDependsOnOnlyv4AssembliesDllPath] = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyComponentsMiscPath, "DependsOn9Also.dll")] = "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyComponents10Path, "DependsOn9.dll")] = "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyComponents20Path, "DependsOn9.dll")] = "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
        [Regress444809_BDllPath] = "A, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Regress444809_DDllPath] = "A, Version=20.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "V.dll")] = "W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "X.dll")] = "Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "Y.dll")] = "Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null",
        [Path.Combine(MyComponentsRootPath, "DependsOnMSBuild12.dll")] = "Microsoft.Build, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
        [Path.Combine(MyVersion40Path, "System.dll")] = "msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089",
        [Path.Combine(MyVersion90Path, "System.dll")] = "msCORlib, Version=2.0.0.0, Culture=NEutral, PublicKeyToken=b77a5c561934e089",
        [@"C:\DirectoryContainsOnlyDll\a.dll"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
        [@"C:\DirectoryContainsdllAndWinmd\b.dll"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
        [@"C:\DirectoryContainstwoWinmd\a.winmd"] = "C, Version=1.0.0.0, PublickEyToken=null, Culture=Neutral",
        [DependsOnNuGet_ADllPath] = "N, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Multiple dependency mappings (path → array of dependency assembly name strings).
    /// </summary>
    public static readonly FrozenDictionary<string, string[]> MultipleDependencies = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
        [MyLibraries_BDllPath] =
        [
            "D, Version=2.0.0.0, Culture=neutral, PuBlIcKeYToken=aaaaaaaaaaaaaaaa",
            "G, Version=2.0.0.0, Culture=neutral, PuBlIcKeYToken=aaaaaaaaaaaaaaaa"
        ],
        [Path.Combine(MyComponentsMiscPath, "ReferenceVersion9.dll")] =
        [
            "mscorlib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            "RandomAssembly, Version=9.0.0.0, Culture=neutral, PublicKeyToken=c77a5c561934e089"
        ],
        [Path.Combine(MyComponentsMiscPath, "DependsOn9.dll")] =
        [
            "System, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            "System.Data, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        ],
        [Regress444809_CDllPath] =
        [
            "B, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            "A, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"
        ],
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Target framework attribute mappings for GetTargetFrameworkAttribute.
    /// </summary>
    public static readonly FrozenDictionary<string, FrameworkName> TargetFrameworks = new Dictionary<string, FrameworkName>(StringComparer.OrdinalIgnoreCase)
    {
        [Path.Combine(FrameworksPath, "DependsOnFoo4Framework.dll")] = new("FoO, Version=v4.0"),
        [Path.Combine(FrameworksPath, "DependsOnFoo45Framework.dll")] = new("FoO, Version=v4.5"),
        [Path.Combine(FrameworksPath, "DependsOnFoo35Framework.dll")] = new("FoO, Version=v3.5"),
        [Path.Combine(FrameworksPath, "IndirectDependsOnFoo4Framework.dll")] = new("FoO, Version=v4.0"),
        [Path.Combine(FrameworksPath, "IndirectDependsOnFoo45Framework.dll")] = new("FoO, Version=v4.0"),
        [Path.Combine(FrameworksPath, "IndirectDependsOnFoo35Framework.dll")] = new("FoO, Version=v4.0"),
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Runtime version mappings for GetRuntimeVersion.
    /// </summary>
    public static readonly FrozenDictionary<string, string> RuntimeVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    ///  PE header machine type mappings for ReadMachineTypeFromPEHeader.
    /// </summary>
    public static readonly FrozenDictionary<string, ushort> MachineTypes = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    ///  WinMD paths that return true from IsWinMDFile (isManagedWinMD = false).
    /// </summary>
    public static readonly FrozenSet<string> WinMDPaths = FrozenSet.Create(
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

    /// <summary>
    ///  GAC path mappings for specific assembly names. Value is path, or null for assemblies not in GAC.
    /// </summary>
    public static readonly FrozenDictionary<string, string> GacPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["V, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = null,
        ["W, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = @"C:\MyComponents2\W.dll",
        ["X, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = @"C:\MyComponents\X.dll",
        ["Y, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = null,
        ["Z, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null"] = null,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Registry subkey names for CurrentUser hive.
    /// </summary>
    public static readonly FrozenDictionary<string, string[]> RegistrySubKeyNames_CurrentUser = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
    ///  Registry subkey names for LocalMachine hive.
    /// </summary>
    public static readonly FrozenDictionary<string, string[]> RegistrySubKeyNames_LocalMachine = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>
    ///  Registry subkey default values for CurrentUser hive.
    /// </summary>
    public static readonly FrozenDictionary<string, string> RegistrySubKeyDefaultValue_CurrentUser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlA"] = @"C:\MyComponentsA",
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\ZControlB"] = @"C:\MyComponentsB",
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.GridControl.1.0"] = @"C:\MyComponents",
        [@"Software\Microsoft\.NetFramework\v2.0.x86chk\AssemblyFoldersEx\RawDropControls"] = @"C:\MyRawDropControls",
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyHKLMControl.1.0"] = @"C:\MyComponents\HKCU Components",
        [@"Software\Microsoft\.NetFramework\v3.0\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0"] = MyComponentsV30Path,
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithFutureTargetNDPVersion.1.0"] = MyComponentsV20Path,
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0"] = MyComponentsV20Path,
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0"] = MyComponentsV20Path,
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp1"] = @"C:\MyComponentServicePack1",
        [@"Software\Microsoft\.NetFramework\v2.0.50727\AssemblyFoldersEx\Infragistics.MyControlWithServicePack.1.0\sp2"] = @"C:\MyComponentServicePack2",
        [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyNDP1Control.1.0"] = MyComponentsV10Path,
        [@"Software\Microsoft\.NetFramework\v1.0\AssemblyFoldersEx\Infragistics.MyControlWithPastTargetNDPVersion.1.0"] = MyComponentsV10Path,
        [@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl"] = @"C:\V1Control",
        [@"SOFTWARE\Microsoft\.NETCompactFramework\v2.0.3600\PocketPC\AssemblyFoldersEx\AFETestDeviceControl\1234"] = @"C:\V1ControlSP1",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///  Registry subkey default values for LocalMachine hive.
    /// </summary>
    public static readonly FrozenDictionary<string, string> RegistrySubKeyDefaultValue_LocalMachine = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
}
