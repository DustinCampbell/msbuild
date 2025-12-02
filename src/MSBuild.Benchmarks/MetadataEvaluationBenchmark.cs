// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for item metadata evaluation and well-known metadata.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class MetadataEvaluationBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private string _tempDirectory = null!;
    private string _simpleMetadataPath = null!;
    private string _wellKnownMetadataPath = null!;
    private string _customMetadataPath = null!;

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

        _simpleMetadataPath = Path.Combine(_tempDirectory, "SimpleMetadata.csproj");
        File.WriteAllText(_simpleMetadataPath, TestData.SimpleMetadataProjectXml);

        _wellKnownMetadataPath = Path.Combine(_tempDirectory, "WellKnownMetadata.csproj");
        File.WriteAllText(_wellKnownMetadataPath, TestData.WellKnownMetadataProjectXml);

        _customMetadataPath = Path.Combine(_tempDirectory, "CustomMetadata.csproj");
        File.WriteAllText(_customMetadataPath, TestData.CustomMetadataProjectXml);
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

    [Benchmark(Description = "Evaluate items without metadata", OperationsPerInvoke = 128)]
    public void EvaluateSimpleMetadata()
    {
        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_simpleMetadataPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate well-known metadata", Baseline = true, OperationsPerInvoke = 96)]
    public void EvaluateWellKnownMetadata()
    {
        for (int i = 0; i < 96; i++)
        {
            var project = new Project(_wellKnownMetadataPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate custom metadata", OperationsPerInvoke = 96)]
    public void EvaluateCustomMetadata()
    {
        for (int i = 0; i < 96; i++)
        {
            var project = new Project(_customMetadataPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }
}
