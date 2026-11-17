// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WIN32_REGISTRY

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Win32;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests.Definition;

/// <summary>
///  Tests for <see cref="ToolsetRegistryReader"/>.
/// </summary>
/// <param name="output">The output helper for the test.</param>
[SupportedOSPlatform("windows")]
public class ToolsetRegistryReader_Tests(ITestOutputHelper output) : TestBase(output)
{
    private TransientTestRegistryKey _registryKey = null!;

    // Subkey for the current MSBuild assembly version.
    private RegistryKey _currentVersionRegistryKey = null!;

    // Subkey "ToolsVersions"
    private RegistryKey _toolsVersionsRegistryKey = null!;

    protected override ValueTask InitializeAsync()
    {
        _registryKey = Environment.CreateRegistryKey();
        _currentVersionRegistryKey = _registryKey.Key.CreateSubKey(Constants.AssemblyVersion);
        RegisterDisposable(_currentVersionRegistryKey);
        _toolsVersionsRegistryKey = _registryKey.Key.CreateSubKey("ToolsVersions");
        RegisterDisposable(_toolsVersionsRegistryKey);

        Environment.SetEnvironmentVariable("VisualStudioVersion", null);
        return default;
    }

    private ToolsetRegistryReader GetStandardRegistryReader()
    {
#pragma warning disable CA2000 // TestBase owns disposal through RegisterDisposable; the returned reader still needs the key.
        var registry = new MockRegistryKey(_registryKey.SubKeyPath);
#pragma warning restore CA2000
        RegisterDisposable(registry);

        using var collection = new ProjectCollection();
        return new ToolsetRegistryReader(collection.EnvironmentProperties, globalProperties: [], registry);
    }

