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

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
/// Tests for the property-function receiver restriction
/// (the <c>Microsoft.Build.RestrictPropertyFunctionReceivers</c> AppContext switch) and its
/// interaction with the <c>Microsoft.Build.EnableAllPropertyFunctions</c> escape hatch.
/// </summary>
/// <remarks>
/// Both the restriction switch and the <c>EnableAllPropertyFunctions</c> escape hatch are driven
/// through their AppContext switches (set explicitly per test and reset to false afterwards) because
/// an AppContext switch, once set, cannot be returned to the "unset" state in process. The new
/// restriction switch intentionally has no environment variable.
/// </remarks>
[Trait("Category", "expansion")]
public class PropertyFunctionReceiverRestriction_Tests
{
    private const string RestrictSwitch = "Microsoft.Build.RestrictPropertyFunctionReceivers";
    private const string RestrictEnvVar = "MSBUILDRESTRICTPROPERTYFUNCTIONS";

    private static IDisposable SetSwitch(string name, bool value)
    {
        AppContext.SetSwitch(name, value);
        return new SwitchReset(name);
    }

    // AppContext has no API to return a switch to "unset", so reset to false (the untrimmed default,
    // which means "not restricted"), keeping other tests on their default behavior.
    private sealed class SwitchReset : IDisposable
    {
        private readonly string _name;

        public SwitchReset(string name) => _name = name;

        public void Dispose() => AppContext.SetSwitch(_name, false);
    }

    private static (string folder, string file) CreateFolderWithFile(TestEnvironment env)
    {
        string folder = env.CreateFolder().Path;
        string file = Path.Combine(folder, "data.txt");
        File.WriteAllText(file, "content");
        return (folder, file);
    }

    // ---- Restricted: side-effect-free receivers remain callable ----

