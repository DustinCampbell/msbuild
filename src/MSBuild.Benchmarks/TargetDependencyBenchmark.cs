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
    private TempDirectory _tempDirectory = null!;
    private string _simpleTargetsPath = null!;
    private string _complexTargetsPath = null!;
    private string _deepDependenciesPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = new TempDirectory();

        _simpleTargetsPath = _tempDirectory.WriteFile("SimpleTargets.csproj", TestData.SimpleTargetsProjectXml);
        _complexTargetsPath = _tempDirectory.WriteFile("ComplexTargets.csproj", TestData.ComplexTargetsProjectXml);
        _deepDependenciesPath = _tempDirectory.WriteFile("DeepDependencies.csproj", TestData.DeepDependenciesProjectXml);
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
        _tempDirectory?.Dispose();
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
