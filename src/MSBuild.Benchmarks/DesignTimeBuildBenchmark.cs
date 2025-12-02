// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for design-time build scenarios (IDE experience).
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class DesignTimeBuildBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private string _tempDirectory = null!;
    private string _normalProjectPath = null!;
    private string _designTimeProjectPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        // Create test files
        for (int i = 0; i < 50; i++)
        {
            File.WriteAllText(Path.Combine(_tempDirectory, $"File{i}.cs"), "// Code");
        }

        _normalProjectPath = Path.Combine(_tempDirectory, "Normal.csproj");
        File.WriteAllText(_normalProjectPath, TestData.NormalProjectXml);

        _designTimeProjectPath = Path.Combine(_tempDirectory, "DesignTime.csproj");
        File.WriteAllText(_designTimeProjectPath, TestData.DesignTimeProject);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _projectCollection = new ProjectCollection();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _projectCollection.UnloadAllProjects();
        _projectCollection.Dispose();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Benchmark(Description = "Evaluate normal build", Baseline = true, OperationsPerInvoke = 112)]
    public void EvaluateNormalBuild()
    {
        for (int i = 0; i < 112; i++)
        {
            var project = new Project(_normalProjectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate design-time build", OperationsPerInvoke = 112)]
    public void EvaluateDesignTimeBuild()
    {
        var globalProperties = new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = "true",
            ["BuildingProject"] = "false",
            ["SkipCompilerExecution"] = "true"
        };

        for (int i = 0; i < 112; i++)
        {
            var project = new Project(_designTimeProjectPath, globalProperties, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate with fast up-to-date check", OperationsPerInvoke = 128)]
    public void EvaluateWithFastUpToDateCheck()
    {
        var globalProperties = new Dictionary<string, string>
        {
            ["DesignTimeBuild"] = "true",
            ["BuildingProject"] = "false"
        };

        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_normalProjectPath, globalProperties, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }
}
