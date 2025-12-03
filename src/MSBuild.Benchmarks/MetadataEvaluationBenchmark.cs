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
    private TempDirectory _tempDirectory = null!;
    private string _simpleMetadataPath = null!;
    private string _wellKnownMetadataPath = null!;
    private string _customMetadataPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = new TempDirectory();

        // Create test files
        for (int i = 0; i < 50; i++)
        {
            _tempDirectory.WriteFile($"File{i}.cs", "// Code");
        }

        _simpleMetadataPath = _tempDirectory.WriteFile("SimpleMetadata.csproj", TestData.SimpleMetadataProjectXml);
        _wellKnownMetadataPath = _tempDirectory.WriteFile("WellKnownMetadata.csproj", TestData.WellKnownMetadataProjectXml);
        _customMetadataPath = _tempDirectory.WriteFile("CustomMetadata.csproj", TestData.CustomMetadataProjectXml);
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
