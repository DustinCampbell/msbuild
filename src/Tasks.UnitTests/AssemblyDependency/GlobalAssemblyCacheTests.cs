// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;
using static Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.TestData;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

/// <summary>
/// Unit tests for the ResolveAssemblyReference GlobalAssemblyCache.
/// </summary>
public sealed class GlobalAssemblyCacheTests(ITestOutputHelper output) : ResolveAssemblyReferenceTestFixture(output)
{
    private const string System4 = "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
    private const string System2 = "System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
    private const string System1 = "System, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089";
    private const string SystemNotStrong = "System, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null";

    private const string System4Path = @"c:\clr4\System.dll";
    private const string System2Path = @"c:\clr2\System.dll";
    private const string System1Path = @"c:\clr2\System1.dll";

    private readonly GacTestServices _gacServices = new();
    private readonly GetPathFromFusionName _getPathFromFusionName = MockGetPathFromFusionName;
    private readonly GetGacEnumerator _gacEnumerator = MockAssemblyCacheEnumerator;

    /// <summary>
    /// Verify when the GAC enumerator returns
    ///
    /// System, Version=4.0.0.0  Runtime=4.0xxxx
    /// System, Version=2.0.0.0  Runtime=2.0xxxx
    /// System, Version=1.0.0.0  Runtime=2.0xxxx
    ///
    /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
    ///
    /// This test two aspects. First that we get the correct runtime, second that we get the highest version for that assembly in the runtime.
    /// </summary>
    [Fact]
    public void VerifySimpleNamev2057020()
    {
        // We want to pass a very generic name to get the correct gac entries.
        AssemblyNameExtension fusionName = new("System");

        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _gacServices, new Version("2.0.57027"), false, _getPathFromFusionName, _gacEnumerator, false);
        Assert.NotNull(path);
        Assert.Equal(System2Path, path);
    }

    /// <summary>
    /// Verify when the GAC enumerator returns
    ///
    /// System, Version=4.0.0.0  Runtime=4.0xxxx
    /// System, Version=2.0.0.0  Runtime=2.0xxxx
    /// System, Version=1.0.0.0  Runtime=2.0xxxx
    ///
    /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
    ///
    /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
    /// Essentially specific version for the gac resolver means do not filter by runtime.
    /// </summary>
    [Fact]
    public void VerifySimpleNamev2057020SpecificVersion()
    {
        // We want to pass a very generic name to get the correct gac entries.
        AssemblyNameExtension fusionName = new("System");

        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _gacServices, new Version("2.0.0"), false, _getPathFromFusionName, _gacEnumerator, true);
        Assert.NotNull(path);
        Assert.Equal(System4Path, path);
    }

    /// <summary>
    /// Verify when the GAC enumerator returns
    ///
    /// System, Version=2.0.0.0  Runtime=2.0xxxx
    /// System, Version=1.0.0.0  Runtime=2.0xxxx
    ///
    /// And we target 2.0 runtime that we get the Version 2.0.0.0 system.
    ///
    /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
    /// Essentially specific version for the gac resolver means do not filter by runtime.
    /// </summary>
    [Fact]
    public void VerifyFusionNamev2057020SpecificVersion()
    {
        // We want to pass a very generic name to get the correct gac entries.
        AssemblyNameExtension fusionName = new("System, Version=2.0.0.0");

        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _gacServices, new Version("2.0.0"), false, _getPathFromFusionName, _gacEnumerator, true);
        Assert.NotNull(path);
        Assert.Equal(System2Path, path);
    }

    /// <summary>
    /// Verify when the GAC enumerator returns
    ///
    /// System, Version=4.0.0.0  Runtime=4.0xxxx
    /// System, Version=2.0.0.0  Runtime=2.0xxxx
    /// System, Version=1.0.0.0  Runtime=2.0xxxx
    ///
    /// And we target 4.0 runtime that we get the Version 4.0.0.0 system.
    ///
    /// This test two aspects. First that we get the correct runtime, second that we get the highest version for that assembly in the runtime.
    /// </summary>
    [Fact]
    public void VerifySimpleNamev40()
    {
        // We want to pass a very generic name to get the correct gac entries.
        AssemblyNameExtension fusionName = new("System");

        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _gacServices, new Version("4.0.0"), false, _getPathFromFusionName, _gacEnumerator, false);
        Assert.NotNull(path);
        Assert.Equal(System4Path, path);
    }

    /// <summary>
    /// Verify when the GAC enumerator returns
    ///
    /// System, Version=4.0.0.0  Runtime=4.0xxxx
    /// System, Version=2.0.0.0  Runtime=2.0xxxx
    /// System, Version=1.0.0.0  Runtime=2.0xxxx
    ///
    /// And we target 4.0 runtime that we get the Version 4.0.0.0 system.
    ///
    /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
    /// Essentially specific version for the gac resolver means do not filter by runtime.
    /// </summary>
    [Fact]
    public void VerifySimpleNamev40SpecificVersion()
    {
        // We want to pass a very generic name to get the correct gac entries.
        AssemblyNameExtension fusionName = new("System");

        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _gacServices, new Version("4.0.0"), false, _getPathFromFusionName, _gacEnumerator, true);
        Assert.NotNull(path);
        Assert.Equal(System4Path, path);
    }

    /// <summary>
    /// Verify when the GAC enumerator returns
    ///
    /// System, Version=4.0.0.0  Runtime=4.0xxxx
    ///
    ///
    /// Verify that by setting the wants specific version to true that we will return the highest version when only the simple name is used.
    /// Essentially specific version for the gac resolver means do not filter by runtime.
    /// </summary>
    [Fact]
    public void VerifyFusionNamev40SpecificVersion()
    {
        // We want to pass a very generic name to get the correct gac entries.
        AssemblyNameExtension fusionName = new("System, Version=4.0.0.0");

        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, _gacServices, new Version("4.0.0.0"), false, _getPathFromFusionName, _gacEnumerator, true);
        Assert.NotNull(path);
        Assert.Equal(System4Path, path);
    }

    /// <summary>
    /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac.
    /// </summary>
    [Fact]
    public void VerifyEmptyPublicKeyspecificVersion()
        => Assert.Throws<FileLoadException>(() =>
        {
            AssemblyNameExtension fusionName = new("System, PublicKeyToken=");
            string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, DefaultServices, new Version("2.0.50727"), false, _getPathFromFusionName, _gacEnumerator, true);
        });

    /// <summary>
    /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac.
    /// </summary>
    [Fact]
    public void VerifyNullPublicKey()
    {
        AssemblyNameExtension fusionName = new("System, PublicKeyToken=null");
        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, DefaultServices, new Version("2.0.50727"), false, _getPathFromFusionName, _gacEnumerator, false);
        Assert.Null(path);
    }

    /// <summary>
    /// Verify when a assembly name is passed in which has the public key explicitly set to null that we return null as the assembly cannot be in the gac.
    /// </summary>
    [Fact]
    public void VerifyNullPublicKeyspecificVersion()
    {
        AssemblyNameExtension fusionName = new("System, PublicKeyToken=null");
        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.None, DefaultServices, new Version("2.0.50727"), false, _getPathFromFusionName, _gacEnumerator, true);
        Assert.Null(path);
    }

    /// <summary>
    /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
    /// this was causing the GAC (api's) to crash.
    /// </summary>
    [Fact]
    public void VerifyProcessorArchitectureDoesNotCrash()
    {
        AssemblyNameExtension fusionName = new("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, DefaultServices, new Version("2.0.50727"), false, _getPathFromFusionName, null /* use the real gac enumerator*/, false);
        Assert.Null(path);
    }

    /// <summary>
    /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
    /// this was causing the GAC (api's) to crash.
    /// </summary>
    [Fact]
    public void VerifyProcessorArchitectureDoesNotCrashSpecificVersion()
    {
        AssemblyNameExtension fusionName = new("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, DefaultServices, new Version("2.0.50727"), false, _getPathFromFusionName, null /* use the real gac enumerator*/, true);
        Assert.Null(path);
    }

    /// <summary>
    /// See bug 648678,  when a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
    /// this was causing the GAC (api's) to crash.
    /// </summary>
    [Fact]
    public void VerifyProcessorArchitectureDoesNotCrashFullFusionName()
    {
        AssemblyNameExtension fusionName = new("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, DefaultServices, new Version("2.0.50727"), true, _getPathFromFusionName, null /* use the real gac enumerator*/, false);
        Assert.Null(path);
    }

    /// <summary>
    /// When a processor architecture is on the end of a fusion name we were appending another processor architecture onto the end causing an invalid fusion name
    /// this was causing the GAC (api's) to crash.
    /// </summary>
    [Fact]
    public void VerifyProcessorArchitectureDoesNotCrashFullFusionNameSpecificVersion()
    {
        AssemblyNameExtension fusionName = new("System, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL");
        string path = GlobalAssemblyCache.GetLocation(fusionName, SystemProcessorArchitecture.MSIL, DefaultServices, new Version("2.0.50727"), true, _getPathFromFusionName, null /* use the real gac enumerator*/, true);
        Assert.Null(path);
    }

    // System.Runtime dependency calculation tests

    // No dependency
    [Fact]
    public void SystemRuntimeDepends_No_Build()
    {
        TaskItem taskItem = new("Regular");
        taskItem.SetMetadata("HintPath", @"C:\SystemRuntime\Regular.dll");

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };
        Assert.True(
            task.Execute(DefaultServices));

        Assert.Equal("false", task.DependsOnSystemRuntime, true); // "Expected no System.Runtime dependency found during build."
        Assert.Equal("false", task.DependsOnNETStandard, true);   // "Expected no netstandard dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(
            task.Execute(DefaultServices));

        Assert.Equal("false", task.DependsOnSystemRuntime, true); // "Expected no System.Runtime dependency found during intellibuild."
        Assert.Equal("false", task.DependsOnNETStandard, true); //                   "Expected no netstandard dependency found during intellibuild."
    }

    // Direct dependency
    [Fact]
    public void SystemRuntimeDepends_Yes()
    {
        TaskItem taskItem = new("System.Runtime");
        taskItem.SetMetadata("HintPath", @"C:\SystemRuntime\System.Runtime.dll");

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(
            task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(
            task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during intellibuild."
    }

    // Indirect dependency
    [Fact]
    public void SystemRuntimeDepends_Yes_Indirect()
    {
        TaskItem taskItem = new("Portable");
        taskItem.SetMetadata("HintPath", @"C:\SystemRuntime\Portable.dll");

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(
            task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(
            task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during intellibuild."
    }

    [Fact]
    public void SystemRuntimeDepends_Yes_Indirect_ExternallyResolved()
    {
        TaskItem taskItem = new("Portable");
        taskItem.SetMetadata("ExternallyResolved", "true");
        taskItem.SetMetadata("HintPath", PortableDllPath);

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during intellibuild."
    }

    [Fact]
    public void NETStandardDepends_Yes()
    {
        TaskItem taskItem = new("netstandard");
        taskItem.SetMetadata("HintPath", @"C:\NetStandard\netstandard.dll");

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnNETStandard, true); // "Expected System.Runtime dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnNETStandard, true); // "Expected System.Runtime dependency found during intellibuild."
    }

    [Fact]
    public void NETStandardDepends_Yes_Indirect()
    {
        TaskItem taskItem = new("netstandardlibrary");
        taskItem.SetMetadata("HintPath", NetstandardLibraryDllPath);

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnNETStandard, true); // "Expected netstandard dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnNETStandard, true); // "Expected netstandard dependency found during intellibuild."
    }

    [Fact]
    public void NETStandardDepends_Yes_Indirect_ExternallyResolved()
    {
        TaskItem taskItem = new("netstandardlibrary");
        taskItem.SetMetadata("ExternallyResolved", "true");
        taskItem.SetMetadata("HintPath", NetstandardLibraryDllPath);

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnNETStandard, true); // "Expected netstandard dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnNETStandard, true); // "Expected netstandard dependency found during intellibuild."
    }

    [Fact]
    public void DependsOn_NETStandard_and_SystemRuntime()
    {
        TaskItem taskItem1 = new("netstandardlibrary");
        taskItem1.SetMetadata("HintPath", NetstandardLibraryDllPath);

        TaskItem taskItem2 = new("Portable");
        taskItem2.SetMetadata("HintPath", PortableDllPath);

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem1, taskItem2],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during build."
        Assert.Equal("true", task.DependsOnNETStandard, true);   // "Expected netstandard dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during intellibuild."
        Assert.Equal("true", task.DependsOnNETStandard, true);   // "Expected netstandard dependency found during intellibuild."
    }

    [Fact]
    public void DependsOn_NETStandard_and_SystemRuntime_ExternallyResolved()
    {
        TaskItem taskItem1 = new("netstandardlibrary");
        taskItem1.SetMetadata("ExternallyResolved", "true");
        taskItem1.SetMetadata("HintPath", NetstandardLibraryDllPath);

        TaskItem taskItem2 = new("Portable");
        taskItem2.SetMetadata("HintPath", PortableDllPath);
        taskItem2.SetMetadata("ExternallyResolved", "true");

        ResolveAssemblyReference task = new()
        {
            BuildEngine = new MockEngine(_output),
            Assemblies = [taskItem1, taskItem2],
            SearchPaths = DefaultPaths,

            // build mode
            FindDependencies = true,
        };

        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during build."
        Assert.Equal("true", task.DependsOnNETStandard, true);   // "Expected netstandard dependency found during build."

        // intelli build mode
        task.FindDependencies = false;
        Assert.True(task.Execute(DefaultServices));

        Assert.Equal("true", task.DependsOnSystemRuntime, true); // "Expected System.Runtime dependency found during intellibuild."
        Assert.Equal("true", task.DependsOnNETStandard, true);   // "Expected netstandard dependency found during intellibuild."
    }

    #region Helper Classes and Methods

    /// <summary>
    /// Test services class that provides mock implementations for GAC tests.
    /// </summary>
    private sealed class GacTestServices : RARServices
    {
        /// <inheritdoc />
        public override bool FileExists(string path)
            => true;

        /// <inheritdoc />
        public override string GetAssemblyRuntimeVersion(string path)
        {
            if (path.Equals(System1Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v2.0.50727";
            }

            if (path.Equals(System4Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v4.0.0";
            }

            if (path.Equals(System2Path, StringComparison.OrdinalIgnoreCase))
            {
                return "v2.0.50727";
            }

            return string.Empty;
        }
    }

    private static string MockGetPathFromFusionName(string strongName)
    {
        if (strongName.Equals(System1, StringComparison.OrdinalIgnoreCase))
        {
            return System1Path;
        }

        if (strongName.Equals(System2, StringComparison.OrdinalIgnoreCase))
        {
            return System2Path;
        }

        if (strongName.Equals(SystemNotStrong, StringComparison.OrdinalIgnoreCase))
        {
            return System2Path;
        }

        if (strongName.Equals(System4, StringComparison.OrdinalIgnoreCase))
        {
            return System4Path;
        }

        return string.Empty;
    }

    private static IEnumerable<AssemblyNameExtension> MockAssemblyCacheEnumerator(string strongName)
    {
        List<string> listOfAssemblies = [];

        if (strongName.StartsWith("System, Version=2.0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            listOfAssemblies.Add(System2);
        }
        else if (strongName.StartsWith("System, Version=4.0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            listOfAssemblies.Add(System4);
        }
        else
        {
            listOfAssemblies.Add(System1);
            listOfAssemblies.Add(System2);
            listOfAssemblies.Add(System4);
        }

        return new MockEnumerator(listOfAssemblies);
    }

    internal sealed class MockEnumerator(List<string> assembliesToEnumerate) : IEnumerable<AssemblyNameExtension>
    {
        public IEnumerator<AssemblyNameExtension> GetEnumerator()
        {
            foreach (string assembly in assembliesToEnumerate)
            {
                yield return new AssemblyNameExtension(assembly);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #endregion
}
