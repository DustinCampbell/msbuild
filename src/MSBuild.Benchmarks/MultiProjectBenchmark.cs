// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for multi-project evaluation scenarios.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class MultiProjectBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private TempDirectory _tempDirectory = null!;
    private string[] _projectPaths = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = new TempDirectory();

        // Create multiple project files simulating a solution
        _projectPaths = new string[10];

        for (int i = 0; i < 10; i++)
        {
            var projectName = $"Project{i}";

            _projectPaths[i] = _tempDirectory.WriteFile($"{projectName}.csproj", TestData.GetProjectContent(i));

            // Create some source files for each project
            for (int j = 0; j < 5; j++)
            {
                _tempDirectory.WriteFile(projectName, $"File{j}.cs", "// Code");
            }
        }
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

    [Benchmark(Description = "Load single project", OperationsPerInvoke = 128)]
    public void LoadSingleProject()
    {
        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_projectPaths[0], globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Load multiple projects sequentially", Baseline = true, OperationsPerInvoke = 32)]
    public void LoadMultipleProjectsSequentially()
    {
        for (int i = 0; i < 32; i++)
        {
            foreach (var projectPath in _projectPaths)
            {
                var project = new Project(projectPath, globalProperties: null, toolsVersion: null, _projectCollection);
                DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            }

            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Load all projects once then access", OperationsPerInvoke = 65536)]
    public void LoadProjectsOnceAndReuse()
    {
        // Load all projects once
        foreach (var projectPath in _projectPaths)
        {
            var project = new Project(projectPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
        }

        // Access each project 65536 times (simulating intensive IDE queries)
        // This represents realistic IDE usage where projects are accessed repeatedly
        // for IntelliSense, hover, navigation, etc.
        for (int i = 0; i < 65536; i++)
        {
            foreach (var projectPath in _projectPaths)
            {
                var project = _projectCollection.GetLoadedProjects(projectPath).FirstOrDefault();
                DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            }
        }

        _projectCollection.UnloadAllProjects();
    }

    [Benchmark(Description = "Reload same project multiple times", OperationsPerInvoke = 64)]
    public void ReloadSameProject()
    {
        for (int i = 0; i < 64; i++)
        {
            for (int j = 0; j < 10; j++)
            {
                var project = new Project(_projectPaths[0], globalProperties: null, toolsVersion: null, _projectCollection);
                DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
                _projectCollection.UnloadAllProjects();
            }
        }
    }
}
