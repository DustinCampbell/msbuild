// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public sealed partial class ToolLocationHelper_Tests
{
    /// <summary>
    ///  Verifies that an empty API contract collection produces no references.
    /// </summary>
    [Fact]
    public void GetApiContractReferencesHandlesEmptyContracts()
        => ToolLocationHelper.GetApiContractReferences(apiContracts: [], targetPlatformSdkRoot: string.Empty)
            .ShouldBeEmpty();

    /// <summary>
    ///  Verifies that a <see langword="null"/> API contract collection produces no references.
    /// </summary>
    [Fact]
    public void GetApiContractReferencesHandlesNullContracts()
        => ToolLocationHelper.GetApiContractReferences(apiContracts: null, targetPlatformSdkRoot: string.Empty)
            .ShouldBeEmpty();

    /// <summary>
    ///  Verifies that a nonexistent SDK root produces no API contract references.
    /// </summary>
    [Fact]
    [Trait("Category", "netcore-osx-failing")]
    [Trait("Category", "netcore-linux-failing")]
    public void GetApiContractReferencesHandlesNonExistingLocation()
    {
        string missingSdkRoot = Path.Combine(Environment.DefaultTestDirectory.Path, "DoesNotExist");

        ToolLocationHelper.GetApiContractReferences(
            apiContracts: [new ApiContract { Name = "Foo", Version = "Bar" }],
            targetPlatformSdkRoot: missingSdkRoot)
            .ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that all WinMD files for the requested API contract are found.
    /// </summary>
    [WindowsOnlyFact]
    public void GetApiContractReferencesFindsWinMDs()
    {
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder referenceDirectory = root.CreateDirectory(Path.Combine("References", "Foo", "Bar"));
        Environment.CreateFile(referenceDirectory, "One.winmd", "First");
        Environment.CreateFile(referenceDirectory, "Two.winmd", "Second");
        Environment.CreateFile(referenceDirectory, "Three.winmd", "Third");

        ToolLocationHelper.GetApiContractReferences(
            apiContracts: [new ApiContract { Name = "Foo", Version = "Bar" }],
            targetPlatformSdkRoot: root.Path)
            .Length.ShouldBe(3);
    }

    /// <summary>
    ///  Verifies that API contract WinMD files are found under the requested SDK version directory.
    /// </summary>
    [WindowsOnlyFact]
    public void GetApiContractReferencesFindsVersionedWinMDs()
    {
        const string SdkVersion = "10.0.12345.0";
        TransientTestFolder root = Environment.CreateFolder();
        TransientTestFolder referenceDirectory = root.CreateDirectory(Path.Combine("References", SdkVersion, "Foo", "Bar"));
        Environment.CreateFile(referenceDirectory, "One.winmd", "First");

        ToolLocationHelper.GetApiContractReferences(
            apiContracts: [new ApiContract { Name = "Foo", Version = "Bar" }],
            targetPlatformSdkRoot: root.Path,
            targetPlatformSdkVersion: SdkVersion)
            .Length.ShouldBe(1);
    }
}
