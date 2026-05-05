// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests;

public class AssemblyFoldersFromConfig_Tests(ITestOutputHelper output) : ResolveAssemblyReferenceTestFixture(output)
{
    private protected override TestRARServices ConfigureDefaultServices()
        => base.ConfigureDefaultServices().AddExistentFiles(
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder1", "assemblyfromconfig1.dll"),
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder2", "assemblyfromconfig2.dll"),
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder3_x86", "assemblyfromconfig3_x86.dll"),
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x86", "assemblyfromconfig_common.dll"),
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x64", "assemblyfromconfig_common.dll"),
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder5010x64", "v5assembly.dll"),
            Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder501000x86", "v5assembly.dll"));

    [Fact]
    public void AssemblyFoldersFromConfigTest()
    {
        string assemblyConfig = Path.GetTempFileName();
        File.WriteAllText(assemblyConfig, TestFile);

        string moniker = $"{{AssemblyFoldersFromConfig:{assemblyConfig},v4.5}}";

        try
        {
            ResolveAssemblyReference task = new()
            {
                BuildEngine = new MockEngine(_output),
                Assemblies = [new TaskItem("assemblyfromconfig2")],
                SearchPaths = [moniker],
            };

            Execute(task);

            ITaskItem resolvedFile = Assert.Single(task.ResolvedFiles);
            Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder2", "assemblyfromconfig2.dll"), resolvedFile.ItemSpec);
            resolvedFile.GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);
        }
        finally
        {
            FileUtilities.DeleteNoThrow(assemblyConfig);
        }
    }

    [Fact]
    public void AssemblyFoldersFromConfigPlatformSpecificAssemblyFirstTest()
    {
        string assemblyConfig = Path.GetTempFileName();
        File.WriteAllText(assemblyConfig, TestFile);

        string moniker = $"{{AssemblyFoldersFromConfig:{assemblyConfig},v4.5}}";

        try
        {
            ResolveAssemblyReference task = new()
            {
                BuildEngine = new MockEngine(_output),
                Assemblies = [new TaskItem("assemblyfromconfig_common.dll")],
                SearchPaths = [moniker],
                TargetProcessorArchitecture = "x86",
            };

            Execute(task);

            ITaskItem resolvedFile = Assert.Single(task.ResolvedFiles);
            Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x86", "assemblyfromconfig_common.dll"), resolvedFile.ItemSpec);
            resolvedFile.GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);
        }
        finally
        {
            FileUtilities.DeleteNoThrow(assemblyConfig);
        }
    }

    [Fact]
    public void AssemblyFoldersFromConfigNormalizeNetFrameworkVersion()
    {
        string assemblyConfig = Path.GetTempFileName();
        File.WriteAllText(assemblyConfig, TestFile);

        string moniker = $"{{AssemblyFoldersFromConfig:{assemblyConfig},v5.0}}";

        try
        {
            ResolveAssemblyReference task = new()
            {
                BuildEngine = new MockEngine(_output),
                Assemblies = [new TaskItem("v5assembly.dll")],
                SearchPaths = [moniker],
                TargetProcessorArchitecture = "x86",
            };

            Execute(task);

            ITaskItem resolvedFile = Assert.Single(task.ResolvedFiles);
            Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder501000x86", "v5assembly.dll"), resolvedFile.ItemSpec);
            resolvedFile.GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);

            // Try again changing only the processor architecture
            task = new ResolveAssemblyReference
            {
                BuildEngine = new MockEngine(_output),
                Assemblies = [new TaskItem("v5assembly.dll")],
                SearchPaths = [moniker],
                TargetProcessorArchitecture = "AMD64",
            };

            Execute(task);

            resolvedFile = Assert.Single(task.ResolvedFiles);
            Assert.Equal(Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder5010x64", "v5assembly.dll"), resolvedFile.ItemSpec);
            resolvedFile.GetMetadata("ResolvedFrom").ShouldBe(moniker, StringCompareShould.IgnoreCase);
        }
        finally
        {
            FileUtilities.DeleteNoThrow(assemblyConfig);
        }
    }

    [Fact]
    public void AssemblyFoldersFromConfigFileNotFoundTest()
    {
        string assemblyConfig = Path.GetTempFileName();
        File.Delete(assemblyConfig);

        string moniker = $"{{AssemblyFoldersFromConfig:{assemblyConfig},v4.5}}";

        try
        {
            ResolveAssemblyReference task = new()
            {
                BuildEngine = new MockEngine(_output),
                Assemblies = [new TaskItem("assemblyfromconfig_common.dll")],
                SearchPaths = [moniker],
                TargetProcessorArchitecture = "x86",
            };

            Assert.Throws<InternalErrorException>(() => Execute(task));
        }
        finally
        {
            FileUtilities.DeleteNoThrow(assemblyConfig);
        }
    }

    [Fact]
    public void AssemblyFoldersFromConfigFileMalformed()
    {
        string assemblyConfig = Path.GetTempFileName();
        File.WriteAllText(assemblyConfig, "<<<>><>!" + TestFile);

        string moniker = $"{{AssemblyFoldersFromConfig:{assemblyConfig},v4.5}}";

        try
        {
            MockEngine engine = new(_output);
            ResolveAssemblyReference task = new()
            {
                BuildEngine = engine,
                Assemblies = [new TaskItem("assemblyfromconfig2")],
                SearchPaths = [moniker],
            };

            bool success = Execute(task);

            Assert.False(success);
            Assert.Empty(task.ResolvedFiles);
            engine.AssertLogContains(") specified in Microsoft.Common.CurrentVersion.targets was invalid. The error was: ");
        }
        finally
        {
            FileUtilities.DeleteNoThrow(assemblyConfig);
        }
    }

    private readonly string TestFile = $"""
        <AssemblyFoldersConfig>
          <AssemblyFolders>
            <AssemblyFolder>
              <Name>Test Assemblies</Name>
              <FrameworkVersion>v5.0</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder1")}</Path>
            </AssemblyFolder>
            <AssemblyFolder>
              <Name>Test Assemblies2</Name>
              <FrameworkVersion>v4.5.25000</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder2")}</Path>
            </AssemblyFolder>
            <AssemblyFolder>
              <FrameworkVersion>v4.0</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder3")}</Path>
            </AssemblyFolder>
            <AssemblyFolder>
              <Name>Platform Specific</Name>
              <FrameworkVersion>v4.5.25000</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x64")}</Path>
              <Platform>x64</Platform>
            </AssemblyFolder>
            <AssemblyFolder>
              <FrameworkVersion>v4.5</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder_x86")}</Path>
              <Platform>x86</Platform>
            </AssemblyFolder>

            <AssemblyFolder>
              <FrameworkVersion>v5.0.1.0</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder5010x64")}</Path>
              <Platform>x64</Platform>
            </AssemblyFolder>
            <AssemblyFolder>
              <FrameworkVersion>v5.0.100.0</FrameworkVersion>
              <Path>{Path.Combine(s_rootPathPrefix, "assemblyfromconfig", "folder501000x86")}</Path>
              <Platform>x86</Platform>
            </AssemblyFolder>
          </AssemblyFolders>
        </AssemblyFoldersConfig>
        """;
}
