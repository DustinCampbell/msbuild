// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks expansion paths that materialize items rather than a single string.
/// </summary>
[BenchmarkCategory("Expansion", "ItemExpansion")]
[MemoryDiagnoser]
public class ItemListExpansionBenchmark
{
    private const string Literal = @"src\Program.cs";
    private const string PropertyAndItemExpression = @"$(OutputPath);@(Compile)";
    private const string PropertyAndMetadataExpression = @"$(OutputPath)\%(Culture)";
    private const string AllExpression = @"$(OutputPath)\%(Culture);@(Compile)";

    private ExpanderBenchmarkFixture _fixture = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        using ExpanderBuilder builder = new();
        builder.AddProperty("OutputPath", @"bin\Release\net11.0");
        builder.AddMetadata("Culture", "en-US");

        for (int i = 0; i < 10; i++)
        {
            builder.AddItem("Compile", $@"src\File{i}.cs");
        }

        _fixture = builder.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => _fixture.Dispose();

    [Benchmark(Baseline = true)]
    public int NoExpansionAll()
        => Expand(Literal, ExpanderOptions.ExpandAll);

    [Benchmark]
    public int NoExpansionPropertyAndItem()
        => Expand(Literal, ExpanderOptions.ExpandPropertiesAndItems);

    [Benchmark]
    public int NoExpansionPropertyAndMetadata()
        => Expand(Literal, ExpanderOptions.ExpandPropertiesAndMetadata);

    [Benchmark]
    public int NoExpansionItemAndMetadata()
        => Expand(Literal, ExpanderOptions.ExpandItems | ExpanderOptions.ExpandMetadata);

    [Benchmark]
    public int PropertyAndItem()
        => Expand(PropertyAndItemExpression, ExpanderOptions.ExpandPropertiesAndItems);

    [Benchmark]
    public int PropertyAndMetadata()
        => Expand(PropertyAndMetadataExpression, ExpanderOptions.ExpandPropertiesAndMetadata);

    [Benchmark]
    public int All()
        => Expand(AllExpression, ExpanderOptions.ExpandAll);

    private int Expand(string expression, ExpanderOptions options)
        => _fixture.Expander.ExpandIntoTaskItemsLeaveEscaped(
            expression,
            options,
            ElementLocation.EmptyLocation).Count;
}
