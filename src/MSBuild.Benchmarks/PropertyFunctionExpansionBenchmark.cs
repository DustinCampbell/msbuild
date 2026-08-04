// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks property-function parsing, overload binding, invocation, and result conversion.
/// </summary>
[MemoryDiagnoser]
public class PropertyFunctionExpansionBenchmark
{
    private const string PropertyBaseline = "$(Text)";
    private const string StaticMethod = "$([System.Math]::Max(123, 456))";
    private const string StaticMethodWithPropertyArguments = "$([System.String]::Concat('$(Prefix)', '$(Suffix)'))";
    private const string InstanceMethod = "$(Text.ToUpperInvariant())";
    private const string ChainedInstanceMethods = "$(Text.Substring(7).ToUpperInvariant())";
    private const string NestedFunctions = "$([System.String]::Concat($([System.String]::Concat('prefix', '-')), 'suffix'))";
    private const string IntrinsicFunction = "$([MSBuild]::ValueOrDefault('$(Undefined)', 'fallback'))";
    private const string MultipleFunctions = "$([System.Math]::Max(123, 456))|$([System.Math]::Min(123, 456))";

    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;

    [GlobalSetup]
    public void GlobalSetup()
        => _expander = PropertyExpansionBenchmarkData.CreateExpander();

    [Benchmark(Baseline = true)]
    public string Property()
        => Expand(PropertyBaseline);

    [Benchmark]
    public string Static()
        => Expand(StaticMethod);

    [Benchmark]
    public string StaticWithPropertyArguments()
        => Expand(StaticMethodWithPropertyArguments);

    [Benchmark]
    public string Instance()
        => Expand(InstanceMethod);

    [Benchmark]
    public string ChainedInstance()
        => Expand(ChainedInstanceMethods);

    [Benchmark]
    public string Nested()
        => Expand(NestedFunctions);

    [Benchmark]
    public string Intrinsic()
        => Expand(IntrinsicFunction);

    [Benchmark]
    public string Multiple()
        => Expand(MultipleFunctions);

    private string Expand(string expression)
        => _expander.ExpandIntoStringLeaveEscaped(
            expression,
            ExpanderOptions.ExpandProperties,
            ElementLocation.EmptyLocation);
}
