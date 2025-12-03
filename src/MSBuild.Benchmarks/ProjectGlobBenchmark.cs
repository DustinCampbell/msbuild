// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for MSBuild glob evaluation performance.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class ProjectGlobBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private TempDirectory _tempDirectory = null!;
    private string _projectWithSimpleGlobPath = null!;
    private string _projectWithRecursiveGlobPath = null!;
    private string _projectWithMultipleGlobsPath = null!;
    private string _projectWithExcludesPath = null!;
    private string _projectWithGlobAndRemovePath = null!;
    private string _projectWithGlobAndMetadataPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = new TempDirectory();

        // Create a realistic directory structure with files
        CreateFileStructure();

        // Create project files with different glob patterns
        _projectWithSimpleGlobPath = _tempDirectory.WriteFile("SimpleGlob.csproj", TestData.ProjectWithSimpleGlobXml);
        _projectWithRecursiveGlobPath = _tempDirectory.WriteFile("RecursiveGlob.csproj", TestData.ProjectWithRecursiveGlobsXml);
        _projectWithMultipleGlobsPath = _tempDirectory.WriteFile("MultipleGlobs.csproj", TestData.ProjectWithMultipleGlobsXml);
        _projectWithExcludesPath = _tempDirectory.WriteFile("WithExcludes.csproj", TestData.ProjectWithExcludesXml);
        _projectWithGlobAndRemovePath = _tempDirectory.WriteFile("GlobAndRemove.csproj", TestData.ProjectWithGlobAndRemoveXml);
        _projectWithGlobAndMetadataPath = _tempDirectory.WriteFile("GlobAndMetadata.csproj", TestData.ProjectWithGlobAndMetadataXml);
    }

    private void CreateFileStructure()
    {
        // Create a realistic project structure
        // Root level - 10 files
        for (int i = 0; i < 10; i++)
        {
            _tempDirectory.WriteFile($"File{i}.cs", "// Code file");
        }

        // Create subdirectories with files (3 levels deep)
        for (int i = 0; i < 5; i++)
        {
            var folderName = $"Folder{i}";

            // 10 files per subdirectory
            for (int j = 0; j < 10; j++)
            {
                _tempDirectory.WriteFile(folderName, $"File{j}.cs", "// Code file");
                _tempDirectory.WriteFile(folderName, $"Resource{j}.resx", "<!-- Resource -->");
            }

            // Nested subdirectories
            for (int k = 0; k < 3; k++)
            {
                var nestedName = Path.Combine(folderName, $"Nested{k}");

                for (int j = 0; j < 10; j++)
                {
                    _tempDirectory.WriteFile(nestedName, $"File{j}.cs", "// Code file");
                    _tempDirectory.WriteFile(nestedName, $"Designer{j}.Designer.cs", "// Designer file");
                }
            }
        }

        // Create bin and obj directories (should be excluded)
        for (int i = 0; i < 20; i++)
        {
            _tempDirectory.WriteFile("bin", $"Output{i}.dll", "Binary");
            _tempDirectory.WriteFile("obj", $"Intermediate{i}.obj", "Object");
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

    [Benchmark(Description = "Evaluate project with simple glob", OperationsPerInvoke = 128)]
    public void EvaluateProjectWithSimpleGlob()
    {
        for (int i = 0; i < 128; i++)
        {
            var project = new Project(_projectWithSimpleGlobPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with recursive glob", Baseline = true, OperationsPerInvoke = 48)]
    public void EvaluateProjectWithRecursiveGlob()
    {
        for (int i = 0; i < 48; i++)
        {
            var project = new Project(_projectWithRecursiveGlobPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with multiple globs", OperationsPerInvoke = 48)]
    public void EvaluateProjectWithMultipleGlobs()
    {
        for (int i = 0; i < 48; i++)
        {
            var project = new Project(_projectWithMultipleGlobsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with excludes", OperationsPerInvoke = 48)]
    public void EvaluateProjectWithExcludes()
    {
        for (int i = 0; i < 48; i++)
        {
            var project = new Project(_projectWithExcludesPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with glob and remove", OperationsPerInvoke = 48)]
    public void EvaluateProjectWithGlobAndRemove()
    {
        for (int i = 0; i < 48; i++)
        {
            var project = new Project(_projectWithGlobAndRemovePath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with glob and metadata", OperationsPerInvoke = 48)]
    public void EvaluateProjectWithGlobAndMetadata()
    {
        for (int i = 0; i < 48; i++)
        {
            var project = new Project(_projectWithGlobAndMetadataPath, globalProperties: null, toolsVersion: null, _projectCollection);
            DeadCodeEliminationHelper.KeepAliveWithoutBoxing(project);
            _projectCollection.UnloadAllProjects();
        }
    }
}
