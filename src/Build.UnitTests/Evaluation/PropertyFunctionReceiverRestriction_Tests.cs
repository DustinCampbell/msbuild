// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Win32;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

/// <summary>
///  Tests for the property-function receiver restriction
///  (the <c>Microsoft.Build.RestrictPropertyFunctionReceivers</c> AppContext switch) and its
///  interaction with the <c>Microsoft.Build.EnableAllPropertyFunctions</c> escape hatch.
/// </summary>
/// <param name="output">The output helper for the test.</param>
/// <remarks>
///  Feature switches are set per test and restored to their original state by <see cref="TestEnvironment"/>.
///  The restriction switch intentionally has no environment variable.
/// </remarks>
public class PropertyFunctionReceiverRestriction_Tests(ITestOutputHelper output) : TestBase(output)
{
    private const string RestrictSwitchName = "Microsoft.Build.RestrictPropertyFunctionReceivers";
    private const string EnableAllSwitchName = "Microsoft.Build.EnableAllPropertyFunctions";
    private const string RestrictEnvVarName = "MSBUILDRESTRICTPROPERTYFUNCTIONS";

    protected override ValueTask InitializeAsync()
    {
        // Prevent an inherited switch or environment variable from bypassing the restrictions under test.
        Environment.SetAppContextSwitch(EnableAllSwitchName, false);
        return default;
    }

