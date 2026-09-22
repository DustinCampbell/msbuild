// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Build.Exceptions;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using static Microsoft.Build.UnitTests.Expansion.ExpansionHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

/// <summary>
///  Tests the property-function receiver restriction and its interaction with the
///  <c>Microsoft.Build.EnableAllPropertyFunctions</c> escape hatch.
/// </summary>
/// <remarks>
///  Switches are isolated through <see cref="TestEnvironment"/>, which restores their original values
///  or absence. The restriction switch intentionally has no environment variable.
/// </remarks>
[Trait("Category", "expansion")]
public class PropertyFunctionReceiverRestriction_Tests(ITestOutputHelper output)
{
    private const string RestrictEnvVar = "MSBUILDRESTRICTPROPERTYFUNCTIONS";

    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void Restricted_StringChain_IsAllowed()
    {
        using var env = CreateEnvironment();

        ExpandProperties("$(S.Substring(0,5))", Properties("S", "HelloWorld"))
            .ShouldBe("Hello");
    }

    [Fact]
    public void Restricted_ArrayMembers_AreAllowed()
    {
        using var env = CreateEnvironment();

        // Array members are permitted; element access is re-checked at the next chain hop.
        ExpandProperties("$(S.ToCharArray().Length)", Properties("S", "HelloWorld"))
            .ShouldBe(10.ToResultString());
    }

    [Fact]
    public void Restricted_AllowListedStaticFunction_StillWorks()
    {
        using var env = CreateEnvironment();

        ExpandProperties("$([System.Math]::Max(1, 2))")
            .ShouldBe(2.ToResultString());
    }

