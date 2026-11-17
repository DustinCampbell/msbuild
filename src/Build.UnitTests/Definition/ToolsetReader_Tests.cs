// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REGISTRY_TOOLSETS && FEATURE_SYSTEM_CONFIGURATION

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using InternalUtilities = Microsoft.Build.Internal.Utilities;

namespace Microsoft.Build.UnitTests.Definition;

/// <summary>
///  Tests for <see cref="ToolsetReader"/> and its derived classes.
/// </summary>
/// <param name="output">The output helper for the test.</param>
[SupportedOSPlatform("windows")]
public class ToolsetReaderTests(ITestOutputHelper output) : TestBase(output)
{
#nullable disable
    // Owns the test registry key passed to ToolsetRegistryReader through MockRegistryKey.
    // The key is created solely for this test under HKCU.
    private TransientTestRegistryKey _registryKey = null;

    // Subkey for the current MSBuild assembly version.
    private RegistryKey _currentVersionRegistryKey = null;

    // Subkey "ToolsVersions"
    private RegistryKey _toolsVersionsRegistryKey = null;
#nullable enable

    protected override ValueTask InitializeAsync()
    {
        ToolsetConfigurationReaderTestHelper.CleanUp();
        RegisterCleanup(ToolsetConfigurationReaderTestHelper.CleanUp);

        _registryKey = Environment.CreateRegistryKey();
        _currentVersionRegistryKey = _registryKey.Key.CreateSubKey(Constants.AssemblyVersion);
        RegisterDisposable(_currentVersionRegistryKey);
        _toolsVersionsRegistryKey = _registryKey.Key.CreateSubKey("ToolsVersions");
        RegisterDisposable(_toolsVersionsRegistryKey);

        // Unset VisualStudioVersion for the duration of the test; Environment restores its original value.
        Environment.SetEnvironmentVariable("VisualStudioVersion", null);
        return default;
    }

    private ToolsetRegistryReader GetStandardRegistryReader()
    {
#pragma warning disable CA2000 // TestBase owns disposal through RegisterDisposable; the returned reader still needs the key.
        var registryKey = new MockRegistryKey(_registryKey.SubKeyPath);
#pragma warning restore CA2000
        RegisterDisposable(registryKey);

        using var collection = new ProjectCollection();
        return new ToolsetRegistryReader(collection.EnvironmentProperties, globalProperties: [], registryKey);
    }

    private ToolsetConfigurationReader GetStandardConfigurationReader()
    {
        using var collection = new ProjectCollection();
        return new ToolsetConfigurationReader(
            collection.EnvironmentProperties,
            globalProperties: [],
            ToolsetConfigurationReaderTestHelper.ReadApplicationConfigurationTest);
    }

