// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;
using ReflectionProcessorArchitecture = System.Reflection.ProcessorArchitecture;

namespace Microsoft.Build.UnitTests.Expansion;

public sealed class WellKnownFunctions_ToolLocationHelper_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void Properties()
    {
        AssertInvoked(nameof(ToolLocationHelper.CurrentToolsVersion), [], ToolLocationHelper.CurrentToolsVersion);
        AssertInvoked(nameof(ToolLocationHelper.PathToSystem), [], ToolLocationHelper.PathToSystem);
    }

    [WindowsOnlyFact]
    public void AssemblyFoldersExMethod()
        => AssertInvoked(
            nameof(ToolLocationHelper.GetAssemblyFoldersExInfo),
            [@"SOFTWARE\__Missing__", "v4.0", "AssemblyFoldersEx", "", "", ReflectionProcessorArchitecture.None]);

    [Fact]
    public void SdkMethods()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var root = env.CreateFolder();
        string rootPath = root.Path;
        string[] roots = [rootPath];
        var version = new Version(1, 0);
        var extensionSdks = new Dictionary<string, string>();
        var targetSdks = new List<TargetPlatformSDK>();
        var config = env.CreateFile(root, "AssemblyFolders.config", "<AssemblyFoldersConfig><AssemblyFolders /></AssemblyFoldersConfig>");

        AssertInvoked(
            nameof(ToolLocationHelper.GetAssemblyFoldersFromConfigInfo),
            [config.Path, "v4.0", ReflectionProcessorArchitecture.None]);

        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocations), ["Missing", version]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocations), [roots, "", "Missing", version]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocations), [roots, roots, "", "Missing", version]);

        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocationsAndVersions), ["Missing", version]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocationsAndVersions), [roots, "", "Missing", version]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocationsAndVersions), [roots, roots, "", "Missing", version]);

        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocation), ["Missing, Version=1.0", "Missing", version]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformExtensionSDKLocation),
            ["Missing, Version=1.0", "Missing", version, roots, ""]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformExtensionSDKLocation),
            ["Missing, Version=1.0", "Missing", version, roots, roots, ""]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformExtensionSDKLocation), ["Missing, Version=1.0", "Missing", "1.0"]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformExtensionSDKLocation),
            ["Missing, Version=1.0", "Missing", "1.0", rootPath, ""]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformExtensionSDKLocation),
            ["Missing, Version=1.0", "Missing", "1.0", rootPath, rootPath, ""]);

        AssertInvoked(nameof(ToolLocationHelper.FilterPlatformExtensionSDKs), [version, extensionSdks]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKReferenceFolders), [rootPath]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKReferenceFolders), [rootPath, "Retail", "Neutral"]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKRedistFolders), [rootPath]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKRedistFolders), [rootPath, "Retail", "Neutral"]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKDesignTimeFolders), [rootPath]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKDesignTimeFolders), [rootPath, "Retail", "Neutral"]);

        AssertInvoked(nameof(ToolLocationHelper.GetTargetPlatformSdks), []);
        AssertInvoked(nameof(ToolLocationHelper.GetTargetPlatformSdks), [roots, ""]);
        AssertInvoked(nameof(ToolLocationHelper.FilterTargetPlatformSdks), [targetSdks, version, version]);

        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKPropsFileLocation), ["Missing", "1.0", "Missing", "1.0", "1.0"]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformSDKPropsFileLocation),
            ["Missing", "1.0", "Missing", "1.0", "1.0", rootPath, ""]);
        AssertInvoked(nameof(ToolLocationHelper.GetTargetPlatformReferences), ["Missing", "1.0", "Missing", "1.0", "1.0"]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetTargetPlatformReferences),
            ["Missing", "1.0", "Missing", "1.0", "1.0", rootPath, ""]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformOrFrameworkExtensionSdkReferences),
            ["Missing, Version=1.0", "Missing", "1.0", rootPath, rootPath, ""]);
        AssertInvoked(
            nameof(ToolLocationHelper.GetPlatformOrFrameworkExtensionSdkReferences),
            ["Missing, Version=1.0", "Missing", "1.0", rootPath, rootPath, "", "Missing", "1.0"]);
        AssertInvoked(nameof(ToolLocationHelper.GetSDKContentFolderPath), ["Missing", "1.0", "Missing", "1.0", "1.0", "Folder", rootPath]);

        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKLocation), ["Missing", version]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKLocation), ["Missing", version, roots, ""]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKLocation), ["Missing", "1.0"]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKLocation), ["Missing", "1.0", rootPath, ""]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKDisplayName), ["Missing", "1.0"]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformSDKDisplayName), ["Missing", "1.0", rootPath, ""]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformsForSDK), ["Missing", version]);
        AssertInvoked(nameof(ToolLocationHelper.GetPlatformsForSDK), ["Missing", version, roots, ""]);
        AssertInvoked(nameof(ToolLocationHelper.GetLatestSDKTargetPlatformVersion), ["Missing", "1.0"]);
        AssertInvoked(nameof(ToolLocationHelper.GetLatestSDKTargetPlatformVersion), ["Missing", "1.0", roots]);

        AssertInvoked(nameof(ToolLocationHelper.GetFoldersInVSInstalls), [version, version, "Subfolder"]);
        AssertInvoked(nameof(ToolLocationHelper.GetFoldersInVSInstallsAsString), ["1.0", "1.0", "Subfolder"]);

        var requiredFile = env.CreateFile(root, "required.txt", "");
        AssertInvoked(
            nameof(ToolLocationHelper.FindRootFolderWhereAllFilesExist),
            [rootPath, Path.GetFileName(requiredFile.Path)],
            rootPath);
    }

    private static void AssertInvoked(string methodName, object?[] arguments)
    {
        WellKnownFunctionResult result =
            WellKnownFunctions.TryInvokeStatic(typeof(ToolLocationHelper), methodName, arguments, FileSystems.Default);

        result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);
    }

    private static void AssertInvoked(string methodName, object?[] arguments, object? expected)
    {
        WellKnownFunctionResult result =
            WellKnownFunctions.TryInvokeStatic(typeof(ToolLocationHelper), methodName, arguments, FileSystems.Default);

        result.Status.ShouldBe(WellKnownFunctionStatus.Invoked);
        result.Result.ShouldBe(expected);
    }
}
