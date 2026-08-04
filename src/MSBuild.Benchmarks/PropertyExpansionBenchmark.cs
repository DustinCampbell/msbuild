// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks common property expansion shapes independently of item and metadata expansion.
/// </summary>
[MemoryDiagnoser]
public class PropertyExpansionBenchmark
{
    private const string Literal = "prefix-suffix";
    private const string SingleProperty = "$(Configuration)";
    private const string EmbeddedProperty = "prefix-$(Configuration)-suffix";
    private const string MultipleProperties = "$(Configuration)|$(Platform)|$(TargetFramework)|$(OutputPath)";
    private const string RepeatedProperty = "$(Configuration)|$(Configuration)|$(Configuration)|$(Configuration)";
    private const string UndefinedProperty = "prefix-$(Undefined)-suffix";
    private const string LongProperty = "$(LongValue)";

    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;

    [GlobalSetup]
    public void GlobalSetup()
        => _expander = PropertyExpansionBenchmarkData.CreateExpander();

    [Benchmark(Baseline = true)]
    public string NoExpansion()
        => Expand(Literal);

    [Benchmark]
    public string Single()
        => Expand(SingleProperty);

    [Benchmark]
    public string Embedded()
        => Expand(EmbeddedProperty);

    [Benchmark]
    public string MultipleDistinct()
        => Expand(MultipleProperties);

    [Benchmark]
    public string MultipleRepeated()
        => Expand(RepeatedProperty);

    [Benchmark]
    public string Undefined()
        => Expand(UndefinedProperty);

    [Benchmark]
    public string LongValue()
        => Expand(LongProperty);

    private string Expand(string expression)
        => _expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandProperties,
            ElementLocation.EmptyLocation);
}
