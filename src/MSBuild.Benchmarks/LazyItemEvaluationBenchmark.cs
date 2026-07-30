// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Measures item evaluation through the <c>LazyItemEvaluator</c>: wildcard includes, semicolon-list
/// includes, metadata-bearing updates, and removes over a set of on-disk files. This exercises the
/// item-operation substring paths (item-spec splitting, glob expansion, metadata) that the broader
/// StringSegment plan targets and that the expression-level benchmarks do not reach.
/// </summary>
/// <remarks>
/// A fresh <see cref="ProjectCollection"/> is used per invocation so the project is re-parsed and
/// re-evaluated every time (no cross-iteration caching), and the reported cost reflects a full item
/// evaluation rather than parsing alone. Backing files are written once in <see cref="GlobalSetup"/>.
/// </remarks>
[MemoryDiagnoser]
public class LazyItemEvaluationBenchmark
{
    /// <summary>
    /// Number of source files on disk for the wildcard include to match.
    /// </summary>
    [Params(500)]
    public int FileCount { get; set; }

    private string _projectDirectory = null!;
    private string _projectPath = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _projectDirectory = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks", "LazyItemEval", Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(_projectDirectory, "src");
        Directory.CreateDirectory(sourceDirectory);

        for (int i = 0; i < FileCount; i++)
        {
            string subDirectory = Path.Combine(sourceDirectory, $"dir{i % 10}");
            Directory.CreateDirectory(subDirectory);
            File.WriteAllText(Path.Combine(subDirectory, $"File{i}.cs"), string.Empty);
        }

        // Semicolon-list include (exercises item-spec splitting).
        string noneList = string.Join(";", Enumerable.Range(0, 40).Select(i => $"item{i}.txt"));

        // A bare project whose item operations exercise the LazyItemEvaluator: a wildcard include
        // with metadata, a semicolon-list include, a metadata-bearing update, and a remove.
        string projectXml = $"""
            <Project>
              <ItemGroup>
                <Compile Include="src\**\*.cs"><Culture>en-US</Culture></Compile>
              </ItemGroup>
              <ItemGroup>
                <None Include="{noneList}" />
              </ItemGroup>
              <ItemGroup>
                <Compile Update="src\dir1\**\*.cs"><Culture>fr-FR</Culture><Generator>ResX</Generator></Compile>
              </ItemGroup>
              <ItemGroup>
                <Compile Remove="src\dir9\**\*.cs" />
              </ItemGroup>
            </Project>
            """;

        _projectPath = Path.Combine(_projectDirectory, "items.proj");
        File.WriteAllText(_projectPath, projectXml);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (_projectDirectory is not null && Directory.Exists(_projectDirectory))
        {
            Directory.Delete(_projectDirectory, recursive: true);
        }
    }

    [Benchmark]
    public int EvaluateItems()
    {
        using ProjectCollection collection = new();
        ProjectInstance project = ProjectInstance.FromFile(_projectPath, new ProjectOptions
        {
            ProjectCollection = collection,
        });

        return project.GetItems("Compile").Count + project.GetItems("None").Count;
    }
}
