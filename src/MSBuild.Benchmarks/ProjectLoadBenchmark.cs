// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Xml;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for project XML loading, traversal, and save operations.
/// Compares the DOM-based parser (ProjectParser) against the XmlReader-based parser (ProjectXmlReader).
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
    /// Measures the time to parse a project XML string using the DOM-based parser (XmlDocument).
    /// Forces the legacy path via environment variable.
    /// </summary>
    [Benchmark(Baseline = true)]
    public ProjectRootElement LoadProjectFromString_DomParser()
    {
        Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", "0");
        ProjectRootElement.ResetUseProjectXmlReaderCache();
        try
        {
            using var reader = XmlReader.Create(new StringReader(_projectXml));
            return ProjectRootElement.Create(reader);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", null);
            ProjectRootElement.ResetUseProjectXmlReaderCache();
        }
    }

    /// <summary>
    /// Measures the time to parse a project XML string using the new XmlReader-based parser
    /// that populates ElementData instead of creating an XmlDocument DOM.
    /// </summary>
    [Benchmark]
    public ProjectRootElement LoadProjectFromString_XmlReaderParser()
    {
        Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", "1");
        ProjectRootElement.ResetUseProjectXmlReaderCache();
        try
        {
            using var reader = XmlReader.Create(new StringReader(_projectXml));
            return ProjectRootElement.Create(reader);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", null);
            ProjectRootElement.ResetUseProjectXmlReaderCache();
        }
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
    /// Measures a load-then-save round trip with the DOM parser.
    /// </summary>
    [Benchmark]
    public void LoadAndSaveRoundTrip_DomParser()
    {
        Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", "0");
        ProjectRootElement.ResetUseProjectXmlReaderCache();
        try
        {
            using var reader = XmlReader.Create(new StringReader(_projectXml));
            var pre = ProjectRootElement.Create(reader);
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.UTF8, 1024, leaveOpen: true);
            pre.Save(writer);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", null);
            ProjectRootElement.ResetUseProjectXmlReaderCache();
        }
    }

    /// <summary>
    /// Measures a load-then-save round trip with the XmlReader parser.
    /// This includes DOM materialization cost on first Save.
    /// </summary>
    [Benchmark]
    public void LoadAndSaveRoundTrip_XmlReaderParser()
    {
        Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", "1");
        ProjectRootElement.ResetUseProjectXmlReaderCache();
        try
        {
            using var reader = XmlReader.Create(new StringReader(_projectXml));
            var pre = ProjectRootElement.Create(reader);
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.UTF8, 1024, leaveOpen: true);
            pre.Save(writer);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILD_ENABLE_XMLREADER_PARSER", null);
            ProjectRootElement.ResetUseProjectXmlReaderCache();
        }
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
