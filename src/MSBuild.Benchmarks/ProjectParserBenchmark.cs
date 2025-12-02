// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Xml;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Build.Construction;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="ProjectParser"/>.
/// </summary>
[MemoryDiagnoser]
[MinIterationCount(15)]
[MaxIterationCount(20)]
public partial class ProjectParserBenchmark
{
    private XmlDocumentWithLocation _simpleDocument = null!;
    private XmlDocumentWithLocation _complexDocument = null!;
    private XmlDocumentWithLocation _largeDocument = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Pre-load XML documents for "parse only" benchmarks
        _simpleDocument = LoadXmlDocument(TestData.SimpleProjectXml);
        _complexDocument = LoadXmlDocument(TestData.ComplexProjectXml);
        _largeDocument = LoadXmlDocument(TestData.LargeProjectXml);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _simpleDocument = null!;
        _complexDocument = null!;
        _largeDocument = null!;
    }

    [Benchmark(Description = "Parse simple project (with XML loading)")]
    public void ParseSimpleProject()
    {
        var document = LoadXmlDocument(TestData.SimpleProjectXml);
        var projectRootElement = ProjectRootElement.Create();
        ProjectParser.Parse(document, projectRootElement);
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(projectRootElement);
    }

    [Benchmark(Description = "Parse complex project (with XML loading)")]
    public void ParseComplexProject()
    {
        var document = LoadXmlDocument(TestData.ComplexProjectXml);
        var projectRootElement = ProjectRootElement.Create();
        ProjectParser.Parse(document, projectRootElement);
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(projectRootElement);
    }

    [Benchmark(Description = "Parse large project (with XML loading)")]
    public void ParseLargeProject()
    {
        var document = LoadXmlDocument(TestData.LargeProjectXml);
        var projectRootElement = ProjectRootElement.Create();
        ProjectParser.Parse(document, projectRootElement);
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(projectRootElement);
    }

    [Benchmark(Description = "Parse simple project (parse only)")]
    public void ParseSimpleProjectOnly()
    {
        var projectRootElement = ProjectRootElement.Create();
        ProjectParser.Parse(_simpleDocument, projectRootElement);
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(projectRootElement);
    }

    [Benchmark(Description = "Parse complex project (parse only)")]
    public void ParseComplexProjectOnly()
    {
        var projectRootElement = ProjectRootElement.Create();
        ProjectParser.Parse(_complexDocument, projectRootElement);
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(projectRootElement);
    }

    [Benchmark(Description = "Parse large project (parse only)", Baseline = true)]
    public void ParseLargeProjectOnly()
    {
        var projectRootElement = ProjectRootElement.Create();
        ProjectParser.Parse(_largeDocument, projectRootElement);
        DeadCodeEliminationHelper.KeepAliveWithoutBoxing(projectRootElement);
    }

    private static XmlDocumentWithLocation LoadXmlDocument(string xmlContent)
    {
        using var stringReader = new StringReader(xmlContent);
        using var xmlReader = XmlReader.Create(stringReader);

        var document = new XmlDocumentWithLocation();
        document.Load(xmlReader);

        return document;
    }
}
