// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for target dependency resolution and graph construction.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class TargetDependencyBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private string _tempDirectory = null!;
    private string _simpleTargetsPath = null!;
    private string _complexTargetsPath = null!;
    private string _deepDependenciesPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        _simpleTargetsPath = Path.Combine(_tempDirectory, "SimpleTargets.csproj");
        File.WriteAllText(_simpleTargetsPath, TestData.SimpleTargetsProjectXml);

        _complexTargetsPath = Path.Combine(_tempDirectory, "ComplexTargets.csproj");
        File.WriteAllText(_complexTargetsPath, TestData.ComplexTargetsProjectXml);

        _deepDependenciesPath = Path.Combine(_tempDirectory, "DeepDependencies.csproj");
        File.WriteAllText(_deepDependenciesPath, TestData.DeepDependenciesProjectXml);
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

    [Benchmark(Description = "Evaluate project with simple targets", OperationsPerInvoke = 160)]
    public void EvaluateSimpleTargets()
    {
        for (int i = 0; i < 160; i++)
        {
            var project = new Project(_simpleTargetsPath, globalProperties: null, toolsVersion: null, _projectCollection);

            // Access Targets to force target graph construction
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project.Targets.Count);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with complex targets", Baseline = true, OperationsPerInvoke = 160)]
    public void EvaluateComplexTargets()
    {
        for (int i = 0; i < 160; i++)
        {
            var project = new Project(_complexTargetsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project.Targets.Count);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with deep dependencies", OperationsPerInvoke = 160)]
    public void EvaluateDeepDependencies()
    {
        for (int i = 0; i < 160; i++)
        {
            var project = new Project(_deepDependenciesPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project.Targets.Count);
            _projectCollection.UnloadAllProjects();
        }
    }
}
