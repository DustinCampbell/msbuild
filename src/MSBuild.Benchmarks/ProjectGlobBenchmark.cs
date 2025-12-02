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
    private string _tempDirectory = null!;
    private string _projectWithSimpleGlobPath = null!;
    private string _projectWithRecursiveGlobPath = null!;
    private string _projectWithMultipleGlobsPath = null!;
    private string _projectWithExcludesPath = null!;
    private string _projectWithGlobAndRemovePath = null!;
    private string _projectWithGlobAndMetadataPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        // Create a realistic directory structure with files
        CreateFileStructure();

        // Create project files with different glob patterns
        _projectWithSimpleGlobPath = Path.Combine(_tempDirectory, "SimpleGlob.csproj");
        File.WriteAllText(_projectWithSimpleGlobPath, TestData.ProjectWithSimpleGlobXml);

        _projectWithRecursiveGlobPath = Path.Combine(_tempDirectory, "RecursiveGlob.csproj");
        File.WriteAllText(_projectWithRecursiveGlobPath, TestData.ProjectWithRecursiveGlobsXml);

        _projectWithMultipleGlobsPath = Path.Combine(_tempDirectory, "MultipleGlobs.csproj");
        File.WriteAllText(_projectWithMultipleGlobsPath, TestData.ProjectWithMultipleGlobsXml);

        _projectWithExcludesPath = Path.Combine(_tempDirectory, "WithExcludes.csproj");
        File.WriteAllText(_projectWithExcludesPath, TestData.ProjectWithExcludesXml);

        _projectWithGlobAndRemovePath = Path.Combine(_tempDirectory, "GlobAndRemove.csproj");
        File.WriteAllText(_projectWithGlobAndRemovePath, TestData.ProjectWithGlobAndRemoveXml);

        _projectWithGlobAndMetadataPath = Path.Combine(_tempDirectory, "GlobAndMetadata.csproj");
        File.WriteAllText(_projectWithGlobAndMetadataPath, TestData.ProjectWithGlobAndMetadataXml);
    }

    private void CreateFileStructure()
    {
        // Create a realistic project structure
        // Root level - 10 files
        for (int i = 0; i < 10; i++)
        {
            File.WriteAllText(Path.Combine(_tempDirectory, $"File{i}.cs"), "// Code file");
        }

        // Create subdirectories with files (3 levels deep)
        for (int i = 0; i < 5; i++)
        {
            var subDir = Path.Combine(_tempDirectory, $"Folder{i}");
            Directory.CreateDirectory(subDir);

            // 10 files per subdirectory
            for (int j = 0; j < 10; j++)
            {
                File.WriteAllText(Path.Combine(subDir, $"File{j}.cs"), "// Code file");
                File.WriteAllText(Path.Combine(subDir, $"Resource{j}.resx"), "<!-- Resource -->");
            }

            // Nested subdirectories
            for (int k = 0; k < 3; k++)
            {
                var nestedDir = Path.Combine(subDir, $"Nested{k}");
                Directory.CreateDirectory(nestedDir);

                for (int j = 0; j < 10; j++)
                {
                    File.WriteAllText(Path.Combine(nestedDir, $"File{j}.cs"), "// Code file");
                    File.WriteAllText(Path.Combine(nestedDir, $"Designer{j}.Designer.cs"), "// Designer file");
                }
            }
        }

        // Create bin and obj directories (should be excluded)
        var binDir = Path.Combine(_tempDirectory, "bin");
        var objDir = Path.Combine(_tempDirectory, "obj");
        Directory.CreateDirectory(binDir);
        Directory.CreateDirectory(objDir);

        for (int i = 0; i < 20; i++)
        {
            File.WriteAllText(Path.Combine(binDir, $"Output{i}.dll"), "Binary");
            File.WriteAllText(Path.Combine(objDir, $"Intermediate{i}.obj"), "Object");
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
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
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
