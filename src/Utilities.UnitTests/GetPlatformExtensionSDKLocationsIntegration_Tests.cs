// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Evaluation;
#if FEATURE_REGISTRY_SDKS
using System.Runtime.Versioning;
using Microsoft.Win32;
#endif
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Tests SDK location property functions against temporary SDK installations.
/// </summary>
/// <param name="output">The output helper for the test.</param>
public sealed class GetPlatformExtensionSDKLocationsIntegration_Tests(ITestOutputHelper output) : TestBase(output)
{
    /// <summary>
    ///  Verifies that SDK property functions resolve locations from an environment-specified root
    ///  and generate a display name when the manifest does not provide one.
    /// </summary>
    [Fact]
    public void VerifyGetInstalledSDKLocations()
    {
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder platformFolder = root.CreateDirectory(Path.Combine("MyPlatform", "8.0"));
        TransientTestFolder sdkFolder = platformFolder.CreateDirectory(Path.Combine("ExtensionSDKs", "SDkWithManifest", "2.0"));
        string platformDirectory = $"{platformFolder.Path}{Path.DirectorySeparatorChar}";
        string sdkDirectory = $"{sdkFolder.Path}{Path.DirectorySeparatorChar}";

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents("""
            <Project DefaultTargets="ExpandSDKReferenceAssemblies" ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0'))</SDKLocation1>
                    <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0'))</SDKLocation2>
                    <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0'))</SDKLocation3>
                    <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0'))</SDKName>
                </PropertyGroup>

                <Import Project="$(MSBuildBinPath)\Microsoft.Common.targets"/>
            </Project>
            """);

        Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", root.Path);
        Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

        Environment.CreateFile(platformFolder, "SDKManifest.xml", "HI");
        Environment.CreateFile(sdkFolder, "SDKManifest.xml", "HI");
        string testProjectFile = Environment.CreateFile(root, "testproject.csproj", tempProjectContents).Path;

        using ProjectCollection pc = new();
        Project project = pc.LoadProject(testProjectFile);
        string propertyValue1 = project.GetPropertyValue("SDKLocation1");
        string propertyValue2 = project.GetPropertyValue("SDKLocation2");
        string propertyValue3 = project.GetPropertyValue("SDKLocation3");
        string sdkName = project.GetPropertyValue("SDKName");

        propertyValue1.ShouldBe(sdkDirectory, StringCompareShould.IgnoreCase);
        propertyValue2.Length.ShouldBe(0);
        propertyValue3.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);

        // No displayname set in the SDK manifest, so it mocks one up
        sdkName.ShouldBe("MyPlatform 8.0");
    }

    /// <summary>
    ///  Verifies that SDK property functions use an explicit directory root and read the manifest's display name.
    /// </summary>
    [Fact]
    public void VerifyGetInstalledSDKLocations2()
    {
        TransientTestFolder root = Environment.CreateFolder();
        string testDirectoryRoot = root.Path;
        TransientTestFolder platformFolder = root.CreateDirectory(Path.Combine("MyPlatform", "8.0"));
        TransientTestFolder sdkFolder = platformFolder.CreateDirectory(Path.Combine("ExtensionSDKs", "SDkWithManifest", "2.0"));
        string platformDirectory = $"{platformFolder.Path}{Path.DirectorySeparatorChar}";
        string sdkDirectory = $"{sdkFolder.Path}{Path.DirectorySeparatorChar}";

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents($"""
            <Project DefaultTargets="ExpandSDKReferenceAssemblies" ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <PropertyGroup>
                <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                <TargetPlatformVersion>8.0</TargetPlatformVersion>
                <SDKDirectoryRoot>{testDirectoryRoot}</SDKDirectoryRoot>
                <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation1>
                <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation2>
                <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation3>
                <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0', '$(SDKDirectoryRoot)', ''))</SDKName>
                </PropertyGroup>

                <Import Project="$(MSBuildBinPath)\Microsoft.Common.targets"/>
            </Project>
            """);

        string platformSDKManifestContents = """
            <FileList
                DisplayName = "My cool platform SDK!">
            </FileList>
            """;

        Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
        Environment.CreateFile(platformFolder, "SDKManifest.xml", platformSDKManifestContents);
        Environment.CreateFile(sdkFolder, "SDKManifest.xml", "HI");
        string testProjectFile = Environment.CreateFile(root, "testproject.csproj", tempProjectContents).Path;

        using ProjectCollection pc = new();
        Project project = pc.LoadProject(testProjectFile);
        string propertyValue1 = project.GetPropertyValue("SDKLocation1");
        string propertyValue2 = project.GetPropertyValue("SDKLocation2");
        string propertyValue3 = project.GetPropertyValue("SDKLocation3");
        string sdkName = project.GetPropertyValue("SDKName");

        propertyValue1.ShouldBe(sdkDirectory, StringCompareShould.IgnoreCase);
        propertyValue3.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
        propertyValue2.Length.ShouldBe(0);
        sdkName.ShouldBe("My cool platform SDK!");
    }