    private static string Evaluate(string expression, params (string Name, string Value)[] properties)
    {
        var propertyDictionary = new PropertyDictionary<ProjectPropertyInstance>();
        foreach ((string name, string value) in properties)
        {
            propertyDictionary.Set(ProjectPropertyInstance.Create(name, value));
        }

        var expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyDictionary, FileSystems.Default);
        return expander.ExpandIntoStringLeaveEscaped(expression, ExpanderOptions.ExpandProperties, MockElementLocation.Instance);
    }

    private (string Folder, string File) CreateFolderWithFile()
    {
        TransientTestFolder folder = Environment.CreateFolder();
        TransientTestFile file = Environment.CreateFile(folder, "data.txt", "content");

        return (folder.Path, file.Path);
    }

    /// <summary>
    ///  Verifies that receiver restrictions allow chaining string methods.
    /// </summary>
    [Fact]
    public void Restricted_StringChain_IsAllowed()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        Evaluate("$(S.Substring(0,5))", ("S", "HelloWorld")).ShouldBe("Hello");
    }

    /// <summary>
    ///  Verifies that receiver restrictions allow reading the length of an array returned by a string method.
    /// </summary>
    [Fact]
    public void Restricted_ArrayMembers_AreAllowed()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        // ToCharArray() returns char[]; Array members (Length) are permitted because array element
        // access is re-checked at the next chain hop.
        Evaluate("$(S.ToCharArray().Length)", ("S", "HelloWorld")).ShouldBe("10");
    }

    /// <summary>
    ///  Verifies that receiver restrictions do not block allowlisted static functions.
    /// </summary>
    [Fact]
    public void Restricted_AllowListedStaticFunction_StillWorks()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        Evaluate("$([System.Math]::Max(1, 2))").ShouldBe("2");
    }

    /// <summary>
    ///  Verifies that receiver restrictions allow read-only directory navigation through property access.
    /// </summary>
    [Fact]
    public void Restricted_DirectoryInfoReadOnlyNavigation_IsAllowed()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (string folder, string file) = CreateFolderWithFile();

        Evaluate("$([System.IO.Directory]::GetParent($(File)).FullName)", ("File", file))
            .ShouldBe(folder);
        Evaluate("$([System.IO.Directory]::GetParent($(File)).Parent.FullName)", ("File", file))
            .ShouldBe(Directory.GetParent(folder)!.FullName);
    }

    /// <summary>
    ///  Verifies that receiver restrictions block directory creation through a returned directory object.
    /// </summary>
    [Fact]
    public void Restricted_DirectoryInfoMutation_IsBlocked()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (_, string file) = CreateFolderWithFile();

        // CreateSubdirectory changes the file system; it is not in the read-only navigation allowlist
        // and is rejected before invocation (so no directory is created).
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate("$([System.IO.Directory]::GetParent($(File)).CreateSubdirectory('sub'))", ("File", file)));
    }

    /// <summary>
    ///  Verifies that receiver restrictions block file enumeration through a returned directory object.
    /// </summary>
    [Fact]
    public void Restricted_DirectoryInfoEnumeration_IsBlocked()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (_, string file) = CreateFolderWithFile();

        // GetFiles returns FileInfo[] and is not in the navigation allowlist, so the chain to FileInfo
        // (and OpenRead/OpenWrite) stops here.
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate("$([System.IO.Directory]::GetParent($(File)).GetFiles())", ("File", file)));
    }

    /// <summary>
    ///  Verifies that receiver restrictions block a method chain that would open a file for writing.
    /// </summary>
    [Fact]
    public void Restricted_FileOpenChain_IsBlocked()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (_, string file) = CreateFolderWithFile();

        // The chain to OpenWrite is not permitted; it stops at GetFiles, before any FileInfo is produced.
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate("$([System.IO.Directory]::GetParent($(File)).GetFiles().GetValue(0).OpenWrite().CanWrite)", ("File", file)));
    }

    /// <summary>
    ///  Verifies that receiver restrictions prevent obtaining the runtime type of a property value.
    /// </summary>
    [Fact]
    public void Restricted_GetType_IsBlocked()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate("$(S.GetType())", ("S", "HelloWorld")));
    }

    /// <summary>
    ///  Verifies that directory enumeration remains available when receiver restrictions are disabled.
    /// </summary>
    [Fact]
    public void Unrestricted_DirectoryInfoEnumeration_IsAllowed()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, false);
        (_, string file) = CreateFolderWithFile();

        Evaluate("$([System.IO.Directory]::GetParent($(File)).GetFiles().Length)", ("File", file))
            .ShouldBe("1");
    }

    /// <summary>
    ///  Verifies that setting an environment variable does not enable receiver restrictions.
    /// </summary>
    [Fact]
    public void EnvironmentVariable_DoesNotEnableRestriction()
    {
        // An explicit false value could mask environment-variable fallback, so remove the switch override.
        Environment.SetAppContextSwitch(RestrictSwitchName, null);
        Environment.SetEnvironmentVariable(RestrictEnvVarName, "1");
        (_, string file) = CreateFolderWithFile();

        Evaluate("$([System.IO.Directory]::GetParent($(File)).GetFiles().Length)", ("File", file))
            .ShouldBe("1");
    }

    /// <summary>
    ///  Verifies that enabling all property functions bypasses receiver restrictions, including the runtime-type check.
    /// </summary>
    [Fact]
    public void EnableAllPropertyFunctions_BypassesRestriction()
    {
        Environment.SetAppContextSwitch(EnableAllSwitchName, true);
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        Evaluate("$(S.GetType().Name)", ("S", "HelloWorld")).ShouldBe("String");
    }

    /// <summary>
    ///  Verifies that receiver restrictions block file-system property setters invoked by method name.
    /// </summary>
    /// <param name="setterCall">The property setter invocation to reject.</param>
    [Theory]
    [InlineData("set_Attributes(2)")]
    [InlineData("set_CreationTime('2000-01-01')")]
    [InlineData("set_LastWriteTime('2000-01-01')")]
    [InlineData("set_LastAccessTime('2000-01-01')")]
    public void Restricted_FileSystemInfoSetter_IsBlocked(string setterCall)
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (string folder, string file) = CreateFolderWithFile();
        FileAttributes attributesBefore = File.GetAttributes(folder);

        // A property setter is reachable only by its set_ special method name (the property-access
        // form is getter-only - it binds with GetProperty/GetField, never SetProperty). That name is
        // not in the FileSystemInfo navigation allowlist, so the call is rejected at the receiver
        // check, before any argument is bound or the setter runs.
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate($"$([System.IO.Directory]::GetParent($(File)).{setterCall})", ("File", file)));

        File.GetAttributes(folder).ShouldBe(attributesBefore);
    }

    /// <summary>
    ///  Verifies that receiver restrictions allow reading properties of a returned directory object.
    /// </summary>
    [Fact]
    public void Restricted_PropertyGetter_IsAllowed()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (_, string file) = CreateFolderWithFile();

        Evaluate("$([System.IO.Directory]::GetParent($(File)).Attributes)", ("File", file))
            .ShouldContain("Directory");
    }

    /// <summary>
    ///  Verifies that receiver restrictions block direct calls to property getter methods.
    /// </summary>
    [Fact]
    public void Restricted_GetterSpecialMethodName_IsBlocked()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);
        (_, string file) = CreateFolderWithFile();

        // Even the getter's get_ special method name is a method that is not in the navigation
        // allowlist, so it too is blocked; only the property-access form (validated by name) works.
        Should.Throw<InvalidProjectFileException>(() =>
            Evaluate("$([System.IO.Directory]::GetParent($(File)).get_Attributes())", ("File", file)));
    }

    /// <summary>
    ///  Verifies that direct calls to property getter methods remain available when receiver restrictions are disabled.
    /// </summary>
    [Fact]
    public void Unrestricted_SpecialMethodName_IsReachable()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, false);
        (_, string file) = CreateFolderWithFile();

        Evaluate("$([System.IO.Directory]::GetParent($(File)).get_Attributes())", ("File", file))
            .ShouldContain("Directory");
    }

    /// <summary>
    ///  Verifies that receiver restrictions do not permit static functions that mutate process state.
    /// </summary>
    /// <param name="expression">The property-function expression to reject.</param>
    [Theory]
    [InlineData("$([System.Environment]::SetEnvironmentVariable('MSBUILD_RPF_TEST', 'x'))")]
    [InlineData("$([System.Environment]::set_CurrentDirectory('.'))")]
    public void Restricted_StaticStateMutation_StaysBlocked(string expression)
    {
        // Start with an absent value so the assertion detects mutation; cleanup restores any inherited value.
        Environment.SetEnvironmentVariable("MSBUILD_RPF_TEST", null);

        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        // The receiver restriction governs instance "dotting in"; static calls remain governed by
        // the static allowlist. Neither process-state mutator is allowlisted, so both stay blocked
        // (and never run).
        Should.Throw<InvalidProjectFileException>(() => Evaluate(expression));

        System.Environment.GetEnvironmentVariable("MSBUILD_RPF_TEST").ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that reading a nonexistent registry key returns an empty value rather than a receiver-restriction error.
    /// </summary>
    [Fact]
    public void Restricted_RegistryValue_NonexistentKey_IsNotBlocked()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        // GetRegistryValue is a static [MSBuild] intrinsic, gated by the static allowlist (which
        // permits it), not by the receiver restriction. A missing key yields the default (empty) and
        // must not raise the restriction's "function unavailable" error.
        Evaluate(@"$([MSBuild]::GetRegistryValue('HKEY_CURRENT_USER\Software\Microsoft\MSBuild_NonexistentRpfKey', 'None'))")
            .ShouldBe(string.Empty);
    }

    /// <summary>
    ///  Verifies that receiver restrictions allow registry reads through both the intrinsic function and registry prefix.
    /// </summary>
    [WindowsOnlyFact]
    [SupportedOSPlatform("windows")]
    public void Restricted_RegistryRead_StillWorks()
    {
        Environment.SetAppContextSwitch(RestrictSwitchName, true);

        RegistryKey key = Environment.CreateRegistryKey().Key;
        key.SetValue("Value", "RegistryString", RegistryValueKind.String);

        Evaluate($"$([MSBuild]::GetRegistryValue('{key.Name}', 'Value'))")
            .ShouldBe("RegistryString");
        Evaluate($"$(Registry:{key.Name}@Value)")
            .ShouldBe("RegistryString");
    }
}
