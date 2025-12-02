// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for MSBuild property and item function evaluation.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class PropertyFunctionBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private string _tempDirectory = null!;
    private string _simplePropertiesPath = null!;
    private string _propertyFunctionsPath = null!;
    private string _itemTransformsPath = null!;
    private string _complexFunctionsPath = null!;

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

        _simplePropertiesPath = Path.Combine(_tempDirectory, "SimpleProperties.csproj");
        File.WriteAllText(_simplePropertiesPath, TestData.SimplePropertiesProjectXml);

        _propertyFunctionsPath = Path.Combine(_tempDirectory, "PropertyFunctions.csproj");
        File.WriteAllText(_propertyFunctionsPath, TestData.PropertyFunctionsProjectXml);

        _itemTransformsPath = Path.Combine(_tempDirectory, "ItemTransforms.csproj");
        File.WriteAllText(_itemTransformsPath, TestData.ItemTransformsProjectXml);

        _complexFunctionsPath = Path.Combine(_tempDirectory, "ComplexFunctions.csproj");
        File.WriteAllText(_complexFunctionsPath, TestData.ComplexFunctionsProjectXml);
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

    [Benchmark(Description = "Evaluate simple properties", OperationsPerInvoke = 128)]
    public void EvaluateSimpleProperties()
    {
        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_simplePropertiesPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate property functions", Baseline = true, OperationsPerInvoke = 96)]
    public void EvaluatePropertyFunctions()
    {
        for (int i = 0; i < 96; i++)
        {
            var project = new Project(_propertyFunctionsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate item transforms", OperationsPerInvoke = 96)]
    public void EvaluateItemTransforms()
    {
        for (int i = 0; i < 96; i++)
        {
            var project = new Project(_itemTransformsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate complex functions", OperationsPerInvoke = 64)]
    public void EvaluateComplexFunctions()
    {
        for (int i = 0; i < 64; i++)
        {
            var project = new Project(_complexFunctionsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }
}
