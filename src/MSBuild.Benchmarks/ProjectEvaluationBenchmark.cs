// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for MSBuild project evaluation.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class ProjectEvaluationBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private string _tempDirectory = null!;
    private string _simpleProjectPath = null!;
    private string _complexProjectPath = null!;
    private string _largeProjectPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        // Write project files to disk (required for evaluation)
        _simpleProjectPath = Path.Combine(_tempDirectory, "Simple.csproj");
        File.WriteAllText(_simpleProjectPath, TestData.SimpleProjectXml);

        _complexProjectPath = Path.Combine(_tempDirectory, "Complex.csproj");
        File.WriteAllText(_complexProjectPath, TestData.ComplexProjectXml);

        _largeProjectPath = Path.Combine(_tempDirectory, "Large.csproj");
        File.WriteAllText(_largeProjectPath, TestData.LargeProjectXml);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Create a fresh ProjectCollection for each iteration to avoid conflicts
        _projectCollection = new ProjectCollection();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        // Unload all projects and dispose the collection
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

    [Benchmark(Description = "Evaluate simple project", OperationsPerInvoke = 160)]
    public void EvaluateSimpleProject()
    {
        for (int i = 0; i < 160; i++)
        {
            var project = new Project(_simpleProjectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate complex project", OperationsPerInvoke = 96)]
    public void EvaluateComplexProject()
    {
        for (int i = 0; i < 96; i++)
        {
            var project = new Project(_complexProjectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate large project", Baseline = true, OperationsPerInvoke = 64)]
    public void EvaluateLargeProject()
    {
        for (int i = 0; i < 64; i++)
        {
            var project = new Project(_largeProjectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate simple project with global properties", OperationsPerInvoke = 160)]
    public void EvaluateSimpleProjectWithGlobalProperties()
    {
        var globalProperties = new Dictionary<string, string>
        {
            ["Configuration"] = "Release",
            ["Platform"] = "x64"
        };

        for (int i = 0; i < 160; i++)
        {
            var project = new Project(_simpleProjectPath, globalProperties, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Re-evaluate simple project", OperationsPerInvoke = 128)]
    public void ReevaluateSimpleProject()
    {
        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_simpleProjectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            project.SetProperty("TestProperty", "NewValue");
            project.ReevaluateIfNecessary();
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Re-evaluate large project", OperationsPerInvoke = 48)]
    public void ReevaluateLargeProject()
    {
        for (int i = 0; i < 48; i++)
        {
            var project = new Project(_largeProjectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            project.SetProperty("Property0", "NewValue");
            project.ReevaluateIfNecessary();
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }
}
