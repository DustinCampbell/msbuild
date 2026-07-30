// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for condition parsing and evaluation (the conditional Scanner/Parser and the
/// expression tree). <see cref="Parse"/> builds a fresh tree per invocation (no expression-tree
/// cache), so it isolates the scan/parse allocations that the StringSegment work targets;
/// <see cref="Evaluate"/> measures end-to-end evaluation including operand expansion via the
/// <see cref="Expander{P, I}"/> (with the production tree cache warm).
/// </summary>
[MemoryDiagnoser]
public class ConditionEvaluationBenchmark
{
    // Scenarios: label -> condition string.
    private static readonly (string Label, string Condition)[] s_scenarios =
    [
        ("Simple", "'$(Configuration)' == 'Release'"),
        ("AndOr", "'$(Configuration)' == 'Release' And ('$(Platform)' == 'AnyCPU' Or '$(Platform)' == 'x64')"),
        ("Function", "Exists('$(OutputPath)')"),
        ("Numeric", "$(VersionMajor) >= 2"),
        ("StringFunction", "HasTrailingSlash('$(OutputPath)')"),
    ];

    private Dictionary<string, string> _conditions = null!;
    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private ElementLocation _location = null!;
    private string _evaluationDirectory = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _location = ElementLocation.EmptyLocation;
        _evaluationDirectory = Path.GetTempPath();

        _conditions = new(s_scenarios.Length);
        foreach ((string label, string condition) in s_scenarios)
        {
            _conditions[label] = condition;
        }

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("Configuration", "Release"));
        properties.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        properties.Set(ProjectPropertyInstance.Create("OutputPath", @"bin\Release\net11.0\"));
        properties.Set(ProjectPropertyInstance.Create("VersionMajor", "8"));

        _expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);
    }

    public static IEnumerable<object> Cases()
    {
        foreach ((string label, _) in s_scenarios)
        {
            yield return label;
        }
    }

    /// <summary>
    /// Fresh parse per invocation (scanner + parser + tree construction), independent of the
    /// expression-tree cache.
    /// </summary>
    [Benchmark]
    [ArgumentsSource(nameof(Cases))]
    public object Parse(string scenario)
        => new Parser().Parse(_conditions[scenario], ParserOptions.AllowAll, _location);

    /// <summary>
    /// End-to-end condition evaluation, including operand expansion. Uses the production
    /// expression-tree cache (warm after the first invocation), matching evaluation steady state.
    /// </summary>
    [Benchmark]
    [ArgumentsSource(nameof(Cases))]
    public bool Evaluate(string scenario)
        => ConditionEvaluator.EvaluateCondition(
            _conditions[scenario],
            ParserOptions.AllowAll,
            _expander,
            ExpanderOptions.ExpandProperties,
            _evaluationDirectory,
            _location,
            FileSystems.Default,
            loggingContext: null);
}
