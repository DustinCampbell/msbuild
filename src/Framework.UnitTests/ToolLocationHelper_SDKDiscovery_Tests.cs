// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    [Fact]
    public void GatherExtensionSDKsInvalidVersionDirectory()
    {
        TransientTestFolder root = Environment.CreateFolder();
        root.CreateDirectory(Path.Combine("Foo", "Bar"));

        DirectoryInfo info = new(root.Path);
        TargetPlatformSDK sdk = new("Foo", new Version(0, 0), string.Empty);
        ToolLocationHelper.GatherExtensionSDKs(info, sdk);
        sdk.ExtensionSDKs.Count.ShouldBe(0);
    }

    [Fact]
    public void GatherExtensionSDKsNoManifest()
    {
        TransientTestFolder root = Environment.CreateFolder();
        root.CreateDirectory(Path.Combine("Foo", "1.0"));

        DirectoryInfo info = new(root.Path);
        TargetPlatformSDK sdk = new("Foo", new Version(0, 0), string.Empty);
        ToolLocationHelper.GatherExtensionSDKs(info, sdk);
        sdk.ExtensionSDKs.Count.ShouldBe(0);
    }

    [Fact]
    public void GatherExtensionSDKsEmptyManifest()
    {
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder sdkDirectory = root.CreateDirectory(Path.Combine("Foo", "1.0"));
        Environment.CreateFile(sdkDirectory, "SDKManifest.xml", string.Empty);

        DirectoryInfo info = new(root.Path);
        TargetPlatformSDK sdk = new("Foo", new Version(0, 0), string.Empty);
        ToolLocationHelper.GatherExtensionSDKs(info, sdk);
        sdk.ExtensionSDKs.Count.ShouldBe(1);
    }

    [Fact]
    public void GatherExtensionSDKsGarbageManifest()
    {
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder sdkDirectory = root.CreateDirectory(Path.Combine("Foo", "1.0"));
        Environment.CreateFile(sdkDirectory, "SDKManifest.xml", "Garbaggggge");

        DirectoryInfo info = new(root.Path);
        TargetPlatformSDK sdk = new("Foo", new Version(0, 0), string.Empty);
        ToolLocationHelper.GatherExtensionSDKs(info, sdk);
        sdk.ExtensionSDKs.Count.ShouldBe(1);
    }
}
