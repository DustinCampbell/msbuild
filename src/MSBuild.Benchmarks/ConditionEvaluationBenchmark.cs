// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for MSBuild condition evaluation performance.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class ConditionEvaluationBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private TempDirectory _tempDirectory = null!;
    private string _simpleConditionsPath = null!;
    private string _complexConditionsPath = null!;
    private string _existsConditionsPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = new TempDirectory();

        // Create test files for Exists() checks
        for (int i = 0; i < 20; i++)
        {
            _tempDirectory.WriteFile($"File{i}.cs", "// Code");
        }

        _simpleConditionsPath = _tempDirectory.WriteFile("SimpleConditions.csproj", TestData.SimpleConditionsProjectXml);
        _complexConditionsPath = _tempDirectory.WriteFile("ComplexConditions.csproj", TestData.ComplexConditionsProjectXml);
        _existsConditionsPath = _tempDirectory.WriteFile("ExistsConditions.csproj", TestData.ExistsConditionsProjectXml);
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

    [Benchmark(Description = "Evaluate simple conditions", OperationsPerInvoke = 160)]
    public void EvaluateSimpleConditions()
    {
        for (int i = 0; i < 160; i++)
        {
            var project = new Project(_simpleConditionsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate complex conditions", Baseline = true, OperationsPerInvoke = 144)]
    public void EvaluateComplexConditions()
    {
        for (int i = 0; i < 144; i++)
        {
            var project = new Project(_complexConditionsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate Exists conditions", OperationsPerInvoke = 128)]
    public void EvaluateExistsConditions()
    {
        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_existsConditionsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }
}
