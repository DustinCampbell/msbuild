// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for ProjectCollection caching and reuse patterns.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class CacheReuseBenchmark
{
    private TempDirectory _tempDirectory = null!;
    private string _projectPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = new TempDirectory();

        for (int i = 0; i < 50; i++)
        {
            _tempDirectory.WriteFile($"File{i}.cs", "// Code");
        }

        _projectPath = _tempDirectory.WriteFile("Project.csproj", TestData.ProjectContentXml);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _tempDirectory?.Dispose();
    }

    [Benchmark(Description = "New collection per evaluation", Baseline = true, OperationsPerInvoke = 64)]
    public void NewCollectionPerEvaluation()
    {
        for (int i = 0; i < 64; i++)
        {
            using var collection = new ProjectCollection();
            var project = new Project(_projectPath, globalProperties: null, toolsVersion: null, collection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
        }
    }

    [Benchmark(Description = "Reuse collection with unload", OperationsPerInvoke = 64)]
    public void ReuseCollectionWithUnload()
    {
        using var collection = new ProjectCollection();
        for (int i = 0; i < 64; i++)
        {
            var project = new Project(_projectPath, globalProperties: null, toolsVersion: null, collection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            collection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Reuse collection without unload", OperationsPerInvoke = 64)]
    public void ReuseCollectionWithoutUnload()
    {
        using var collection = new ProjectCollection();
        for (int i = 0; i < 64; i++)
        {
            var project = collection.LoadProject(_projectPath);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
        }
    }

    [Benchmark(Description = "Get cached project", OperationsPerInvoke = 128)]
    public void GetCachedProject()
    {
        using var collection = new ProjectCollection();
        var project = new Project(_projectPath, globalProperties: null, toolsVersion: null, collection);

        for (int i = 0; i < 128; i++)
        {
            var cachedProject = collection.GetLoadedProjects(_projectPath).FirstOrDefault();
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(cachedProject);
        }
    }
}