    /// <summary>
    ///  Verifies that a deleted registry root produces no toolsets without throwing.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_DeletedKey()
    {
        // Close the fixture handles before deleting the root so the reader sees a missing key.
        _currentVersionRegistryKey.Dispose();
        _toolsVersionsRegistryKey.Dispose();
        _registryKey.Revert();

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);
        toolsets.ShouldBeEmpty();
    }

    /// <summary>
    ///  Verifies that installation creates the expected toolsets and default tools version in the registry.
    /// </summary>
    [Fact(Skip = "Test requires installed toolset.")]
    public void DefaultValuesInRegistryCreatedBySetup()
    {
        if (NativeMethodsShared.IsUnixLike)
        {
            return; // "TODO: under Unix this runs out of stack. Investigate"
        }

        using var collection = new ProjectCollection();
        ToolsetReader reader = new ToolsetRegistryReader(collection.EnvironmentProperties, globalProperties: []);  // we don't use the test registry key because we want to verify the install

        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        // Check the values in the data
        toolsets.ContainsKey("4.0").ShouldBeTrue(); // "Tools version 4.0 should be defined by default"
        toolsets.ContainsKey(ObjectModelHelpers.MSBuildDefaultToolsVersion)
            .ShouldBeTrue($"Tools version {ObjectModelHelpers.MSBuildDefaultToolsVersion} should be defined by default");
        defaultToolsVersion.ShouldBe("2.0"); // "Default tools version should be 2.0"
    }

    /// <summary>
    ///  Verifies that reading the registry without a configured default tools version returns <see langword="null"/>.
    /// </summary>
    [WindowsOnlyFact]
    public void DefaultValueInRegistryDoesNotExist()
    {
        using var collection = new ProjectCollection();
        using var registry = new MockRegistryKey(_registryKey.SubKeyPath, "3.5" /* fail to find subkey 3.5 */);
        ToolsetReader reader = new ToolsetRegistryReader(collection.EnvironmentProperties, globalProperties: [], registry);

        string defaultToolsVersion = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out _,
            out _);

        defaultToolsVersion.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that an empty toolset registry key produces no toolsets or default tools version.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_NoSubkeyNoValues()
    {
        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.ShouldBeEmpty();
        defaultToolsVersion.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that values directly under the <c>ToolsVersions</c> key do not define toolsets.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_NoSubkeysOnlyValues()
    {
        _toolsVersionsRegistryKey.SetValue("Name1", "Value1");
        _toolsVersionsRegistryKey.SetValue("Name2", "Value2");

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.ShouldBeEmpty();
        defaultToolsVersion.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that a single toolset's tools path is read without adding extra properties.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_OnlyOneSubkey()
    {
        string xdir = NativeMethodsShared.IsWindows ? @"c:\xxx" : "/xxx";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", xdir);

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        defaultToolsVersion.ShouldBeNull();
        toolsets.ShouldHaveSingleItem("tv1", toolset =>
        {
            toolset.Properties.ShouldBeEmpty();
            toolset.ToolsPath.ShouldBe(xdir, StringCompareShould.IgnoreCase);
        });
    }

    /// <summary>
    ///  Verifies that tools paths and custom properties are read from multiple registry toolsets.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_Basic()
    {
        string xdir = NativeMethodsShared.IsWindows ? @"c:\xxx" : "/xxx";
        string ydir = NativeMethodsShared.IsWindows ? @"c:\yyy" : "/yyy";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", xdir);
        key1.SetValue("name1", "value1");

        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
        key2.SetValue("name2", "value2");
        key2.SetValue("msbuildtoolspath", ydir);

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.Count.ShouldBe(2);
        toolsets.ShouldContain("tv1", toolset =>
        {
            toolset.ToolsPath.ShouldBe(xdir);
            toolset.Properties.ShouldHaveSingleItem("name1", property => property.EvaluatedValue.ShouldBe("value1"));
        });
        toolsets.ShouldContain("tv2", toolset =>
        {
            toolset.ToolsPath.ShouldBe(ydir);
            toolset.Properties.ShouldHaveSingleItem("name2", property => property.EvaluatedValue.ShouldBe("value2"));
        });
    }

    /// <summary>
    ///  Verifies that non-string toolset property values are rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_NonStringData()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
            key1.SetValue("msbuildtoolspath", @"c:\xxx");
            key1.SetValue("name1", "value1");

            using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
            key2.SetValue("msbuildtoolspath", @"c:\xxx");
            key2.SetValue("name2", new[] { "value2a", "value2b" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: false,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that toolsets include their immediate subtoolsets and the properties defined under each.
    /// </summary>
    /// <remarks>
    ///  The registry contains the following toolset and subtoolset hierarchy:
    ///  <code>
    ///   ToolsVersions
    ///       tv1
    ///           SubKey1
    ///       tv2
    ///           SubKey2
    ///           SubKey3
    ///  </code>
    /// </remarks>
    [WindowsOnlyFact]
    public void ReadRegistry_HasSubToolsets()
    {
        string xdir = NativeMethodsShared.IsWindows ? @"c:\xxx" : "/xxx";
        string ydir = NativeMethodsShared.IsWindows ? @"c:\yyy" : "/yyy";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", xdir);
        key1.SetValue("name1", "value1");

        using RegistryKey subKey1 = key1.CreateSubKey("SubKey1");
        subKey1.SetValue("name1a", "value1a");
        subKey1.SetValue("name2a", "value2a");

        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
        key2.SetValue("msbuildtoolspath", ydir);
        key2.SetValue("name2", "value2");

        using RegistryKey subKey2 = key2.CreateSubKey("SubKey2");
        subKey2.SetValue("name3a", "value3a");
        subKey2.SetValue("name2a", "value2a");

        using RegistryKey subKey3 = key2.CreateSubKey("SubKey3");
        subKey3.SetValue("name4a", "value4a");
        subKey3.SetValue("name5a", "value5a");

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.Count.ShouldBe(2);
        toolsets.ShouldContain("tv1", toolset =>
        {
            toolset.ToolsPath.ShouldBe(xdir);
            toolset.Properties.ShouldHaveSingleItem("name1", property => property.EvaluatedValue.ShouldBe("value1"));
            toolset.SubToolsets.ShouldHaveSingleItem("SubKey1", subToolset =>
            {
                subToolset.Properties.Count.ShouldBe(2);
                subToolset.Properties.ShouldContain("name1a", property => property.EvaluatedValue.ShouldBe("value1a"));
                subToolset.Properties.ShouldContain("name2a", property => property.EvaluatedValue.ShouldBe("value2a"));
            });
        });
        toolsets.ShouldContain("tv2", toolset =>
        {
            toolset.ToolsPath.ShouldBe(ydir);
            toolset.Properties.ShouldHaveSingleItem("name2", property => property.EvaluatedValue.ShouldBe("value2"));
            toolset.SubToolsets.Count.ShouldBe(2);
            toolset.SubToolsets.ShouldContain("SubKey2", subToolset =>
            {
                subToolset.Properties.Count.ShouldBe(2);
                subToolset.Properties.ShouldContain("name3a", property => property.EvaluatedValue.ShouldBe("value3a"));
                subToolset.Properties.ShouldContain("name2a", property => property.EvaluatedValue.ShouldBe("value2a"));
            });
            toolset.SubToolsets.ShouldContain("SubKey3", subToolset =>
            {
                subToolset.Properties.Count.ShouldBe(2);
                subToolset.Properties.ShouldContain("name4a", property => property.EvaluatedValue.ShouldBe("value4a"));
                subToolset.Properties.ShouldContain("name5a", property => property.EvaluatedValue.ShouldBe("value5a"));
            });
        });
    }

    /// <summary>
    ///  Verifies that subkeys below a subtoolset are ignored.
    /// </summary>
    /// <remarks>
    ///  The nested <c>SubSubKey1</c> key must not contribute properties to <c>SubKey1</c>:
    ///  <code>
    ///   ToolsVersions
    ///       tv1
    ///           SubKey1
    ///               SubSubKey1
    ///  </code>
    /// </remarks>
    [WindowsOnlyFact]
    public void ReadRegistry_IgnoreSubToolsetSubKeys()
    {
        string xdir = NativeMethodsShared.IsWindows ? @"c:\xxx" : "/xxx";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", xdir);
        key1.SetValue("name1", "value1");

        using RegistryKey subKey1 = key1.CreateSubKey("SubKey1");
        subKey1.SetValue("name1a", "value1a");
        subKey1.SetValue("name2a", "value2a");

        using RegistryKey subSubKey1 = subKey1.CreateSubKey("SubSubKey1");
        subSubKey1.SetValue("name2b", "value2b");

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.ShouldHaveSingleItem("tv1", toolset =>
        {
            toolset.ToolsPath.ShouldBe(xdir);
            toolset.Properties.ShouldHaveSingleItem("name1", property => property.EvaluatedValue.ShouldBe("value1"));
            toolset.SubToolsets.ShouldHaveSingleItem("SubKey1", subToolset =>
            {
                subToolset.Properties.Count.ShouldBe(2);
                subToolset.Properties.ShouldContain("name1a", property => property.EvaluatedValue.ShouldBe("value1a"));
                subToolset.Properties.ShouldContain("name2a", property => property.EvaluatedValue.ShouldBe("value2a"));
            });
        });
    }

    /// <summary>
    ///  Verifies that selected subtoolset properties override base-toolset properties, including empty values.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_SubToolsetOverridesBaseToolsetEntries()
    {
        string xdir = NativeMethodsShared.IsWindows ? @"c:\xxx" : "/xxx";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", xdir);
        key1.SetValue("name1", "value1");
        key1.SetValue("name2", "value2");

        using RegistryKey subKey1 = key1.CreateSubKey("Foo");
        subKey1.SetValue("name1", "value1a");
        subKey1.SetValue("name2", "");

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.ShouldHaveSingleItem("tv1", toolset =>
        {
            toolset.ToolsPath.ShouldBe(xdir);
            toolset.Properties.Count.ShouldBe(2);
            toolset.Properties.ShouldContain("name1", property => property.EvaluatedValue.ShouldBe("value1"));
            toolset.Properties.ShouldContain("name2", property => property.EvaluatedValue.ShouldBe("value2"));
            toolset.SubToolsets.ShouldHaveSingleItem("Foo", subToolset =>
            {
                subToolset.Properties.Count.ShouldBe(2);
                subToolset.Properties.ShouldContain("name1", property => property.EvaluatedValue.ShouldBe("value1a"));
                subToolset.Properties.ShouldContain("name2", property => property.EvaluatedValue.ShouldBeEmpty());
            });

            // Check when requesting the final evaluated value of the property in the context of its sub-toolset
            // that the sub-toolset overrides
            toolset.GetProperty("name1", "Foo").EvaluatedValue.ShouldBe("value1a");
            toolset.GetProperty("name2", "Foo").EvaluatedValue.ShouldBeEmpty();
        });
    }

    /// <summary>
    ///  Verifies that base-toolset properties remain unchanged when no subtoolset is selected.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadRegistry_UnselectedSubToolsetIsIgnored()
    {
        string xdir = NativeMethodsShared.IsWindows ? @"c:\xxx" : "/xxx";

        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", xdir);
        key1.SetValue("name1", "value1");
        key1.SetValue("name2", "value2");

        using RegistryKey subKey1 = key1.CreateSubKey("Foo");
        subKey1.SetValue("name1", "value1a");
        subKey1.SetValue("name2", "");

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];
        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.ShouldHaveSingleItem("tv1", toolset =>
        {
            toolset.ToolsPath.ShouldBe(xdir);
            toolset.Properties.Count.ShouldBe(2);
            toolset.Properties.ShouldContain("name1", property => property.EvaluatedValue.ShouldBe("value1"));
            toolset.Properties.ShouldContain("name2", property => property.EvaluatedValue.ShouldBe("value2"));
        });
    }

    /// <summary>
    ///  Verifies that the configured default tools version is read from the current-version registry key.
    /// </summary>
    [WindowsOnlyFact]
    public void GetDefaultToolsVersionFromRegistry_Basic()
    {
        _currentVersionRegistryKey.SetValue("DefaultToolsVersion", "tv1");
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1"); // Need matching tools version
        key1.SetValue("msbuildtoolspath", @"c:\xxx");

        ToolsetReader reader = GetStandardRegistryReader();
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out _,
            out _);

        defaultToolsVersion.ShouldBe("tv1");
    }

    /// <summary>
    ///  Verifies that a missing <c>DefaultToolsVersion</c> registry value yields <see langword="null"/>.
    /// </summary>
    [WindowsOnlyFact]
    public void GetDefaultToolsVersionFromRegistry_DefaultValueNotSet()
    {
        ToolsetReader reader = GetStandardRegistryReader();
        string defaultToolsVersion = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out _,
            out _);

        defaultToolsVersion.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that a non-string <c>DefaultToolsVersion</c> registry value is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void GetDefaultToolsVersionFromRegistry_NonStringData()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _currentVersionRegistryKey.SetValue("DefaultToolsVersion", new[] { "2.0.xxxx.a", "2.0.xxxx.b" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();

            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: false,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that the configured override-task path is read from the current-version registry key.
    /// </summary>
    [WindowsOnlyFact]
    public void GetOverrideTasksPathFromRegistry_Basic()
    {
        if (NativeMethodsShared.IsUnixLike)
        {
            return; // "Registry is not supported under Unix"
        }

        _currentVersionRegistryKey.SetValue("MsBuildOverrideTasksPath", @"c:\Foo");

        ToolsetReader reader = GetStandardRegistryReader();
        _ = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out string msbuildOverrideTasksPath,
            out _);

        msbuildOverrideTasksPath.ShouldBe(@"c:\Foo");
    }

    /// <summary>
    ///  Verifies that a missing <c>MsBuildOverrideTasksPath</c> registry value yields <see langword="null"/>.
    /// </summary>
    [WindowsOnlyFact]
    public void GetOverrideTasksPathFromRegistry_ValueNotSet()
    {
        ToolsetReader reader = GetStandardRegistryReader();
        _ = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out string msbuildOverrideTasksPath,
            out _);

        msbuildOverrideTasksPath.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that a non-string <c>MsBuildOverrideTasksPath</c> registry value is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void GetOverrideTasksPathFromRegistry_NonStringData()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _currentVersionRegistryKey.SetValue("MsBuildOverrideTasksPath", new[] { "2938304894", "3948394.2.3.3.3" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();
            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: false,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that the configured default override tools version is read from the registry.
    /// </summary>
    [WindowsOnlyFact]
    public void GetDefaultOverrideToolsVersionFromRegistry_Basic()
    {
        _currentVersionRegistryKey.SetValue("DefaultOverrideToolsVersion", "Current");

        ToolsetReader reader = GetStandardRegistryReader();
        _ = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out _,
            out string defaultOverrideToolsVersion);

        defaultOverrideToolsVersion.ShouldBe("Current");
    }

    /// <summary>
    ///  Verifies that a missing <c>DefaultOverrideToolsVersion</c> registry value yields <see langword="null"/>.
    /// </summary>
    [WindowsOnlyFact]
    public void GetDefaultOverrideToolsVersionFromRegistry_ValueNotSet()
    {
        ToolsetReader reader = GetStandardRegistryReader();
        _ = reader.ReadToolsets(
            toolsets: [with(StringComparer.OrdinalIgnoreCase)],
            globalProperties: [],
            initialProperties: [],
            accumulateProperties: false,
            out _, out string defaultOverrideToolsVersion);

        defaultOverrideToolsVersion.ShouldBeNull();
    }

    /// <summary>
    ///  Verifies that a non-string <c>DefaultOverrideToolsVersion</c> registry value is rejected.
    /// </summary>
    [WindowsOnlyFact]
    public void GetDefaultOverrideToolsVersionFromRegistry_NonStringData()
        => Should.Throw<InvalidToolsetDefinitionException>(() =>
        {
            _currentVersionRegistryKey.SetValue("DefaultOverrideToolsVersion", new[] { "2938304894", "3948394.2.3.3.3" }, RegistryValueKind.MultiString);

            ToolsetReader reader = GetStandardRegistryReader();
            _ = reader.ReadToolsets(
                toolsets: [with(StringComparer.OrdinalIgnoreCase)],
                globalProperties: [],
                initialProperties: [],
                accumulateProperties: false,
                out _,
                out _);
        });

    /// <summary>
    ///  Verifies that toolsets without a tools path or binary path are ignored.
    /// </summary>
    [WindowsOnlyFact]
    public void ReadToolsets_NoBinPathOrToolsPath()
    {
        using RegistryKey key1 = _toolsVersionsRegistryKey.CreateSubKey("tv1");
        key1.SetValue("msbuildtoolspath", @"c:\xxx");
        key1.SetValue("name1", "value1");

        using RegistryKey key2 = _toolsVersionsRegistryKey.CreateSubKey("tv2");
        key2.SetValue("name2", "value2");

        using RegistryKey key3 = _toolsVersionsRegistryKey.CreateSubKey("tv3");
        key3.SetValue("msbuildtoolspath", @"c:\zzz");
        key3.SetValue("name3", "value3");

        ToolsetReader reader = GetStandardRegistryReader();
        Dictionary<string, Toolset> toolsets = [with(StringComparer.OrdinalIgnoreCase)];

        _ = reader.ReadToolsets(toolsets, globalProperties: [], initialProperties: [], accumulateProperties: false, out _, out _);

        toolsets.ContainsKey("tv1").ShouldBeTrue();

        // should not contain the second toolset because it does not define a tools/bin path
        toolsets.ContainsKey("tv2").ShouldBeFalse();

        toolsets.ContainsKey("tv3").ShouldBeTrue();
    }
}

#endif