    /// <summary>
    ///  Verifies that no toolsets or default tools version are read when the toolset section is not registered.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "The machine.config is only present on Windows.")]
    public void GetToolsetDataFromConfiguration_SectionNotRegisteredInConfigFile()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="2.0">
                    <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out string msbuildOverrideTasksPath,
            out _);

        msbuildOverrideTasksPath.ShouldBeNull();
        defaultToolsVersion.ShouldBeNull();
        toolsets.ShouldBeEmpty();
    }

    #region "Reading from application configuration file tests"

    /// <summary>
    ///  Verifies that configuration defines toolsets, their tools paths, and the default and override settings.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_Basic()
    {
        string v2Folder = NativeMethodsShared.IsWindows
            ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
            : Path.Combine(NativeMethodsShared.FrameworkBasePath, "2.0");
        string v4Folder = NativeMethodsShared.IsWindows
            ? @"D:\windows\Microsoft.NET\Framework\v4.0.x86ret"
            : Path.Combine(NativeMethodsShared.FrameworkBasePath, "4.0");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0" msbuildOverrideTasksPath="c:\Cat" DefaultOverrideToolsVersion="4.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{v2Folder}"/>
                    </toolset>
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="{v4Folder}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out string msbuildOverrideTasksPath,
            out string defaultOverrideToolsVersion);

        msbuildOverrideTasksPath.ShouldBe(@"c:\Cat");
        defaultOverrideToolsVersion.ShouldBe("4.0");
        defaultToolsVersion.ShouldBe("2.0");
        toolsets.Count.ShouldBe(2);
        toolsets.ShouldContain("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(v2Folder);
        });
        toolsets.ShouldContain("4.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(v4Folder);
        });
    }

    /// <summary>
    ///  Verifies that relative tools paths are expanded and resolved against the MSBuild tools directory.
    /// </summary>
    [WindowsOnlyFact]
    public void RelativePathInValue()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0" msbuildOverrideTasksPath="..\Foo">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildToolsPath" value="..{Path.DirectorySeparatorChar}foo"/>
                        <!-- derelativization occurs before comparing toolspath and binpath -->
                        <property name="MSBuildBinPath" value="..{Path.DirectorySeparatorChar}.{Path.DirectorySeparatorChar}foo"/>
                    </toolset>
                    <toolset toolsVersion="3.0">
                        <!-- properties are expanded before derelativization-->
                        <property name="MSBuildBinPath" value="$(DotDotSlash)bar"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [ProjectPropertyInstance.Create("DotDotSlash", $"..{Path.DirectorySeparatorChar}")],
            accumulateProperties: true,
            out string msbuildOverrideTasksPath,
            out _);

        string expected1 = Path.GetFullPath(Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "..", "foo"));
        string expected2 = Path.GetFullPath(Path.Combine(BuildEnvironmentHelper.Instance.CurrentMSBuildToolsDirectory, "..", "bar"));
        Output.WriteLine(toolsets["2.0"].ToolsPath);
        toolsets.ShouldContain("2.0", toolset => toolset.ToolsPath.ShouldBe(expected1));
        toolsets.ShouldContain("3.0", toolset => toolset.ToolsPath.ShouldBe(expected2));
        msbuildOverrideTasksPath.ShouldBe(@"..\Foo");
    }

    /// <summary>
    ///  Verifies that an invalid relative tools path is retained without throwing, matching invalid absolute paths.
    /// </summary>
    [WindowsOnlyFact]
    public void InvalidRelativePath()
    {
        if (NativeMethodsShared.IsLinux)
        {
            return; // "Cannot force invalid character name on Linux"
        }

        string invalidRelativePath = ".." + Path.DirectorySeparatorChar + ":|invalid|";
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0" msbuildOverrideTasksPath="">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{invalidRelativePath}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out string msbuildOverrideTasksPath,
            out _);

        // Don't crash (consistent with invalid absolute path)
        toolsets.ShouldContain("2.0", toolset => toolset.ToolsPath.ShouldBe(invalidRelativePath));
        msbuildOverrideTasksPath.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that malformed configuration XML is reported as an invalid toolset definition.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_InvalidXmlFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""<INVALIDXML>""");

            ToolsetReader reader = GetStandardConfigurationReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that malformed toolset configuration is reported as an invalid toolset definition.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_InvalidConfigFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets>
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                        </toolsVersion>
                        <SOMEINVALIDTAG/>
                    </msbuildToolsets>
                </configuration>
                """);

            ToolsetReader reader = GetStandardConfigurationReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that an empty configuration file is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_FileEmpty()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile(string.Empty);

            ToolsetReader reader = GetStandardConfigurationReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that configuration-loading exceptions are reported as <see cref="InvalidToolsetDefinitionException"/>.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_ConfigurationExceptionThrown()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile(string.Empty, new ConfigurationErrorsException());

            ToolsetReader reader = GetStandardConfigurationReader();

            // this should throw ...
            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that configuration errors are reported as <see cref="InvalidToolsetDefinitionException"/>.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_ConfigurationErrorsExceptionThrown()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile(string.Empty, new ConfigurationErrorsException());

            ToolsetReader reader = GetStandardConfigurationReader();

            // this should throw ...
            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that toolsets are read without a default tools version when the configuration omits the default attribute.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_DefaultAttributeNotSpecified()
    {
        string v2Folder = NativeMethodsShared.IsWindows
            ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
            : Path.Combine(NativeMethodsShared.FrameworkBasePath, "2.0");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets msbuildOverrideTasksPath="C:\Cat">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{v2Folder}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out string msbuildOverrideTasksPath,
            out _);

        defaultToolsVersion.ShouldBeNull();
        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(v2Folder);
        });
        msbuildOverrideTasksPath.ShouldBe("C:\\Cat");
    }

    /// <summary>
    ///  Verifies that a default tools version without a matching toolset definition does not cause an error.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_DefaultToolsetUndefined()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="nonexistent">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        _ = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out _,
            out _);
    }

    /// <summary>
    ///  Verifies that a missing <c>msbuildToolsets</c> section produces no toolsets or default tools version.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_MSBuildToolsetsNodeNotPresent()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out _,
            out _);

        defaultToolsVersion.ShouldBeNull();
        toolsets.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that an empty <c>msbuildToolsets</c> section produces no toolsets or default tools version.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_EmptyMSBuildToolsetsNode()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets/>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out _,
            out _);

        defaultToolsVersion.ShouldBeNull();
        toolsets.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that specifying only a default tools version does not create any toolsets.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_OnlyDefaultSpecified()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0"/>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: true, out _, out _);

        toolsets.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that a single configured toolset and its default tools version are read.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_OneToolsVersionNode()
    {
        string v2Folder = NativeMethodsShared.IsWindows
                              ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
                              : Path.Combine(NativeMethodsShared.FrameworkBasePath, "2.0");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{v2Folder}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out _,
            out _);

        defaultToolsVersion.ShouldBe("2.0");
        toolsets.ShouldHaveSingleItem("2.0", toolset => toolset.ToolsPath.ShouldBe(v2Folder));
    }

    /// <summary>
    ///  Verifies that an empty <c>toolsVersion</c> attribute is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_ToolsVersionIsEmptyString()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="">
                            <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                        </toolset>
                        <toolset toolsVersion="4.0">
                            <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            ToolsetReader reader = GetStandardConfigurationReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that conflicting <c>MSBuildToolsPath</c> and <c>MSBuildBinPath</c> values are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_ToolsPathAndBinPathDiffer()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                            <property name="MSBuildToolsPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            ToolsetReader reader = GetStandardConfigurationReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that a configuration property with an empty name is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void BlankPropertyNameInConfigFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                            <property name="" value="foo"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            ToolsetReader reader = GetStandardConfigurationReader();

            // this should throw ...
            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that a registry toolset property with an empty name is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void BlankPropertyNameInRegistry()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");
            rk.SetValue("", "foo");

            // Should throw ...
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that a registry subtoolset property with an empty name is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void BlankPropertyNameInRegistrySubToolset()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");

            using RegistryKey subToolsetKey = rk.CreateSubKey("11.0");
            subToolsetKey.SetValue("", "foo");

            PropertyDictionary<ProjectPropertyInstance> globalProperties =
                [ProjectPropertyInstance.Create("VisualStudioVersion", "11.0")];

            // Should throw ...
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: globalProperties,
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that configuration properties can have empty values.
    /// </summary>
    [WindowsOnlyFact]
    public void BlankPropertyValueInConfigFile()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                        <property name="foo" value=""/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        // this should not throw ...
        _ = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out _,
            out _);
    }

    /// <summary>
    ///  Verifies that registry toolset properties can have empty values.
    /// </summary>
    [WindowsOnlyFact]
    public void BlankPropertyValueInRegistry()
    {
        using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        rk.SetValue("MSBuildBinPath", "someBinPath");
        rk.SetValue("foo", "");

        // Should not throw ...
        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Registry);
    }

    /// <summary>
    ///  Verifies that registry subtoolset properties can have empty values.
    /// </summary>
    [WindowsOnlyFact]
    public void BlankPropertyValueInRegistrySubToolset()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"c:\someBinPath" : "/someBinPath";
        using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        rk.SetValue("MSBuildBinPath", binPath);

        using RegistryKey subToolsetKey = rk.CreateSubKey("11.0");
        subToolsetKey.SetValue("foo", "");

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        // Should not throw ...
        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Registry);

        defaultToolsVersion.ShouldBe("2.0");
        toolsets.ShouldContain("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(binPath);
            toolset.SubToolsets.ShouldHaveSingleItem("11.0", subToolset =>
                subToolset.Properties.ShouldContain("foo", property => property.EvaluatedValue.ShouldBeEmpty()));
        });
    }

    /// <summary>
    ///  Verifies that invalid configuration property names are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void InvalidPropertyNameInConfigFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                            <property name="&amp;" value="foo"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            ToolsetReader reader = GetStandardConfigurationReader();

            // this should throw ...
            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: true,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that invalid registry toolset property names are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void InvalidPropertyNameInRegistry()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");
            rk.SetValue("foo|bar", "x");

            // Should throw ...
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that invalid registry subtoolset property names are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void InvalidPropertyNameInRegistrySubToolset()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");

            using RegistryKey subToolsetKey = rk.CreateSubKey("10.0");
            subToolsetKey.SetValue("MSBuildBinPath", "someBinPath");
            subToolsetKey.SetValue("foo|bar", "x");

            // Should throw ...
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that a configured toolset with an empty <c>MSBuildBinPath</c> is ignored.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_PropertyValueIsEmptyString1()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value=""/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: true, out _, out _);

        toolsets.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that an ordinary configuration property retains its empty value.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_PropertyValueIsEmptyString2()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildToolsPath" value="xxx"/>
                        <property name="foo" value=""/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: true, out _, out _);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
            toolset.Properties.ShouldHaveSingleItem("foo", property => property.EvaluatedValue.ShouldBeEmpty()));
    }

    /// <summary>
    ///  Verifies that XML entities are decoded in configured tools versions and property values.
    /// </summary>
    /// <remarks>
    ///  The configuration APIs decode XML entities before the toolset definitions are read.
    /// </remarks>
    [WindowsOnlyFact]
    public void GetToolsetDataFromConfiguration_XmlEscapedCharacters()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2&gt;.0">
                    <toolset toolsVersion="2&gt;.0">
                        <property name="MSBuildBinPath" value="x"/>
                        <property name="foo" value="some&gt;value"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        ToolsetReader reader = GetStandardConfigurationReader();

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets,
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: true,
            out _,
            out _);

        defaultToolsVersion.ShouldBe("2>.0");
        toolsets.ShouldHaveSingleItem("2>.0", toolset =>
            toolset.Properties.ShouldContain("foo", property => property.EvaluatedValue.ShouldBe(@"some>value")));
    }

    #endregion

    #region "GetToolsetData tests"

    /// <summary>
    ///  Verifies that distinct tools versions from the registry and configuration file are combined.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_NoConflict()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
        string binPath2 = NativeMethodsShared.IsWindows ? @"D:\somepath2" : "/somepath2";
        string fworkPath2 = NativeMethodsShared.IsWindows
            ? @"D:\windows\Microsoft.NET\Framework\v2.0.x86ret"
            : "/windows/Microsoft.NET/Framework/v2.0.x86ret";
        string fworkPath4 = NativeMethodsShared.IsWindows
            ? @"D:\windows\Microsoft.NET\Framework\v4.0.x86ret"
            : "/windows/Microsoft.NET/Framework/v4.0.x86ret";

        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", binPath);
        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", binPath2);

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="4.5">
                    <toolset toolsVersion="4.5">
                        <property name="MSBuildBinPath" value="{fworkPath2}{Path.DirectorySeparatorChar}"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="{fworkPath4}{Path.DirectorySeparatorChar}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.Count.ShouldBe(4);
        defaultToolsVersion.ShouldBe("4.5");
        toolsets.ShouldContain("2.0", toolset => toolset.ToolsPath.ShouldBe(binPath));
        toolsets.ShouldContain("4.0", toolset => toolset.ToolsPath.ShouldBe(binPath2));
        toolsets.ShouldContain("4.5", toolset => toolset.ToolsPath.ShouldBe(fworkPath2));
        toolsets.ShouldContain("5.0", toolset => toolset.ToolsPath.ShouldBe(fworkPath4));
    }

    /// <summary>
    ///  Verifies that <see cref="ToolsetDefinitionLocations.None"/> ignores external definitions and uses a fallback toolset.
    /// </summary>
    [WindowsOnlyFact]
    public void ToolsetInitializationFlagsSetToNone()
    {
        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "22.0");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("33.0");
        key1.SetValue("MSBuildBinPath", @"D:\somepath");
        using RegistryKey key2 = _registryKey.Key.CreateSubKey("55.0");
        key2.SetValue("MSBuildBinPath", @"D:\somepath2");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="4.5">
                    <toolset toolsVersion="5.5">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="6.5">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.None);

        _ = toolsets.ShouldHaveSingleItem();

        string expectedDefault = FrameworkLocationHelper.PathToDotNetFrameworkV20 == null
            ? ObjectModelHelpers.MSBuildDefaultToolsVersion
            : "2.0";

        defaultToolsVersion.ShouldBe(expectedDefault);
    }

    /// <summary>
    ///  Verifies that <see cref="ToolsetDefinitionLocations.Registry"/> excludes configuration-file definitions.
    /// </summary>
    [WindowsOnlyFact]
    public void ToolsetInitializationFlagsSetToRegistry()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
        string binPath2 = NativeMethodsShared.IsWindows ? @"D:\somepath2" : "/somepath2";

        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", binPath);
        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", binPath2);

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="4.5">
                    <toolset toolsVersion="4.5">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Registry);

        toolsets.Count.ShouldBe(2);
        defaultToolsVersion.ShouldBe("2.0");
        toolsets.ShouldContain("2.0", toolset => toolset.ToolsPath.ShouldBe(binPath));
        toolsets.ShouldContain("4.0", toolset => toolset.ToolsPath.ShouldBe(binPath2));
    }

    /// <summary>
    ///  Verifies that non-string registry values are rejected as toolset properties.
    /// </summary>
    [WindowsOnlyFact]
    public void ThrowOnNonStringRegistryValueTypes()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("MSBuildBinPath", "someBinPath");

            // Non-string
            rk.SetValue("QuadWordValue", 42, RegistryValueKind.QWord);

            // Should throw ...
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that registry toolset properties do not expand references to other registry properties.
    /// </summary>
    [WindowsOnlyFact]
    public void PropertiesInRegistryCannotReferToOtherPropertiesInRegistry()
    {
        string binPath = NativeMethodsShared.IsWindows ? "c:\\x" : "/x";
        using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        rk.SetValue("MSBuildBinPath", binPath + "$(p1)");
        rk.SetValue("p0", "$(p1)");
        rk.SetValue("p1", "v");
        rk.SetValue("p2", "$(p1)");
        rk.SetValue("MSBuildToolsPath", binPath + "$(p1)");

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Registry);

        toolsets.ShouldContain("2.0", toolset =>
        {
            toolset.Properties.ShouldContain("p0", property => property.EvaluatedValue.ShouldBeEmpty());
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe("v"));
            toolset.Properties.ShouldContain("p2", property => property.EvaluatedValue.ShouldBeEmpty());
            toolset.ToolsPath.ShouldBe(binPath);
        });
    }

    /// <summary>
    ///  Verifies that registry subtoolset properties do not expand references to other registry properties.
    /// </summary>
    [WindowsOnlyFact]
    public void SubToolsetPropertiesInRegistryCannotReferToOtherPropertiesInRegistry()
    {
        using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        rk.SetValue("MSBuildBinPath", "c:\\x$(p1)");
        rk.SetValue("p0", "$(p1)");
        rk.SetValue("p1", "v");

        using RegistryKey subToolsetKey = rk.CreateSubKey("dogfood");
        subToolsetKey.SetValue("p2", "$(p1)");
        subToolsetKey.SetValue("p3", "c:\\x$(p1)");

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Registry);

        toolsets.ShouldContain("2.0", toolset =>
        {
            toolset.Properties.ShouldContain("p0", property => property.EvaluatedValue.ShouldBeEmpty());
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe("v"));
            toolset.SubToolsets.ShouldContain("dogfood", subToolset =>
            {
                subToolset.Properties.ShouldContain("p2", property => property.EvaluatedValue.ShouldBeEmpty());
                subToolset.Properties.ShouldContain("p3", property => property.EvaluatedValue.ShouldBe(@"c:\x"));
            });
        });
    }

    /// <summary>
    ///  Verifies that registry subtoolsets cannot define <c>MSBuildToolsPath</c>.
    /// </summary>
    [WindowsOnlyFact]
    public void SubToolsetsCannotDefineMSBuildToolsPath()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey rk = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            rk.SetValue("p0", "$(p1)");
            rk.SetValue("p1", "v");

            using RegistryKey subToolsetKey = rk.CreateSubKey("dogfood");
            subToolsetKey.SetValue("p2", "$(p1)");
            subToolsetKey.SetValue("MSBuildToolsPath", "c:\\x$(p1)");

            // throws
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that <see cref="ToolsetDefinitionLocations.ConfigurationFile"/> excludes registry definitions.
    /// </summary>
    [WindowsOnlyFact]
    public void ToolsetInitializationFlagsSetToConfigurationFile()
    {
        string v2Dir = NativeMethodsShared.IsWindows ? "D:\\windows\\Microsoft.NET\\Framework\\v2.0.x86ret" : "/windows/Microsoft.NET/Framework/v2.0.x86ret";
        string v4Dir = NativeMethodsShared.IsWindows ? "D:\\windows\\Microsoft.NET\\Framework\\v4.0.x86ret" : "/windows/Microsoft.NET/Framework/v4.0.x86ret";

        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", @"D:\somepath");
        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", @"D:\somepath2");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="4.5">
                    <toolset toolsVersion="4.5">
                        <property name="MSBuildBinPath" value="{v2Dir}"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="{v4Dir}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.ConfigurationFile);

        toolsets.Count.ShouldBe(2);
        defaultToolsVersion.ShouldBe("4.5");
        toolsets.ShouldContain("4.5", toolset => toolset.ToolsPath.ShouldBe(v2Dir));
        toolsets.ShouldContain("5.0", toolset => toolset.ToolsPath.ShouldBe(v4Dir));
    }

    /// <summary>
    ///  Verifies that configuration properties expand named and default registry values.
    /// </summary>
    /// <remarks>
    ///  A registry expression has the form
    ///  <c>$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation)</c>.
    ///  Omitting the value name and preceding <c>@</c> reads the key's default value.
    /// </remarks>
    [WindowsOnlyFact(additionalMessage: "Registry access is only supported under Windows.")]
    public void PropertyInConfigurationFileReferencesRegistryLocation()
    {
        TransientTestRegistryKey registryKey = Environment.CreateRegistryKey();
        RegistryKey key1 = registryKey.Key;
        key1.SetValue("TaskLocation", @"somePathToTasks");
        key1.SetValue("TargetsLocation", @"D:\somePathToTargets");
        key1.SetValue("SchemaLocation", @"Schemas");
        key1.SetValue(null, @"D:\somePathToDefault");  // this sets the default value for this key

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="D:\$(Registry:{key1.Name}@TaskLocation)"/>
                        <property name="p1" value="$(p2)$(REGISTRY:{key1.Name})"/>
                        <property name="p2" value="$(p1)\$(Registry:hkey_current_user\{registryKey.SubKeyPath}@TaskLocation)\$(Registry:{key1.Name}@SchemaLocation)\2.0"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.ToolsPath.ShouldBe(@"D:\somePathToTasks");
            toolset.Properties.Count.ShouldBe(2);
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe(@"D:\somePathToDefault"));
            toolset.Properties.ShouldContain("p2", property =>
                property.EvaluatedValue.ShouldBe(@"D:\somePathToDefault\somePathToTasks\Schemas\2.0"));
        });
    }

    /// <summary>
    ///  Verifies that registry tools paths containing invalid characters are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void ToolsPathInRegistryHasInvalidPathChars()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
            using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\some\foo|bar\path\");

            // should throw...
            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: null,
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that duplicate property definitions within a configured toolset are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void SamePropertyDefinedMultipleTimesForSingleToolsVersionInConfigurationFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="p1" value="another"/>
                            <property name="MSBuildBinPath" value="C:\$(p1)\folder"/>
                            <property name="p1" value="newValue"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: null,
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.ConfigurationFile);
        });

    /// <summary>
    ///  Verifies that duplicate configuration property names are rejected regardless of casing.
    /// </summary>
    [WindowsOnlyFact]
    public void SamePropertyDifferentCaseDefinedMultipleTimesForSingleToolsVersionInConfigurationFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="p1" value="another"/>
                            <property name="mSbUiLdBiNpAtH" value="C:\$(p1)\folder"/>
                            <property name="P1" value="newValue"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: null,
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.ConfigurationFile);
        });

    /// <summary>
    ///  Verifies that duplicate tools version definitions in configuration are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void SameToolsVersionDefinedMultipleTimesInConfigurationFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="p1" value="another"/>
                        </toolset>
                        <toolset toolsVersion="4.0">
                            <property name="MSBuildBinPath" value="D:\folder"/>
                            <property name="p2" value="anotherValue"/>
                        </toolset>
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="p1" value="another"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: null,
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.ConfigurationFile);
        });

    /// <summary>
    ///  Verifies that duplicate configured tools versions are rejected regardless of casing.
    /// </summary>
    [WindowsOnlyFact]
    public void SameToolsVersionDifferentCaseDefinedMultipleTimesInConfigurationFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="DevDiv">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="p1" value="another"/>
                        </toolset>
                        <toolset toolsVersion="4.0">
                            <property name="MSBuildBinPath" value="D:\folder"/>
                            <property name="p2" value="anotherValue"/>
                        </toolset>
                        <toolset toolsVersion="DEVDIV">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="p1" value="another"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: null,
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.ConfigurationFile);
        });

    /// <summary>
    ///  Verifies that configuration toolsets cannot define reserved properties such as <c>MSBuildProjectFile</c>.
    /// </summary>
    [WindowsOnlyFact]
    public void CannotSetReservedPropertyInConfigFile()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\some\folder\on\disk"/>
                            <property name="MSBuildProjectFile" value="newValue"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: null,
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.ConfigurationFile);
        });

    /// <summary>
    ///  Verifies that registry toolsets cannot define reserved properties such as <c>MSBuildProjectFile</c>.
    /// </summary>
    [WindowsOnlyFact]
    public void CannotSetReservedPropertyInRegistry()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");
            key1.SetValue("MSBuildProjectFile", @"SomeRegistryValue");

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: null,
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that registry subtoolsets cannot define reserved properties such as <c>MSBuildProjectFile</c>.
    /// </summary>
    [WindowsOnlyFact]
    public void CannotSetReservedPropertyInRegistrySubToolset()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            using RegistryKey subKey1 = key1.CreateSubKey("Foo");

            subKey1.SetValue("MSBuildBinPath", @"D:\somepath");
            subKey1.SetValue("MSBuildProjectFile", @"SomeRegistryValue");

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: null,
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Registry);
        });

    /// <summary>
    ///  Verifies that property expansion does not leak between toolset definitions.
    /// </summary>
    [WindowsOnlyFact]
    public void NoInterferenceBetweenToolsetDefinitions()
    {
        string v20Dir = NativeMethodsShared.IsWindows ? @"D:\20\some\folder\on\disk" : "/20/some/folder/on/disk";
        string v35Dir = NativeMethodsShared.IsWindows ? @"D:\35\some\folder\on\disk" : "/35/some/folder/on/disk";
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{v20Dir}"/>
                        <property name="p1" value="another"/>
                        <property name="p4" value="fourth$(p3)Value" />
                    </toolset>
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="{v35Dir}"/>
                        <property name="p2" value="some$(p1)value"/>
                        <property name="p3" value="propertyValue"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: null,
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.ConfigurationFile);

        toolsets.Count.ShouldBe(2);

        toolsets.ShouldContain("2.0", toolset =>
        {
            toolset.ToolsPath.ShouldBe(v20Dir);
            toolset.Properties.Count.ShouldBe(2);
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe(@"another"));
            toolset.Properties.ShouldContain("p4", property => property.EvaluatedValue.ShouldBe(@"fourthValue"));
        });
        toolsets.ShouldContain("4.0", toolset =>
        {
            toolset.ToolsPath.ShouldBe(v35Dir);
            toolset.Properties.Count.ShouldBe(2);
            toolset.Properties.ShouldContain("p2", property => property.EvaluatedValue.ShouldBe(@"somevalue"));
            toolset.Properties.ShouldContain("p3", property => property.EvaluatedValue.ShouldBe(@"propertyValue"));
        });
    }

    /// <summary>
    ///  Verifies that a registry expression without a location is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "Access local machine registry is for Windows only.")]
    public void ConfigFileInvalidRegistryExpression1()
        => ConfigFileInvalidRegistryExpressionHelper("""<property name="p" value="$(Registry:)"/>""");

    /// <summary>
    ///  Verifies that a registry expression with an invalid key path is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "Access local machine registry is for Windows only.")]
    public void ConfigFileInvalidRegistryExpression2()
        => ConfigFileInvalidRegistryExpressionHelper("""<property name="p" value="$(Registry:__bogus__)"/>""");

    /// <summary>
    ///  Verifies that a registry expression containing only the <c>@</c> value separator is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "Access local machine registry is for Windows only.")]
    public void ConfigFileInvalidRegistryExpression3()
        => ConfigFileInvalidRegistryExpressionHelper("""<property name="p" value="$(Registry:@)"/>""");

    /// <summary>
    ///  Verifies that a registry expression with repeated <c>@</c> value separators is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileInvalidRegistryExpression4()
        => ConfigFileInvalidRegistryExpressionHelper(
            """<property name="p" value="$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@@TaskLocation)"/>""");

    /// <summary>
    ///  Verifies that a registry expression with a trailing <c>@</c> value separator is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileInvalidRegistryExpression5()
        => ConfigFileInvalidRegistryExpressionHelper(
            """<property name="p" value="$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation@)"/>""");

    /// <summary>
    ///  Verifies that a registry expression with a leading <c>@</c> value separator is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileInvalidRegistryExpression6()
        => ConfigFileInvalidRegistryExpressionHelper(
            """<property name="p" value="$(Registry:@HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation)"/>""");

    /// <summary>
    ///  Verifies that a registry expression with an unsupported hive is rejected in configuration properties.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "Access registry is for Windows only.")]
    public void ConfigFileInvalidRegistryExpression7()
        => ConfigFileInvalidRegistryExpressionHelper(
            """<property name="p" value="$(Registry:BOGUS_HIVE\Software\Vendor\Tools@TaskLocation)"/>""");

    /// <summary>
    ///  Verifies that an undefined property named <c>Registry</c> expands to an empty configuration value.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileStringEmptyRegistryExpression1()
        => ConfigFileValidRegistryExpressionHelper("""<property name="p" value="$(Registry)"/>""", string.Empty);

    /// <summary>
    ///  Verifies that reading a nonexistent registry key produces an empty configuration value.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileStringEmptyRegistryExpression2()
        => ConfigFileValidRegistryExpressionHelper(
            """<property name="p" value="$(Registry:HKEY_CURRENT_USER\Nonexistent_Key\Software\Vendor\Tools@TaskLocation)"/>""",
            string.Empty);

    /// <summary>
    ///  Verifies that registry-like text without a closing <c>)</c> is not treated as a property
    ///  expression and remains unchanged.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileNonPropertyRegistryExpression1()
        => ConfigFileValidRegistryExpressionHelper(
            """<property name="p" value="$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation"/>""",
            @"$(Registry:HKEY_CURRENT_USER\Software\Vendor\Tools@TaskLocation");

    /// <summary>
    ///  Verifies that registry-like text without a colon is treated as an undefined property and expands to an empty value.
    /// </summary>
    /// <remarks>
    ///  The remaining text contains invalid property-name characters but still expands to an empty value.
    /// </remarks>
    [WindowsOnlyFact]
    public void ConfigFileNonPropertyRegistryExpression2()
        => ConfigFileValidRegistryExpressionHelper(
            """<property name="p" value="$(RegistryHKEY_CURRENT_USER\Software\Vendor\Tools@@TaskLocation)"/>""",
            string.Empty);

    /// <summary>
    ///  Verifies that item expressions in configuration property values remain literal text.
    /// </summary>
    [WindowsOnlyFact]
    public void ConfigFileItemExpressionsDoNotExpandInConfigurationProperties()
        => ConfigFileValidRegistryExpressionHelper("""<property name="p" value="@(SomeItem)"/>""", "@(SomeItem)");

    /// <summary>
    ///  Verifies that a registry expression with an invalid key path is rejected in a registry-backed toolset property.
    /// </summary>
    [WindowsOnlyFact(additionalMessage: "Access local machine registry is for Windows only.")]
    public void RegistryInvalidRegistryExpression1()
        => RegistryInvalidRegistryExpressionHelper("$(Registry:__bogus__)");

    /// <summary>
    ///  Verifies that an undefined property named <c>Registry</c> expands to an empty registry-backed toolset value.
    /// </summary>
    [WindowsOnlyFact]
    public void RegistryValidRegistryExpression1()
        => RegistryValidRegistryExpressionHelper("$(Registry)", string.Empty);

    /// <summary>
    ///  Verifies that an invalid expression in a registry-backed toolset property is rejected.
    /// </summary>
    /// <param name="propertyExpression">The invalid property expression to store in the registry.</param>
    private void RegistryInvalidRegistryExpressionHelper(string propertyExpression)
    {
        // this should throw...
        InvalidToolsetDefinitionException exception = Should.Throw<InvalidToolsetDefinitionException>(() =>
            RegistryValidRegistryExpressionHelper(propertyExpression, string.Empty));
        Output.WriteLine(exception.Message);
    }

    /// <summary>
    ///  Verifies that a registry-backed toolset property expression evaluates to the expected value.
    /// </summary>
    /// <param name="propertyExpression">The property expression to store in the registry.</param>
    /// <param name="expectedValue">The expected evaluated property value.</param>
    private void RegistryValidRegistryExpressionHelper(string propertyExpression, string expectedValue)
    {
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", "xxxx");
        key1.SetValue("p", propertyExpression);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Registry);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
            toolset.Properties.ShouldContain("p", property => property.EvaluatedValue.ShouldBe(expectedValue)));
    }

    /// <summary>
    ///  Verifies that an invalid registry expression in a configuration property is rejected.
    /// </summary>
    /// <param name="propertyExpression">The property XML containing the invalid registry expression.</param>
    private void ConfigFileInvalidRegistryExpressionHelper(string propertyExpression)
    {
        // this should throw...
        InvalidToolsetDefinitionException exception = Should.Throw<InvalidToolsetDefinitionException>(() =>
            ConfigFileValidRegistryExpressionHelper(propertyExpression, string.Empty));
        Output.WriteLine(exception.Message);
    }

    /// <summary>
    ///  Verifies that a configuration property containing an expression evaluates to the expected value.
    /// </summary>
    /// <param name="propertyExpression">The property XML to insert into the toolset configuration.</param>
    /// <param name="expectedValue">The expected evaluated property value.</param>
    private void ConfigFileValidRegistryExpressionHelper(string propertyExpression, string expectedValue)
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="xxxx"/>
                        {propertyExpression}
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.ConfigurationFile);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
            toolset.Properties.ShouldContain("p", property => property.EvaluatedValue.ShouldBe(expectedValue)));
    }

    /// <summary>
    ///  Verifies that configuration tools paths take precedence over registry values for the same tools version.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_ConflictingPropertyValuesSameCase()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
        string overrideBinPath = NativeMethodsShared.IsWindows ? @"D:\somedifferentpath" : "/somedifferentpath";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", binPath);

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{overrideBinPath}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(overrideBinPath);
        });
    }

    /// <summary>
    ///  Verifies that a configuration toolset takes precedence over an invalid registry definition.
    /// </summary>
    /// <remarks>
    ///  The registry definition contains a property function that would throw if evaluated.
    ///  The configuration definition must take precedence without reading that registry value.
    /// </remarks>
    [WindowsOnlyFact]
    public void GetToolsetData_ConflictingPropertyValuesRegistryThrows()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", @"$([MSBuild]::SomeNonexistentPropertyFunction())");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{binPath}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(binPath);
        });
    }

    /// <summary>
    ///  Verifies that configuration toolsets replace registry toolsets rather than merging their properties.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_NoMerging()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
        string overrideBinPath = NativeMethodsShared.IsWindows ? @"D:\someotherpath" : "/someotherpath";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", binPath);
        key1.SetValue("SomeRegistryProperty", @"SomeRegistryValue");

        // Set the config file contents as needed
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBuildBinPath" value="{overrideBinPath}"/>
                        <property name="SomeConfigProperty" value="SomeConfigValue"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.ToolsPath.ShouldBe(overrideBinPath);
            toolset.Properties.ShouldNotContainKey("SomeRegistryProperty"); // Was zapped
            toolset.Properties.ShouldHaveSingleItem("SomeConfigProperty", property =>
                property.EvaluatedValue.ShouldBe(@"SomeConfigValue"));
        });
    }

    /// <summary>
    ///  Verifies that a project without <c>ToolsVersion</c> uses the collection's default under legacy defaulting rules.
    /// </summary>
    [WindowsOnlyFact]
    public void ToolsVersionAttributeNotSpecifiedOnProjectElementAndDefaultVersionSpecifiedInRegistry()
    {
        // In the new world of figuring out the ToolsVersion to use, we completely ignore the default
        // ToolsVersion in the ProjectCollection.  However, this test explicitly depends on modifying
        // that, so we need to turn the new defaulting behavior off in order to verify that this still works.
        TransientTestState legacyDefault = Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
        RegisterCleanup(() =>
        {
            // Cleanup callbacks run before Environment cleanup, so restore the variable before refreshing the cache.
            legacyDefault.Revert();
            InternalUtilities.RefreshInternalEnvironmentValues();
        });

        InternalUtilities.RefreshInternalEnvironmentValues();

        using ProjectCollection projectCollection = new();

        projectCollection.AddToolset(new Toolset("2.0", "20toolsPath", projectCollection, msbuildOverrideTasksPath: null));
        projectCollection.AddToolset(new Toolset(
            ObjectModelHelpers.MSBuildDefaultToolsVersion,
            "120toolsPath",
            projectCollection,
            msbuildOverrideTasksPath: null));

        string projectPath = Environment.CreateFile("x.proj", "<Project />").Path;

        Project project = projectCollection.LoadProject(projectPath);

        string defaultExpected = FrameworkLocationHelper.PathToDotNetFrameworkV20 == null
            ? ObjectModelHelpers.MSBuildDefaultToolsVersion
            : "Current";

        project.ToolsVersion.ShouldBe(defaultExpected);
        projectCollection.DefaultToolsVersion.ShouldBe(defaultExpected);
    }

    /// <summary>
    ///  Verifies that configuration toolsets are read when registry toolset definitions are absent.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_RegistryNotPresent()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somedifferentpath" : "/somedifferentpath";
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="2.0">
                    <toolset toolsVersion="2.0">
                        <property name="MSBUILDBINPATH" value="{binPath}"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(binPath);
        });
    }

    /// <summary>
    ///  Verifies that registry toolsets are read when the configuration file has no toolset definitions.
    /// </summary>
    /// <remarks>
    ///  A missing configuration file is equivalent to one without an <c>msbuildToolsets</c> section.
    /// </remarks>
    [WindowsOnlyFact]
    public void GetToolsetData_ConfigFileNotPresent()
    {
        string binPath = NativeMethodsShared.IsWindows ? @"D:\somepath" : "/somepath";
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", binPath);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldHaveSingleItem("2.0", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(binPath);
        });
    }

    /// <summary>
    ///  Verifies that a fallback toolset is selected when neither the registry nor configuration defines toolsets.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_RegistryAndConfigNotPresent()
    {
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        // Should either be the last-ditch 2.0 toolset, or if 2.0 is not installed, then the last-last-ditch of 4.0
        _ = toolsets.ShouldHaveSingleItem();

        if (FrameworkLocationHelper.PathToDotNetFrameworkV20 != null)
        {
            defaultToolsVersion.ShouldBe("2.0");
        }
        else
        {
            defaultToolsVersion.ShouldBe(ObjectModelHelpers.MSBuildDefaultToolsVersion);
        }
    }

    /// <summary>
    ///  Verifies that configuration-loading failures are reported as invalid toolset definitions.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_ReadConfigThrowsException()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
            key1.SetValue("MSBuildBinPath", @"D:\somepath");

            // Set the config helper to throw exception
            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile(string.Empty, new ConfigurationErrorsException());

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: GetStandardRegistryReader(),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Default);
        });

    /// <summary>
    ///  Verifies that registry subkey access failures are reported as invalid toolset definitions.
    /// </summary>
    [WindowsOnlyFact]
    public void GetToolsetData_ReadRegistryOpenSubKeyThrowsException()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKeyWrapper mockRegistryKey =
                new MockRegistryKey(_registryKey.SubKeyPath, MockRegistryKey.WhereToThrow.OpenSubKey);

            _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
                <configuration>
                    <configSections>
                        <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                    </configSections>
                    <msbuildToolsets default="2.0">
                        <toolset toolsVersion="2.0">
                            <property name="MSBuildBinPath" value="D:\somedifferentpath"/>
                        </toolset>
                    </msbuildToolsets>
                </configuration>
                """);

            _ = ToolsetReader.ReadAllToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                registryReader: new ToolsetRegistryReader(
                    Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                    globalProperties: [],
                    mockRegistryKey),
                configurationReader: GetStandardConfigurationReader(),
                environmentProperties: Environment.CreateProjectCollection().Collection.EnvironmentProperties,
                globalProperties: [],
                locations: ToolsetDefinitionLocations.Default);
        });

    #endregion

    #region "SetDefaultToolsetVersion tests"

    /// <summary>
    ///  Verifies that the configuration's default tools version takes precedence over the registry's default.
    /// </summary>
    [WindowsOnlyFact]
    public void SetDefaultToolsetVersion_SpecifiedInRegistryAndConfigFile()
    {
        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "2.0");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("2.0");
        key1.SetValue("MSBuildBinPath", @"D:\somepath");
        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", @"D:\somepath2");

        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="5.0">
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        defaultToolsVersion.ShouldBe("5.0");
    }

    /// <summary>
    ///  Verifies that the registry's default tools version is used when configuration does not specify one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetDefaultToolsetVersion_SpecifiedOnlyInRegistry()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="3.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "4.0");
        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", @"D:\somepath2");

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        defaultToolsVersion.ShouldBe("4.0");
    }

    /// <summary>
    ///  Verifies that the registry's override-task path is used when configuration does not specify one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetOverrideTasks_SpecifiedOnlyInRegistry()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="3.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        string overridePath = NativeMethodsShared.IsWindows ? "c:\\TaskOverridePath" : "/TaskOverridePath";

        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "4.0");
        _currentVersionRegistryKey.SetValue("msbuildOverrideTasksPath", overridePath);

        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", @"D:\somepath2");
        key2.SetValue("MSBuildBinPath", @"D:\OtherTaskOverridePath");

        using RegistryKey key3 = _toolsVersionsRegistryKey.CreateSubKey("5.0");
        key3.SetValue("MSBuildBinPath", @"D:\somepath3");

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        defaultToolsVersion.ShouldBe("4.0");
        toolsets.ShouldContain("4.0", toolset => toolset.OverrideTasksPath.ShouldBe(overridePath));

        // Assert.Equal("c:\\OtherTaskOverridePath", values["5.0"].OverrideTasksPath); // UNDONE: Per-toolset override paths don't work.
    }

    /// <summary>
    ///  Verifies that the registry's default override tools version is used when configuration does not specify one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetDefaultOverrideToolsVersion_SpecifiedOnlyInRegistry()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="3.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        // Set up registry with two tools versions and one property each
        _currentVersionRegistryKey.SetValue("DefaultOverrideToolsVersion", "13.0");

        using RegistryKey key = _toolsVersionsRegistryKey.CreateSubKey("13.0");
        key.SetValue("MSBuildBinPath", @"D:\somepath2");

        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("4.0");
        key2.SetValue("MSBuildBinPath", @"D:\somepath3");

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldContain("4.0", toolset => toolset.DefaultOverrideToolsVersion.ShouldBe("13.0"));
    }

    /// <summary>
    ///  Verifies that the configuration's default tools version is used when the registry does not specify one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetDefaultToolsetVersion_SpecifiedOnlyInConfigFile()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="5.0">
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        defaultToolsVersion.ShouldBe("5.0");
    }

    /// <summary>
    ///  Verifies that the configuration-wide override-task path is used when the registry does not specify one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetOverrideTaskPath_SpecifiedOnlyInConfigFile()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="5.0" msbuildOverrideTasksPath="C:\TaskOverride">
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                        <property name="MSBuildOverrideTasksPath" value="c:\OtherTaskOverride"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        defaultToolsVersion.ShouldBe("5.0");
        toolsets.ShouldContain("4.0", toolset => toolset.OverrideTasksPath.ShouldBe(@"C:\TaskOverride"));

        // Assert.Equal("C:\\OtherTaskOverride", values["5.0"].OverrideTasksPath); // UNDONE: Per-toolset override paths aren't working
    }

    /// <summary>
    ///  Verifies that the configuration's default override tools version is used when the registry does not specify one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetDefaultOverrideToolsVersion_SpecifiedOnlyInConfigFile()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets default="5.0" DefaultOverrideToolsVersion="3.0">
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        defaultToolsVersion.ShouldBe("5.0");
        toolsets.ShouldContain("4.0", toolset => toolset.DefaultOverrideToolsVersion.ShouldBe("3.0"));
    }

    /// <summary>
    ///  Verifies that a fallback default tools version is selected when neither source specifies one.
    /// </summary>
    [WindowsOnlyFact]
    public void SetDefaultToolsetVersion_SpecifiedNowhere()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                    </toolset>
                    <toolset toolsVersion="5.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v4.0.x86ret\"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        string defaultToolsVersion = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: [],
            locations: ToolsetDefinitionLocations.Default);

        string expectedDefault = FrameworkLocationHelper.PathToDotNetFrameworkV20 == null
            ? ObjectModelHelpers.MSBuildDefaultToolsVersion
            : "2.0";

        defaultToolsVersion.ShouldBe(expectedDefault); // built-in default
        toolsets.ShouldContain(expectedDefault, toolset =>
        {
            toolset.OverrideTasksPath.ShouldBeNull();
            toolset.DefaultOverrideToolsVersion.ShouldBeNull();
        });
    }

    /// <summary>
    ///  Verifies that configuration properties expand earlier properties and environment variables.
    /// </summary>
    [WindowsOnlyFact]
    public void PropertiesInToolsetsFromConfigFileAreExpanded()
    {
        // $(COMPUTERNAME) is just a convenient env var. $(NUMBER_OF_PROCESSORS) isn't defined on Longhorn
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                        <property name="p1" value="v1$(p2)"/>
                        <property name="p2" value="__$(p1)__"/>
                        <property name="p3" value="$(COMPUTERNAME)"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: collection.GlobalPropertiesCollection,
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldContain("4.0", toolset =>
        {
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe("v1"));

            // Properties can refer to other properties also defined in the config file
            toolset.Properties.ShouldContain("p2", property => property.EvaluatedValue.ShouldBe("__v1__"));
            toolset.Properties.ShouldContain("p3", property => property.EvaluatedValue.ShouldBe(System.Environment.MachineName));
        });
    }

    /// <summary>
    ///  Verifies that configured tools paths expand toolset properties and can be referenced by later properties.
    /// </summary>
    [WindowsOnlyFact]
    public void PropertiesInToolsetsFromConfigFileAreExpandedInToolsPath()
    {
        string binPathConfig = NativeMethodsShared.IsWindows
            ? @"D:\windows\$(p1)\Framework\v2.0.x86ret\$(COMPUTERNAME)"
            : "/windows/$(p1)/Framework/v2.0.x86ret";
        string toolsPathConfig = NativeMethodsShared.IsWindows
            ? @"D:\$(p2)\$(p1)\Framework\v2.0.x86ret\$(COMPUTERNAME)"
            : "/$(p2)/$(p1)/Framework/v2.0.x86ret";

        // $(COMPUTERNAME) is just a convenient env var. $(NUMBER_OF_PROCESSORS) isn't defined on Longhorn
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile($"""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="4.0">
                        <property name="p1" value="Microsoft.NET"/>
                        <property name="p2" value="windows"/>
                        <property name="MSBuildBinPath" value="{binPathConfig}"/>
                        <property name="MSBuildToolsPath" value="{toolsPathConfig}"/>
                        <property name="p3" value="v3$(MSBuildToolsPath)"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        using var collection = new ProjectCollection();
        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: collection.EnvironmentProperties,
            globalProperties: collection.GlobalPropertiesCollection,
            locations: ToolsetDefinitionLocations.Default);

        string expectedToolsPath = NativeMethodsShared.IsWindows
            ? $@"D:\windows\Microsoft.NET\Framework\v2.0.x86ret\{System.Environment.MachineName}"
            : "/windows/Microsoft.NET/Framework/v2.0.x86ret";

        toolsets.ShouldContain("4.0", toolset =>
        {
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe("Microsoft.NET"));
            toolset.Properties.ShouldContain("p2", property => property.EvaluatedValue.ShouldBe("windows"));
            toolset.ToolsPath.ShouldBe(expectedToolsPath);
            toolset.Properties.ShouldContain("p3", property => property.EvaluatedValue.ShouldBe("v3" + expectedToolsPath));
        });
    }

    /// <summary>
    ///  Verifies that global properties expand in toolset properties but cannot be overwritten by them.
    /// </summary>
    [WindowsOnlyFact]
    public void GlobalPropertiesInToolsetsAreExpandedButAreNotOverwritten()
    {
        _ = ToolsetConfigurationReaderTestHelper.WriteConfigFile("""
            <configuration>
                <configSections>
                    <section name="msbuildToolsets" type="Microsoft.Build.Evaluation.ToolsetConfigurationSection, Microsoft.Build" />
                </configSections>
                <msbuildToolsets>
                    <toolset toolsVersion="4.0">
                        <property name="MSBuildBinPath" value="D:\windows\Microsoft.NET\Framework\v2.0.x86ret\"/>
                        <property name="p1" value="$(gp1)"/>
                        <property name="gp1" value="v2"/>
                        <property name="p2" value="$(gp1)"/>
                    </toolset>
                </msbuildToolsets>
            </configuration>
            """);

        Dictionary<string, string> globalProperties = [with(StringComparer.OrdinalIgnoreCase)];
        globalProperties["gp1"] = "gv1";

        using ProjectCollection e = new(globalProperties, loggers: null, ToolsetDefinitionLocations.None);
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        _ = ToolsetReader.ReadAllToolsets(
            toolsets,
            registryReader: GetStandardRegistryReader(),
            configurationReader: GetStandardConfigurationReader(),
            environmentProperties: e.EnvironmentProperties,
            globalProperties: e.GlobalPropertiesCollection,
            locations: ToolsetDefinitionLocations.Default);

        toolsets.ShouldContain("4.0", toolset =>
        {
            toolset.Properties.ShouldContain("p1", property => property.EvaluatedValue.ShouldBe("gv1"));
            toolset.Properties.ShouldContain("p2", property => property.EvaluatedValue.ShouldBe("gv1"));
        });
    }

    #endregion
}

#endif
