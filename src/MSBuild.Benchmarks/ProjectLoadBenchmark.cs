// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for project XML loading, traversal, and save operations.
/// Establishes a performance baseline for the construction model's XML DOM dependency.
/// These benchmarks will be used to verify that the DOM elimination refactoring
/// does not regress performance and ideally improves it.
/// </summary>
[MemoryDiagnoser]
public class ProjectLoadBenchmark
{
    private string _projectFilePath = null!;
    private string _projectXml = null!;
    private ProjectRootElement _loadedProject = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Use the MSBuild engine project itself as a realistic, non-trivial project file.
        _projectFilePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "Build", "Microsoft.Build.csproj"));

        if (!File.Exists(_projectFilePath))
        {
            // Fallback: generate a synthetic project with realistic complexity.
            _projectFilePath = Path.GetTempFileName();
            File.WriteAllText(_projectFilePath, GenerateSyntheticProject());
        }

        _projectXml = File.ReadAllText(_projectFilePath);
        _loadedProject = ProjectRootElement.Open(_projectFilePath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
    }

    /// <summary>
    /// Measures the time to parse a project XML string into a ProjectRootElement.
    /// </summary>
    [Benchmark]
    public ProjectRootElement LoadProjectFromString()
    {
        return ProjectRootElement.Create(new StringReader(_projectXml));
    }

    /// <summary>
    /// Measures the time to traverse all elements and read their locations and attributes.
    /// This exercises the accessor paths that currently delegate to XmlElementWithLocation.
    /// </summary>
    [Benchmark]
    public int TraverseConstructionModel()
    {
        int count = 0;
        foreach (ProjectElement element in _loadedProject.AllChildren)
        {
            _ = element.ElementName;
            _ = element.Location;
            _ = element.Condition;
            _ = element.ConditionLocation;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Measures the time to serialize a loaded project back to XML text.
    /// </summary>
    [Benchmark]
    public string SaveProjectToString()
    {
        return _loadedProject.RawXml;
    }

    /// <summary>
    /// Measures a load-then-save round trip using a MemoryStream.
    /// </summary>
    [Benchmark]
    public void LoadAndSaveRoundTrip()
    {
        var pre = ProjectRootElement.Create(new StringReader(_projectXml));
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        pre.Save(writer);
    }

    /// <summary>
    /// Measures programmatic creation and mutation of project elements.
    /// </summary>
    [Benchmark]
    public ProjectRootElement CreateAndMutateProject()
    {
        var root = ProjectRootElement.Create();
        root.DefaultTargets = "Build";

        var pg = root.AddPropertyGroup();
        pg.AddProperty("OutputType", "Library");
        pg.AddProperty("TargetFramework", "net10.0");
        pg.AddProperty("Nullable", "enable");

        var ig = root.AddItemGroup();
        for (int i = 0; i < 50; i++)
        {
            ig.AddItem("Compile", $"src/File{i}.cs");
        }

        var target = root.AddTarget("CustomBuild");
        target.Condition = "'$(Configuration)' == 'Release'";
        var task = target.AddTask("Csc");
        task.SetParameter("Sources", "@(Compile)");

        return root;
    }

    private static string GenerateSyntheticProject()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine("    <OutputType>Library</OutputType>");
        sb.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine("  <ItemGroup>");
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"    <Compile Include=\"src/File{i}.cs\" />");
        }

        sb.AppendLine("  </ItemGroup>");
        for (int t = 0; t < 10; t++)
        {
            sb.AppendLine($"  <Target Name=\"Target{t}\" Condition=\"'$(Config)' == 'Release'\">");
            sb.AppendLine($"    <Message Text=\"Running target {t}\" />");
            sb.AppendLine("  </Target>");
        }

        sb.AppendLine("</Project>");
        return sb.ToString();
    }
}