#if FEATURE_REGISTRY_SDKS

    /// <summary>
    ///  Verifies that SDK location and display-name property functions resolve SDKs from an isolated registry hierarchy.
    /// </summary>
    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void VerifyGetInstalledSDKLocations3()
    {
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder platformFolder = root.CreateDirectory(Path.Combine("MyPlatform", "8.0"));
        TransientTestFolder sdkFolder = platformFolder.CreateDirectory(Path.Combine("ExtensionSDKs", "SDkWithManifes", "2.0"));
        string platformDirectory = $"{platformFolder.Path}{Path.DirectorySeparatorChar}";
        string sdkDirectory = $"{sdkFolder.Path}{Path.DirectorySeparatorChar}";
        TransientTestRegistryKey registryKey = Environment.CreateRegistryKey();
        Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents($"""
            <Project DefaultTargets="ExpandSDKReferenceAssemblies" ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>{registryKey.SubKeyPath}</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation1>
                    <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation2>
                    <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation3>
                    <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</SDKName>
                </PropertyGroup>
                <Import Project="$(MSBuildBinPath)\Microsoft.Common.targets"/>
            </Project>
            """);

        string platformSDKManifestContents = """
            <FileList
                DisplayName = "MyPlatform from the registry"
                PlatformIdentity = "MyPlatform, version=8.0"
                TargetFramework = ".NETCore, version=v4.5; .NETFramework, version=v4.5"
                MinVSVersion = "12.0"
                MinOSVersion = "6.2.1"
                MaxOSVersionTested = "6.2.1"
                UnsupportedDowntarget = "MyPlatform, version=8.0">

                <File Reference = "Windows">
                    <ToolboxItems VSCategory = "Toolbox.Default"/>
                </File>
            </FileList>
            """;

        using RegistryKey extensionKey = registryKey.Key.CreateSubKey(@"MyPlatform\v8.0\ExtensionSDKS\SDKWithManifest\2.0");
        extensionKey.SetValue("", sdkDirectory);
        using RegistryKey platformKey = registryKey.Key.CreateSubKey(@"MyPlatform\v8.0");
        platformKey.SetValue("", platformDirectory);

        Environment.CreateFile(sdkFolder, "SDKManifest.xml", "HI");
        Environment.CreateFile(platformFolder, "SDKManifest.xml", platformSDKManifestContents);
        string testProjectFile = Environment.CreateFile(root, "testproject.csproj", tempProjectContents).Path;

        using ProjectCollection pc = new();
        Project project = pc.LoadProject(testProjectFile);
        string propertyValue1 = project.GetPropertyValue("SDKLocation1");
        string propertyValue2 = project.GetPropertyValue("SDKLocation2");
        string propertyValue3 = project.GetPropertyValue("SDKLocation3");
        string sdkName = project.GetPropertyValue("SDKName");

        propertyValue1.ShouldBe(sdkDirectory, StringCompareShould.IgnoreCase);
        propertyValue3.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
        propertyValue2.Length.ShouldBe(0);
        sdkName.ShouldBe("MyPlatform from the registry");
    }

    /// <summary>
    ///  Verifies that property functions resolve a registry-registered pre-OneCore SDK
    ///  and its design-time props directory.
    /// </summary>
    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void VerifyGetPreOneCoreSDKPropsLocation()
    {
        // This is the mockup layout for SDKs before One Core SDK.
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder platformFolder = root.CreateDirectory(Path.Combine("MyPlatform", "8.0"));
        string platformDirectory = $"{platformFolder.Path}{Path.DirectorySeparatorChar}";
        string propsDirectory = platformFolder.CreateDirectory(Path.Combine("DesignTime", "CommonConfiguration", "Neutral")).Path;
        TransientTestRegistryKey registryKey = Environment.CreateRegistryKey();
        Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents($"""
            <Project DefaultTargets="ExpandSDKReferenceAssemblies" ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>{registryKey.SubKeyPath}</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <PlatformSDKLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform', '8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</PlatformSDKLocation>
                    <PropsLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKPropsFileLocation('',' ','MyPlatform',' ','8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</PropsLocation>
                </PropertyGroup>
                <Import Project="$(MSBuildBinPath)\Microsoft.Common.targets"/>
            </Project>
            """);

        using RegistryKey platformKey = registryKey.Key.CreateSubKey(@"MyPlatform\v8.0");
        platformKey.SetValue("InstallationFolder", platformDirectory);

        Environment.CreateFile(platformFolder, "SDKManifest.xml", "Test");

        using var collection = new ProjectCollection();
        Project project = ObjectModelHelpers.CreateInMemoryProject(collection, tempProjectContents);

        string propertyValue = project.GetPropertyValue("PlatformSDKLocation");
        string propsLocation = project.GetPropertyValue("PropsLocation");

        propertyValue.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
        propsLocation.ShouldBe(propsDirectory, StringCompareShould.IgnoreCase);
    }

    /// <summary>
    ///  Verifies that property functions resolve a registry-registered OneCore SDK
    ///  and its platform/version-specific design-time props directory.
    /// </summary>
    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void VerifyGetOneCoreSDKPropsLocation()
    {
        // This is the mockup layout for One Core SDK.
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder platformFolder = root.CreateDirectory(Path.Combine("OneCoreSDK", "1.0"));
        string platformDirectory = $"{platformFolder.Path}{Path.DirectorySeparatorChar}";
        string propsDirectory = platformFolder.CreateDirectory(
            Path.Combine("DesignTime", "CommonConfiguration", "Neutral", "MyPlatform", "0.8.0.0")).Path;
        TransientTestFolder platformVersionFolder = platformFolder.CreateDirectory(Path.Combine("Platforms", "MyPlatform", "0.8.0.0"));
        TransientTestRegistryKey registryKey = Environment.CreateRegistryKey();
        Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);

        string tempProjectContents = ObjectModelHelpers.CleanupFileContents($"""
            <Project DefaultTargets="ExpandSDKReferenceAssemblies" ToolsVersion="msbuilddefaulttoolsversion" xmlns="msbuildnamespace">
                <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier>
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>{registryKey.SubKeyPath}</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <PlatformSDKLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('OneCoreSDK', '1.0', '', '$(SDKRegistryRoot)'))</PlatformSDKLocation>
                    <PropsLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKPropsFileLocation('OneCoreSDK','1.0','MyPlatform',' ','0.8.0.0', '', '$(SDKRegistryRoot)'))</PropsLocation>
                </PropertyGroup>
                <Import Project="$(MSBuildBinPath)\Microsoft.Common.targets"/>
            </Project>
            """);

        using RegistryKey platformKey = registryKey.Key.CreateSubKey(@"OneCoreSDK\1.0");
        platformKey.SetValue("InstallationFolder", platformDirectory);

        Environment.CreateFile(platformFolder, "SDKManifest.xml", "Test");
        Environment.CreateFile(platformVersionFolder, "Platform.xml", "Test");

        using var collection = new ProjectCollection();
        Project project = ObjectModelHelpers.CreateInMemoryProject(collection, tempProjectContents);

        string propertyValue = project.GetPropertyValue("PlatformSDKLocation");
        string propsLocation = project.GetPropertyValue("PropsLocation");

        propertyValue.ShouldBe(platformDirectory, StringCompareShould.IgnoreCase);
        propsLocation.ShouldBe(propsDirectory, StringCompareShould.IgnoreCase);
    }

#endif
}
