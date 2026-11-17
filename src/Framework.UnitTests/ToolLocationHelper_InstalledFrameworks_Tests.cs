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
    ///  Verifies that a missing reference-assembly root produces no framework identifiers.
    /// </summary>
    [Fact]
    public void GetFrameworkIdentifiersNoReferenceAssemblies()
        => ToolLocationHelper.GetFrameworkIdentifiers(
            NativeMethodsShared.IsWindows ? @"f:\IDontExistAtAll" : "/IDontExistAtAll")
            .ShouldBeEmpty();

    /// <summary>
    ///  Verifies that a missing reference-assembly root yields <see langword="null"/> for the highest framework version.
    /// </summary>
    [Fact]
    public void HighestVersionOfTargetFrameworkIdentifierRootDoesNotExist()
        => ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(
            NativeMethodsShared.IsWindows ? @"f:\IDontExistAtAll" : "/IDontExistAtAll",
            ".UnKNownFramework")
            .ShouldBeNull();

    /// <summary>
    ///  Verifies that a framework without version-named directories yields <see langword="null"/> for its highest version.
    /// </summary>
    [Fact]
    public void HighestVersionOfTargetFrameworkIdentifierRootNoVersions()
    {
        TransientTestFolder root = Environment.CreateFolder();
        root.CreateDirectory(Path.Combine(".UnknownFramework", "NotAVersion"));

        ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(root.Path, ".UnKNownFramework")
            .ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that the highest framework version is selected when multiple version directories exist.
    /// </summary>
    [Fact]
    public void HighestVersionOfTargetFrameworkIdentifierRootMultipleVersions()
    {
        TransientTestFolder root = Environment.CreateFolder();
        root.CreateDirectory(Path.Combine(".UnknownFramework", "v1.0"));
        root.CreateDirectory(Path.Combine(".UnknownFramework", "v2.0"));
        root.CreateDirectory(Path.Combine(".UnknownFramework", "v4.0"));

        ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(root.Path, ".UnknownFramework")
            .ShouldNotBeNull()
            .Version.Major.ShouldBe(4);
    }
}
