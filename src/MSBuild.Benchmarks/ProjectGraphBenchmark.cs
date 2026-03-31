// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class ProjectGraphBenchmark
{
    /// <summary>
    /// Number of leaf projects in the graph (projects with no references).
    /// </summary>
    private const int LeafCount = 10;

    /// <summary>
    /// Number of mid-tier projects that each reference all leaf projects.
    /// </summary>
    private const int MidCount = 5;

    private string _tempDir = null!;

    /// <summary>
    /// A small graph: single root with a few references. Exercises the minimum path through
    /// graph construction, topological sort, and GetTargetLists.
    /// </summary>
    private string _smallRootPath = null!;

    /// <summary>
    /// A diamond/fan-out graph: one root -> <see cref="MidCount"/> mid-tier projects -> <see cref="LeafCount"/> shared leaves.
    /// The shared leaves create a fan-in that stresses edge deduplication, target propagation,
    /// and the ImmutableList manipulation in GetTargetLists / ExpandDefaultTargets.
    /// </summary>
    private string _diamondRootPath = null!;

    /// <summary>
    /// A linear chain of projects: root -> p1 -> p2 -> ... -> pN.
    /// Stresses the depth of target propagation and topological sort without fan-out.
    /// </summary>
    private string _chainRootPath = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _smallRootPath = CreateSmallGraph();
        _diamondRootPath = CreateDiamondGraph();
        _chainRootPath = CreateChainGraph();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    // Graph construction benchmarks
    // -----------------------------------------------------------------------

    [Benchmark]
    public ProjectGraph Construction_SmallGraph()
    {
        using var pc = new ProjectCollection();
        return new ProjectGraph(_smallRootPath, pc);
    }

    [Benchmark]
    public ProjectGraph Construction_DiamondGraph()
    {
        using var pc = new ProjectCollection();
        return new ProjectGraph(_diamondRootPath, pc);
    }

    [Benchmark]
    public ProjectGraph Construction_ChainGraph()
    {
        using var pc = new ProjectCollection();
        return new ProjectGraph(_chainRootPath, pc);
    }

    // -----------------------------------------------------------------------
    // TopologicalSort benchmarks (lazy, triggered by accessing the property)
    // -----------------------------------------------------------------------

    [Benchmark]
    public int TopologicalSort_DiamondGraph()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_diamondRootPath, pc);
        // Force the lazy topological sort and consume the result.
        return graph.ProjectNodesTopologicallySorted.Count;
    }

    [Benchmark]
    public int TopologicalSort_ChainGraph()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_chainRootPath, pc);
        return graph.ProjectNodesTopologicallySorted.Count;
    }

    // -----------------------------------------------------------------------
    // GetTargetLists benchmarks — exercises target propagation, ImmutableList
    // building, ExpandDefaultTargets, and deduplication.
    // -----------------------------------------------------------------------

    [Benchmark]
    public int GetTargetLists_SmallGraph_DefaultTargets()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_smallRootPath, pc);
        var targetLists = graph.GetTargetLists(null);
        return targetLists.Count;
    }

    [Benchmark]
    public int GetTargetLists_DiamondGraph_DefaultTargets()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_diamondRootPath, pc);
        var targetLists = graph.GetTargetLists(null);
        return targetLists.Count;
    }

    [Benchmark]
    public int GetTargetLists_DiamondGraph_ExplicitBuildTarget()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_diamondRootPath, pc);
        var targetLists = graph.GetTargetLists(["Build"]);
        return targetLists.Count;
    }

    [Benchmark]
    public int GetTargetLists_DiamondGraph_MultipleTargets()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_diamondRootPath, pc);
        var targetLists = graph.GetTargetLists(["Restore", "Build"]);
        return targetLists.Count;
    }

    [Benchmark]
    public int GetTargetLists_ChainGraph_DefaultTargets()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_chainRootPath, pc);
        var targetLists = graph.GetTargetLists(null);
        return targetLists.Count;
    }

    // -----------------------------------------------------------------------
    // Combined: construction + topological sort + GetTargetLists
    // This is the realistic end-to-end scenario.
    // -----------------------------------------------------------------------

    [Benchmark]
    public int EndToEnd_DiamondGraph()
    {
        using var pc = new ProjectCollection();
        var graph = new ProjectGraph(_diamondRootPath, pc);
        _ = graph.ProjectNodesTopologicallySorted;
        var targetLists = graph.GetTargetLists(["Build"]);
        return targetLists.Count;
    }

    // -----------------------------------------------------------------------
    // Helpers to create project files on disk
    // -----------------------------------------------------------------------

    private string CreateSmallGraph()
    {
        // Root -> {Leaf1, Leaf2, Leaf3}
        string subDir = Path.Combine(_tempDir, "small");
        Directory.CreateDirectory(subDir);

        for (int i = 1; i <= 3; i++)
        {
            WriteProjectFile(subDir, $"Leaf{i}.proj", references: [], defaultTargets: "Build");
        }

        WriteProjectFile(subDir, "Root.proj",
            references: ["Leaf1.proj", "Leaf2.proj", "Leaf3.proj"],
            defaultTargets: "Build");

        return Path.Combine(subDir, "Root.proj");
    }

    private string CreateDiamondGraph()
    {
        // Root -> Mid1..MidN -> Leaf1..LeafN (each mid references all leaves)
        string subDir = Path.Combine(_tempDir, "diamond");
        Directory.CreateDirectory(subDir);

        // Create leaf projects
        for (int i = 1; i <= LeafCount; i++)
        {
            WriteProjectFile(subDir, $"Leaf{i}.proj", references: [], defaultTargets: "Build");
        }

        // Create mid-tier projects, each referencing all leaves
        string[] leafRefs = Enumerable.Range(1, LeafCount).Select(i => $"Leaf{i}.proj").ToArray();
        string[] midRefs = new string[MidCount];
        for (int i = 1; i <= MidCount; i++)
        {
            string name = $"Mid{i}.proj";
            WriteProjectFile(subDir, name, references: leafRefs, defaultTargets: "Build");
            midRefs[i - 1] = name;
        }

        // Create root referencing all mid-tier projects
        WriteProjectFile(subDir, "Root.proj", references: midRefs, defaultTargets: "Build");

        return Path.Combine(subDir, "Root.proj");
    }

    private string CreateChainGraph()
    {
        // Chain: Root -> P1 -> P2 -> ... -> P19 (20 total depth)
        const int chainLength = 20;
        string subDir = Path.Combine(_tempDir, "chain");
        Directory.CreateDirectory(subDir);

        // Create the tail first (no references)
        WriteProjectFile(subDir, $"P{chainLength - 1}.proj", references: [], defaultTargets: "Build");

        // Create intermediate nodes
        for (int i = chainLength - 2; i >= 1; i--)
        {
            WriteProjectFile(subDir, $"P{i}.proj", references: [$"P{i + 1}.proj"], defaultTargets: "Build");
        }

        // Create root
        WriteProjectFile(subDir, "Root.proj", references: ["P1.proj"], defaultTargets: "Build");

        return Path.Combine(subDir, "Root.proj");
    }

    private static void WriteProjectFile(string directory, string fileName, string[] references, string defaultTargets)
    {
        string fullPath = Path.Combine(directory, fileName);

        string projectRefItems = references.Length > 0
            ? string.Join(Environment.NewLine,
                references.Select(r => $"    <ProjectReference Include=\"{r}\" />"))
            : string.Empty;

        string projectReferenceTargets = """
    <ProjectReferenceTargets Include="Build" Targets=".default" />
    <ProjectReferenceTargets Include="Restore" Targets="Restore" />
""";

        string content = $"""
<Project DefaultTargets="{defaultTargets}">
  <ItemGroup>
{projectReferenceTargets}
{projectRefItems}
  </ItemGroup>
  <Target Name="Build" />
  <Target Name="Restore" />
</Project>
""";

        File.WriteAllText(fullPath, content);
    }
}
