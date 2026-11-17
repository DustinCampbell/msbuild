// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Build.Utilities;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using ProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    /// <summary>
    ///  Verifies that an empty registry root is rejected.
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestEmptyRegistryRoot()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(
                registryRoot: string.Empty,
                targetFrameworkVersion: "v3.0",
                registryKeySuffix: "AssemblyFoldersEx",
                osVersion: null,
                platform: null,
                targetProcessorArchitecture: ProcessorArchitecture.MSIL));

    /// <summary>
    ///  Verifies that a <see langword="null"/> registry root is rejected.
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoListTestNullRegistryRoot()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(
                registryRoot: null,
                targetFrameworkVersion: "v3.0",
                registryKeySuffix: "AssemblyFoldersEx",
                osVersion: null,
                platform: null,
                targetProcessorArchitecture: ProcessorArchitecture.MSIL));

    /// <summary>
    ///  Verifies that an empty registry suffix is rejected.
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestEmptyRegistrySuffix()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(
                registryRoot: @"SOFTWARE\Microsoft\.UnitTest",
                targetFrameworkVersion: "v3.0",
                registryKeySuffix: string.Empty,
                osVersion: null,
                platform: null,
                targetProcessorArchitecture: ProcessorArchitecture.MSIL));

    /// <summary>
    ///  Verifies that a <see langword="null"/> registry suffix is rejected.
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestNullRegistrySuffix()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(
                registryRoot: @"SOFTWARE\Microsoft\.UnitTest",
                targetFrameworkVersion: "v3.0",
                registryKeySuffix: null,
                osVersion: null,
                platform: null,
                targetProcessorArchitecture: ProcessorArchitecture.MSIL));

    /// <summary>
    ///  Verifies that an empty target framework version is rejected.
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestEmptyTargetRuntime()
        => Should.Throw<ArgumentException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(
                registryRoot: @"SOFTWARE\Microsoft\.UnitTest",
                targetFrameworkVersion: string.Empty,
                registryKeySuffix: "AssemblyFoldersEx",
                osVersion: null,
                platform: null,
                targetProcessorArchitecture: ProcessorArchitecture.MSIL));

    /// <summary>
    ///  Verifies that a <see langword="null"/> target framework version is rejected.
    /// </summary>
    [Fact]
    public void GetAssemblyFoldersExInfoTestNullTargetRuntimeVersion()
        => Should.Throw<ArgumentNullException>(() =>
            ToolLocationHelper.GetAssemblyFoldersExInfo(
                registryRoot: @"SOFTWARE\Microsoft\.UnitTest",
                targetFrameworkVersion: null,
                registryKeySuffix: "AssemblyFoldersEx",
                osVersion: null,
                platform: null,
                targetProcessorArchitecture: ProcessorArchitecture.MSIL));

    /// <summary>
    ///  Verifies that AssemblyFoldersEx entries are discovered in the expected precedence order.
    /// </summary>
    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void GetAssemblyFoldersExInfoTest()
    {
        TransientTestRegistryKey registryKey = Environment.CreateRegistryKey();
        using RegistryKey folderKey = registryKey.Key.CreateSubKey(@"v2.0.3600\AssemblyFoldersEx\Component1");
        folderKey.SetValue("", @"C:\V1Control");
        using RegistryKey servicePackKey = registryKey.Key.CreateSubKey(@"v2.0.3600\AssemblyFoldersEx\Component2");
        servicePackKey.SetValue("", @"C:\V1Control2");

        IList<AssemblyFoldersExInfo> directories = ToolLocationHelper.GetAssemblyFoldersExInfo(
            registryKey.SubKeyPath,
            targetFrameworkVersion: "v3.0",
            registryKeySuffix: "AssemblyFoldersEx",
            osVersion: null,
            platform: null,
            targetProcessorArchitecture: ProcessorArchitecture.MSIL);

        directories.ShouldHaveItems(
            directory => directory.DirectoryPath.ShouldBe(@"C:\V1Control2", StringCompareShould.IgnoreCase),
            directory => directory.DirectoryPath.ShouldBe(@"C:\V1Control", StringCompareShould.IgnoreCase));
    }
}

#endif