    [Fact]
    public void Restricted_StringChain_IsAllowed()
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            ExpandProperties("$(S.Substring(0,5))", Properties("S", "HelloWorld"))
                .ShouldBe("Hello");
        }
    }

    [Fact]
    public void Restricted_ArrayMembers_AreAllowed()
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            // ToCharArray() returns char[]; Array members (Length) are permitted because array element
            // access is re-checked at the next chain hop.
            ExpandProperties("$(S.ToCharArray().Length)", Properties("S", "HelloWorld"))
                .ShouldBe("10");
        }
    }

    [Fact]
    public void Restricted_AllowListedStaticFunction_StillWorks()
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            ExpandProperties("$([System.Math]::Max(1, 2))")
                .ShouldBe("2");
        }
    }

    [Fact]
    public void Restricted_DirectoryInfoReadOnlyNavigation_IsAllowed()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (string folder, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, true))
        {
            // GetParent(file) -> DirectoryInfo(folder); FullName / Parent are read-only navigation.
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).FullName)", Properties("File", file))
                .ShouldBe(folder);
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Parent.FullName)", Properties("File", file))
                .ShouldBe(Directory.GetParent(folder)!.FullName);
        }
    }

    // ---- Restricted: chains to non-listed receivers are not permitted ----

    [Fact]
    public void Restricted_DirectoryInfoMutation_IsBlocked()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, true))
        {
            // CreateSubdirectory changes the file system; it is not in the read-only navigation allowlist
            // and is rejected before invocation (so no directory is created).
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([System.IO.Directory]::GetParent($(File)).CreateSubdirectory('sub'))", Properties("File", file)));
        }
    }

    [Fact]
    public void Restricted_DirectoryInfoEnumeration_IsBlocked()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, true))
        {
            // GetFiles returns FileInfo[] and is not in the navigation allowlist, so the chain to FileInfo
            // (and OpenRead/OpenWrite) stops here.
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles())", Properties("File", file)));
        }
    }

    [Fact]
    public void Restricted_FileOpenChain_IsBlocked()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, true))
        {
            // The chain to OpenWrite is not permitted; it stops at GetFiles, before any FileInfo is produced.
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles().GetValue(0).OpenWrite().CanWrite)", Properties("File", file)));
        }
    }

    [Fact]
    public void Restricted_GetType_IsBlocked()
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$(S.GetType())", Properties("S", "HelloWorld")));
        }
    }

    // ---- Default (unrestricted) behavior is preserved ----

    [Fact]
    public void Unrestricted_DirectoryInfoEnumeration_IsAllowed()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, false))
        {
            // With the restriction off, the historical dotting behavior is unchanged.
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles().Length)", Properties("File", file))
                .ShouldBe("1");
        }
    }

    // ---- The new restriction switch has no environment-variable opt-in ----

    [Fact]
    public void EnvironmentVariable_DoesNotEnableRestriction()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        // The new switch deliberately has no environment-variable opt-in; setting the (non-wired)
        // variable must not turn the restriction on.
        env.SetEnvironmentVariable(RestrictEnvVar, "1");

        ExpandProperties("$([System.IO.Directory]::GetParent($(File)).GetFiles().Length)", Properties("File", file))
            .ShouldBe("1");
    }

    // ---- EnableAll escape hatch bypasses the restriction ----

    [Fact]
    public void EnableAllPropertyFunctions_BypassesRestriction()
    {
        using TestEnvironment env = TestEnvironment.Create();

        using (SetSwitch("Microsoft.Build.EnableAllPropertyFunctions", true))
        using (SetSwitch(RestrictSwitch, true))
        {
            // EnableAll takes precedence over the restriction (and over the GetType block), preserving
            // the documented "anything goes" escape hatch.
            ExpandProperties("$(S.GetType().Name)", Properties("S", "HelloWorld"))
                .ShouldBe("String");
        }
    }

    // ---- Restricted: property setters and their special method names cannot run ----

    [Theory]
    [InlineData("set_Attributes(2)")] // FileSystemInfo.Attributes setter mutates the file system
    [InlineData("set_CreationTime('2000-01-01')")]
    [InlineData("set_LastWriteTime('2000-01-01')")]
    [InlineData("set_LastAccessTime('2000-01-01')")]
    public void Restricted_FileSystemInfoSetter_IsBlocked(string setterCall)
    {
        using TestEnvironment env = TestEnvironment.Create();
        (string folder, string file) = CreateFolderWithFile(env);
        FileAttributes attributesBefore = File.GetAttributes(folder);

        using (SetSwitch(RestrictSwitch, true))
        {
            // A property setter is reachable only by its set_ special method name (the property-access
            // form is getter-only - it binds with GetProperty/GetField, never SetProperty). That name is
            // not in the FileSystemInfo navigation allowlist, so the call is rejected at the receiver
            // check, before any argument is bound or the setter runs.
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties($"$([System.IO.Directory]::GetParent($(File)).{setterCall})", Properties("File", file)));
        }

        // The setter never executed: the directory on disk is unchanged.
        File.GetAttributes(folder).ShouldBe(attributesBefore);
    }

    [Fact]
    public void Restricted_PropertyGetter_IsAllowed()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, true))
        {
            // Reading the property through property-access syntax (the getter) is allowed; only the
            // matching setter is blocked.
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).Attributes)", Properties("File", file))
                .ShouldNotBeNull()
                .ShouldContain("Directory");
        }
    }

    [Fact]
    public void Restricted_GetterSpecialMethodName_IsBlocked()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, true))
        {
            // Even the getter's get_ special method name is a method that is not in the navigation
            // allowlist, so it too is blocked; only the property-access form (validated by name) works.
            Should.Throw<InvalidProjectFileException>(() =>
                ExpandProperties("$([System.IO.Directory]::GetParent($(File)).get_Attributes())", Properties("File", file)));
        }
    }

    [Fact]
    public void Unrestricted_SpecialMethodName_IsReachable()
    {
        using TestEnvironment env = TestEnvironment.Create();
        (_, string file) = CreateFolderWithFile(env);

        using (SetSwitch(RestrictSwitch, false))
        {
            // With the restriction off, get_/set_ special method names are genuinely invocable as
            // methods - which is exactly why the blocked-setter tests above are meaningful.
            ExpandProperties("$([System.IO.Directory]::GetParent($(File)).get_Attributes())", Properties("File", file))
                .ShouldNotBeNull()
                .ShouldContain("Directory");
        }
    }

    // ---- Restricted: state-mutating static functions stay blocked ----

    [Theory]
    [InlineData("$([System.Environment]::SetEnvironmentVariable('MSBUILD_RPF_TEST', 'x'))")]
    [InlineData("$([System.Environment]::set_CurrentDirectory('.'))")]
    public void Restricted_StaticStateMutation_StaysBlocked(string expression)
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            // The receiver restriction governs instance "dotting in"; static calls remain governed by
            // the static allowlist. Neither process-state mutator is allowlisted, so both stay blocked
            // (and never run).
            Should.Throw<InvalidProjectFileException>(() => ExpandProperties(expression));
        }

        Environment.GetEnvironmentVariable("MSBUILD_RPF_TEST").ShouldBeNull();
    }

    // ---- Restricted: registry reads are unaffected ----

    [Fact]
    public void Restricted_RegistryValue_NonexistentKey_IsNotBlocked()
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            // GetRegistryValue is a static [MSBuild] intrinsic, gated by the static allowlist (which
            // permits it), not by the receiver restriction. A missing key yields the default (empty) and
            // must not raise the restriction's "function unavailable" error.
            ExpandProperties(@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_NonexistentRpfKey', 'None'))")
                .ShouldBe(string.Empty);
        }
    }

    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void Restricted_RegistryRead_StillWorks()
    {
        using (SetSwitch(RestrictSwitch, true))
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\MSBuild_test_rpf");
                key.SetValue("Value", "RegistryString", RegistryValueKind.String);

                // Both registry read paths - the [MSBuild]::GetRegistryValue function and the
                // $(Registry:...) prefix - are read-only and unaffected by the receiver restriction.
                ExpandProperties(@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test_rpf', 'Value'))")
                    .ShouldBe("RegistryString");
                ExpandProperties(@"$(Registry:HKEY_CURRENT_USER\Software\Microsoft\MSBuild_test_rpf@Value)")
                    .ShouldBe("RegistryString");
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKey(@"Software\Microsoft\MSBuild_test_rpf");
            }
        }
    }
}