    [Fact]
    public void Restricted_DirectoryInfoReadOnlyNavigation_IsAllowed()
    {
        using var env = CreateEnvironment();
        (string folder, string file) = CreateFolderWithFile(env);

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).FullName)", Properties("File", file))
            .ShouldBe(folder);
        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Parent.FullName)", Properties("File", file))
            .ShouldBe(Directory.GetParent(folder)!.FullName);
    }

    [Fact]
    public void Restricted_DirectoryInfoMutation_IsBlocked()
    {
        using var env = CreateEnvironment();
        (string folder, string file) = CreateFolderWithFile(env);

        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).CreateSubdirectory('sub'))", Properties("File", file)));

        Directory.Exists(Path.Combine(folder, "sub")).ShouldBeFalse();
    }

    [Fact]
    public void Restricted_DirectoryInfoEnumeration_IsBlocked()
    {
        using var env = CreateEnvironment();
        (_, string file) = CreateFolderWithFile(env);

        // GetFiles returns FileInfo[] and is not in the navigation allowlist.
        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles())", Properties("File", file)));
    }

    [Fact]
    public void Restricted_FileOpenChain_IsBlocked()
    {
        using var env = CreateEnvironment();
        (_, string file) = CreateFolderWithFile(env);

        // The chain stops at GetFiles, before any FileInfo is produced.
        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties(
                "$([System.IO.Directory]::GetParent($(File)).GetFiles().GetValue(0).OpenWrite().CanWrite)",
                Properties("File", file)));
    }

    [Fact]
    public void Restricted_GetType_IsBlocked()
    {
        using var env = CreateEnvironment();

        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$(S.GetType())", Properties("S", "HelloWorld")));
    }

    [Fact]
    public void Unrestricted_DirectoryInfoEnumeration_IsAllowed()
    {
        using var env = CreateEnvironment(restricted: false);
        (_, string file) = CreateFolderWithFile(env);

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles().Length)", Properties("File", file))
            .ShouldBe(1.ToResultString());
    }

    [Fact]
    public void EnvironmentVariable_DoesNotEnableRestriction()
    {
        using var env = CreateEnvironment();
        env.RemoveAppContextSwitch(AppContextSwitch.RestrictPropertyFunctionReceivers);
        (_, string file) = CreateFolderWithFile(env);

        // With the switch unset, the environment variable must not enable the restriction.
        env.SetEnvironmentVariable(RestrictEnvVar, "1");

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles().Length)", Properties("File", file))
            .ShouldBe(1.ToResultString());
    }

    [Fact]
    public void EnableAllPropertyFunctions_BypassesRestriction()
    {
        using var env = CreateEnvironment();
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, true);

        // EnableAll takes precedence over both the receiver restriction and the GetType block.
        ExpandProperties("$(S.GetType().Name)", Properties("S", "HelloWorld"))
            .ShouldBe("String");
    }

    [Theory]
    [InlineData("set_Attributes(2)")] // FileSystemInfo.Attributes setter mutates the file system
    [InlineData("set_CreationTime('2000-01-01')")]
    [InlineData("set_LastWriteTime('2000-01-01')")]
    [InlineData("set_LastAccessTime('2000-01-01')")]
    public void Restricted_FileSystemInfoSetter_IsBlocked(string setterCall)
    {
        using var env = CreateEnvironment();
        (string folder, string file) = CreateFolderWithFile(env);
        FileAttributes attributesBefore = File.GetAttributes(folder);

        // Property-access syntax is getter-only. The setter's special method name is not allowlisted.
        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties($"$([System.IO.Directory]::GetParent($(File)).{setterCall})", Properties("File", file)));

        // The setter never executed: the directory on disk is unchanged.
        File.GetAttributes(folder).ShouldBe(attributesBefore);
    }

    [Fact]
    public void Restricted_PropertyGetter_IsAllowed()
    {
        using var env = CreateEnvironment();
        (_, string file) = CreateFolderWithFile(env);

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Attributes)", Properties("File", file))
            .ShouldNotBeNull()
            .ShouldContain("Directory");
    }

    [Fact]
    public void Restricted_GetterSpecialMethodName_IsBlocked()
    {
        using var env = CreateEnvironment();
        (_, string file) = CreateFolderWithFile(env);

        // Only property-access syntax is allowlisted, not the getter's special method name.
        Should.Throw<InvalidProjectFileException>(() =>
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).get_Attributes())", Properties("File", file)));
    }

    [Fact]
    public void Unrestricted_SpecialMethodName_IsReachable()
    {
        using var env = CreateEnvironment(restricted: false);
        (_, string file) = CreateFolderWithFile(env);

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).get_Attributes())", Properties("File", file))
            .ShouldNotBeNull()
            .ShouldContain("Directory");
    }

    [Theory]
    [InlineData("$([System.Environment]::SetEnvironmentVariable('MSBUILD_RPF_TEST', 'x'))")]
    [InlineData("$([System.Environment]::set_CurrentDirectory('.'))")]
    public void Restricted_StaticStateMutation_StaysBlocked(string expression)
    {
        using var env = CreateEnvironment();
        env.SetEnvironmentVariable("MSBUILD_RPF_TEST", null);
        env.SetCurrentDirectory(Directory.GetCurrentDirectory());

        // Static calls remain governed by the static allowlist, not the receiver restriction.
        Should.Throw<InvalidProjectFileException>(() => ExpandProperties(expression));

        Environment.GetEnvironmentVariable("MSBUILD_RPF_TEST").ShouldBeNull();
    }

    [Fact]
    public void Restricted_RegistryValue_NonexistentKey_IsNotBlocked()
    {
        using var env = CreateEnvironment();
        string keyPath = $@"Software\Microsoft\MSBuild_NonexistentRpfKey_{Guid.NewGuid():N}";

        // The static intrinsic remains allowlisted; a missing key yields the empty default.
        ExpandProperties($@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\{keyPath}', 'None'))")
            .ShouldBeEmpty();
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void Restricted_RegistryRead_StillWorks()
    {
        using var env = CreateEnvironment();
        string keyPath = $@"Software\Microsoft\MSBuild_test_rpf_{Guid.NewGuid():N}";

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue("Value", "RegistryString", RegistryValueKind.String);

            // Both registry read paths are unaffected by the receiver restriction.
            ExpandProperties($@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\{keyPath}', 'Value'))")
                .ShouldBe("RegistryString");
            ExpandProperties($@"$(Registry:HKEY_CURRENT_USER\{keyPath}@Value)")
                .ShouldBe("RegistryString");
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKey(keyPath);
        }
    }

    private TestEnvironment CreateEnvironment(bool restricted = true)
    {
        var env = TestEnvironment.Create(_output);
        env.SetAppContextSwitch(AppContextSwitch.EnableAllPropertyFunctions, false);
        env.SetAppContextSwitch(AppContextSwitch.RestrictPropertyFunctionReceivers, restricted);

        return env;
    }

    private static (string Folder, string File) CreateFolderWithFile(TestEnvironment env)
    {
        var folder = env.CreateFolder();
        var file = env.CreateFile(folder, "data.txt", "content");

        return (folder.Path, file.Path);
    }
}
