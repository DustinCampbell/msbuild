// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Context;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks targeting the lazy item evaluation pipeline in <see cref="Microsoft.Build.Evaluation" />.
/// Each benchmark constructs a project XML that exercises a specific LazyItemEvaluator code path,
/// then evaluates it via the public <see cref="Project"/> API.
/// </summary>
[MemoryDiagnoser]
public class LazyItemEvaluatorBenchmarks
{
    /// <summary>
    /// Number of items generated per item group. Kept moderate so benchmarks finish quickly.
    /// </summary>
    [Params(100, 500)]
    public int ItemCount { get; set; }

    private string _includeOnlyXml = null!;
    private string _includeWithExcludeXml = null!;
    private string _includeRemoveXml = null!;
    private string _includeUpdateXml = null!;
    private string _itemReferenceXml = null!;
    private string _batchedUpdateXml = null!;
    private string _manyIncludeElementsXml = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _includeOnlyXml = BuildIncludeOnlyProject(ItemCount);
        _includeWithExcludeXml = BuildIncludeWithExcludeProject(ItemCount);
        _includeRemoveXml = BuildIncludeRemoveProject(ItemCount);
        _includeUpdateXml = BuildIncludeUpdateProject(ItemCount);
        _itemReferenceXml = BuildItemReferenceProject(ItemCount);
        _batchedUpdateXml = BuildBatchedUpdateProject(ItemCount);
        _manyIncludeElementsXml = BuildManyIncludeElementsProject(ItemCount);
    }

    /// <summary>
    /// Exercises <c>IncludeOperation</c> with simple semicolon-separated value items (no globs, no excludes).
    /// </summary>
    [Benchmark(Description = "Include only")]
    public Project IncludeOnly() => EvaluateProject(_includeOnlyXml);

    /// <summary>
    /// Exercises <c>IncludeOperation</c> with excludes, hitting the exclude-matching logic.
    /// </summary>
    [Benchmark(Description = "Include + Exclude")]
    public Project IncludeWithExclude() => EvaluateProject(_includeWithExcludeXml);

    /// <summary>
    /// Exercises <c>RemoveOperation</c>, including the dictionary-based removal optimization
    /// when the item count exceeds <c>DictionaryBasedItemRemoveThreshold</c>.
    /// </summary>
    [Benchmark(Description = "Include + Remove")]
    public Project IncludeRemove() => EvaluateProject(_includeRemoveXml);

    /// <summary>
    /// Exercises <c>UpdateOperation</c> applying metadata updates to all items via self-referencing Update="@(I)".
    /// </summary>
    [Benchmark(Description = "Include + Update @(self)")]
    public Project IncludeUpdate() => EvaluateProject(_includeUpdateXml);

    /// <summary>
    /// Exercises <c>MemoizedOperation</c> caching by having one item type reference another via Include="@(Source)".
    /// </summary>
    [Benchmark(Description = "Item reference @(Other)")]
    public Project ItemReference() => EvaluateProject(_itemReferenceXml);

    /// <summary>
    /// Exercises the batched (non-wildcard) Update path in <c>ComputeItems</c> where multiple
    /// Update elements each target a single literal item spec and are batched into a dictionary.
    /// </summary>
    [Benchmark(Description = "Batched literal Updates")]
    public Project BatchedUpdate() => EvaluateProject(_batchedUpdateXml);

    /// <summary>
    /// Exercises the <c>LazyItemList</c> chain traversal in <c>ComputeItems</c> by creating many
    /// individual item elements, each producing a separate <c>IncludeOperation</c> node.
    /// This is representative of large hand-authored project files.
    /// </summary>
    [Benchmark(Description = "Many individual Include elements")]
    public Project ManyIncludeElements() => EvaluateProject(_manyIncludeElementsXml);

    private static Project EvaluateProject(string xml)
    {
        var collection = new ProjectCollection();
        var context = EvaluationContext.Create(EvaluationContext.SharingPolicy.Isolated);

        using XmlReader reader = XmlReader.Create(new StringReader(xml));
        ProjectRootElement root = ProjectRootElement.Create(reader, collection);

        var options = new ProjectOptions
        {
            ProjectCollection = collection,
            EvaluationContext = context,
        };

        return Project.FromProjectRootElement(root, options);
    }

    #region Project XML builders

    private static string BuildIncludeOnlyProject(int count)
    {
        // Single Include attribute with many semicolon-separated values.
        string items = string.Join(";", Enumerable.Range(0, count).Select(i => $"item{i}.cs"));
        return WrapInProject($"""
            <ItemGroup>
              <Compile Include="{items}" />
            </ItemGroup>
            """);
    }

    private static string BuildIncludeWithExcludeProject(int count)
    {
        // Include all items, then exclude roughly half of them.
        string includes = string.Join(";", Enumerable.Range(0, count).Select(i => $"item{i}.cs"));
        string excludes = string.Join(";", Enumerable.Range(0, count).Where(i => i % 2 == 0).Select(i => $"item{i}.cs"));
        return WrapInProject($"""
            <ItemGroup>
              <Compile Include="{includes}" Exclude="{excludes}" />
            </ItemGroup>
            """);
    }

    private static string BuildIncludeRemoveProject(int count)
    {
        // Include all items, then remove roughly half via a second element.
        string includes = string.Join(";", Enumerable.Range(0, count).Select(i => $"item{i}.cs"));
        string removes = string.Join(";", Enumerable.Range(0, count).Where(i => i % 2 == 0).Select(i => $"item{i}.cs"));
        return WrapInProject($"""
            <ItemGroup>
              <Compile Include="{includes}" />
              <Compile Remove="{removes}" />
            </ItemGroup>
            """);
    }

    private static string BuildIncludeUpdateProject(int count)
    {
        // Include items, then update all of them with metadata via self-reference.
        string items = string.Join(";", Enumerable.Range(0, count).Select(i => $"item{i}.cs"));
        return WrapInProject($"""
            <ItemGroup>
              <Compile Include="{items}" />
              <Compile Update="@(Compile)">
                <CustomMeta>value</CustomMeta>
              </Compile>
            </ItemGroup>
            """);
    }

    private static string BuildItemReferenceProject(int count)
    {
        // One item type includes values, a second item type references the first via @(Source).
        // This exercises the MemoizedOperation caching path.
        string items = string.Join(";", Enumerable.Range(0, count).Select(i => $"item{i}.cs"));
        return WrapInProject($"""
            <ItemGroup>
              <Source Include="{items}" />
              <Compile Include="@(Source)" />
            </ItemGroup>
            """);
    }

    private static string BuildBatchedUpdateProject(int count)
    {
        // Many individual Update elements, each targeting a single literal item.
        // This exercises the batched dictionary-based update path in ComputeItems.
        string items = string.Join(";", Enumerable.Range(0, count).Select(i => $"item{i}.cs"));
        var updates = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, count).Select(i =>
                $"""    <Compile Update="item{i}.cs"><Index>{i}</Index></Compile>"""));

        return WrapInProject($"""
            <ItemGroup>
              <Compile Include="{items}" />
            </ItemGroup>
            <ItemGroup>
            {updates}
            </ItemGroup>
            """);
    }

    private static string BuildManyIncludeElementsProject(int count)
    {
        // Many individual item elements, each with a single Include attribute.
        string items = string.Join(Environment.NewLine, Enumerable.Range(0, count).Select(i => $"""<Compile Include="item{i}.cs" />"""));
        return WrapInProject($"""
            <ItemGroup>
            {items}
            </ItemGroup>
            """);
    }

    private static string WrapInProject(string body)
        => $"""
            <Project>
              {body}
            </Project>
            """;

    #endregion
}
