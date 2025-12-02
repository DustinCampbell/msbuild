// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for MSBuild project import and SDK resolution.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class ProjectImportBenchmark
{
    private ProjectCollection _projectCollection = null!;
    private string _tempDirectory = null!;
    private string _projectWithImportsPath = null!;
    private string _projectWithMultipleImportsPath = null!;
    private string _commonPropsPath = null!;
    private string _commonTargetsPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDirectory);

        // Create common imported files
        _commonPropsPath = Path.Combine(_tempDirectory, "Common.props");
        File.WriteAllText(_commonPropsPath, TestData.CommonPropsXml);

        _commonTargetsPath = Path.Combine(_tempDirectory, "Common.targets");
        File.WriteAllText(_commonTargetsPath, TestData.CommonTargetsXml);

        // Create project files
        _projectWithImportsPath = Path.Combine(_tempDirectory, "ProjectWithImports.csproj");
        File.WriteAllText(_projectWithImportsPath, TestData.GetProjectWithImportsXml(_commonPropsPath, _commonTargetsPath));

        _projectWithMultipleImportsPath = Path.Combine(_tempDirectory, "ProjectWithMultipleImports.csproj");
        File.WriteAllText(_projectWithMultipleImportsPath, TestData.GetProjectWithMultipleImportsXml(_tempDirectory));

        // Create additional import files for multiple imports test
        for (int i = 0; i < 5; i++)
        {
            var importPath = Path.Combine(_tempDirectory, $"Import{i}.props");
            File.WriteAllText(importPath, TestData.GetGenericImportXml(i));
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

    [Benchmark(Description = "Evaluate project with single import", OperationsPerInvoke = 96)]
    public void EvaluateProjectWithSingleImport()
    {
        for (int i = 0; i < 96; i++)
        {
            _ = new Project(_projectWithImportsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with multiple imports", Baseline = true, OperationsPerInvoke = 64)]
    public void EvaluateProjectWithMultipleImports()
    {
        for (int i = 0; i < 64; i++)
        {
            _ = new Project(_projectWithMultipleImportsPath, globalProperties: null, toolsVersion: null, _projectCollection);
            _projectCollection.UnloadAllProjects();
        }
    }

    [Benchmark(Description = "Evaluate project with conditional imports", OperationsPerInvoke = 96)]
    public void EvaluateProjectWithConditionalImports()
    {
        var globalProperties = new Dictionary<string, string>
        {
            ["Configuration"] = "Debug"
        };

        for (int i = 0; i < 96; i++)
        {
            _ = new Project(_projectWithImportsPath, globalProperties, toolsVersion: null, _projectCollection);
            _projectCollection.UnloadAllProjects();
        }
    }
}
