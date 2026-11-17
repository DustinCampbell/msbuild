// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    [WindowsOnlyFact]
    public void GetWinBlueSDKLocation()
    {
        string sdkRootPath = ToolLocationHelper.GetPlatformSDKLocation("Windows", "8.1");

        ToolLocationHelper.GetSDKContentFolderPath(
            "Windows", "8.1", targetPlatformIdentifier: null, targetPlatformMinVersion: null, targetPlatformVersion: null, folderName: null)
            .ShouldBe(sdkRootPath);
    }

    [WindowsOnlyFact]
    public void GetWinBlueContentFolderPath()
    {
        string sdkRootPath = ToolLocationHelper.GetPlatformSDKLocation("Windows", "8.1");

        ToolLocationHelper.GetSDKContentFolderPath(
            "Windows", "8.1", targetPlatformIdentifier: null, targetPlatformMinVersion: null, targetPlatformVersion: null, @"DesignTime\CommonConfiguration\Neutral")
            .ShouldBe(Path.Combine(sdkRootPath, @"DesignTime\CommonConfiguration\Neutral"));
    }

    [WindowsOnlyFact]
    public void GetSDKRootLocation()
    {
        string expectedValue = ToolLocationHelper.GetPlatformSDKLocation("Windows", "10.0");

        ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.14944.0", "10.0.14944.0", folderName: null)
            .ShouldBe(expectedValue);

        ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.10586.0", "10.0.10586.0", folderName: null)
            .ShouldBe(expectedValue);
    }

#if RUNTIME_TYPE_NETCORE
    [WindowsOnlyFact(Skip = "https://github.com/dotnet/msbuild/issues/1250")]
#else
    [WindowsOnlyFact(Skip = "https://github.com/dotnet/msbuild/issues/2569")]
#endif
    public void GetUnversionedSDKUnionMetadataLocation()
    {
        string sdkRootPath = ToolLocationHelper.GetPlatformSDKLocation("Windows", "10.0");
        string returnValue = ToolLocationHelper.GetSDKContentFolderPath("Windows", "10.0", "UAP", "10.0.14393.0", "10.0.14393.0", "UnionMetadata");

        returnValue.ShouldNotContain("10.0.14393.0");
        returnValue.ShouldBe(Path.Combine(sdkRootPath, "UnionMetadata"));
    }

    [WindowsOnlyFact]
    public void GetVersionedSDKUnionMetadataLocation()
    {
        // Create manifest file
        TransientTestFolder platformRootFolder = Environment.CreateFolder();
        TransientTestFolder sdkRootFolder = platformRootFolder.CreateDirectory(Path.Combine("Windows Kits", "10"));
        TransientTestFolder platformFolder = sdkRootFolder.CreateDirectory(Path.Combine("Platforms", "UAP", "10.0.14944.0"));

        const string SdkManifestFileContent = """
            <?xml version="1.0" encoding="utf-8" ?>
            <FileList
              TargetPlatform="UAP"
              TargetPlatformMinVersion="10.0.0.0"
              TargetPlatformVersion="10.0.14944.0"
              DisplayName = "Microsoft Mock SDK for UAP 10.0.14944.0"
              AppliesTo = "WindowsAppContainer + (Managed | Javascript | Native)"
              MinVSVersion = "14.0"
              SupportsMultipleVersions="Error"
              SupportedArchitectures="x86;x64;ARM;ARM64">
            </FileList>
            """;

        const string PlatformFileContent = """
            <?xml version="1.0" encoding="utf-8"?>
            <ApplicationPlatform name="UAP" friendlyName="Windows 10 Anniversary Edition Insider Preview" version="10.0.14944.0">
               <VersionedContent>true</VersionedContent>
            </ApplicationPlatform>
            """;

        Environment.CreateFile(platformFolder, "Platform.xml", PlatformFileContent);
        Environment.CreateFile(sdkRootFolder, "SDKManifest.xml", SdkManifestFileContent);

        // Get and verify return value
        ToolLocationHelper.GetSDKContentFolderPath(
            "Windows", "10.0", "UAP", "10.0.14944.0", "10.0.14944.0", "UnionMetadata", platformRootFolder.Path)
            .ShouldBe(Path.Combine(sdkRootFolder.Path, "UnionMetadata", "10.0.14944.0"));
    }
}
