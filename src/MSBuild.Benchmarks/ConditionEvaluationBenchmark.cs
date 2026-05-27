// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;
using static MSBuild.Benchmarks.ConditionStrings;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks for MSBuild condition evaluation covering parsing, tree construction,
///  and evaluation of common condition patterns.
/// </summary>
[MemoryDiagnoser]
public class ConditionEvaluationBenchmark
{
    private PropertyDictionary<ProjectPropertyInstance> _propertyBag = null!;
    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private ElementLocation _elementLocation = null!;
    private string _evaluationDirectory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
        _propertyBag.Set(ProjectPropertyInstance.Create("Configuration", "Debug"));
        _propertyBag.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        _propertyBag.Set(ProjectPropertyInstance.Create("TargetFramework", "net10.0"));
        _propertyBag.Set(ProjectPropertyInstance.Create("TargetFrameworkIdentifier", ".NETCoreApp"));
        _propertyBag.Set(ProjectPropertyInstance.Create("TargetFrameworkVersion", "10.0"));
        _propertyBag.Set(ProjectPropertyInstance.Create("UseWindowsForms", "false"));
        _propertyBag.Set(ProjectPropertyInstance.Create("BuildNumber", "42"));
        _propertyBag.Set(ProjectPropertyInstance.Create("ErrorCount", "0"));
        _propertyBag.Set(ProjectPropertyInstance.Create("OutputPath", @"bin\Debug\"));
        _propertyBag.Set(ProjectPropertyInstance.Create("IsPackable", "true"));
        _propertyBag.Set(ProjectPropertyInstance.Create("GenerateDocumentationFile", "false"));
        _propertyBag.Set(ProjectPropertyInstance.Create("RootNamespace", "MyApp"));
        _propertyBag.Set(ProjectPropertyInstance.Create("AssemblyName", "MyApp"));
        _propertyBag.Set(ProjectPropertyInstance.Create("A", "1"));
        _propertyBag.Set(ProjectPropertyInstance.Create("B", "2"));
        _propertyBag.Set(ProjectPropertyInstance.Create("C", "3"));
        _propertyBag.Set(ProjectPropertyInstance.Create("D", "4"));
        _propertyBag.Set(ProjectPropertyInstance.Create("MSBuildProjectDirectory", ".", mayBeReserved: true));

        _expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(_propertyBag, FileSystems.Default);
        _elementLocation = ElementLocation.Create("benchmark.proj", 1, 1);
        _evaluationDirectory = Directory.GetCurrentDirectory();
    }

    // --- Simple comparisons ---

    [Benchmark]
    public bool SimpleEquality_Evaluate()
        => Evaluate(SimpleEquality);

    [Benchmark]
    public bool EmptyCheck_Evaluate()
        => Evaluate(EmptyCheck);

    [Benchmark]
    public bool NonEmptyCheck_Evaluate()
        => Evaluate(NonEmptyCheck);

    [Benchmark]
    public bool NumericComparison_Evaluate()
        => Evaluate(NumericComparison);

    [Benchmark]
    public bool NumericLessThan_Evaluate()
        => Evaluate(NumericLessThan);

    // --- Boolean operators ---

    [Benchmark]
    public bool BooleanAnd_Evaluate()
        => Evaluate(BooleanAnd);

    [Benchmark]
    public bool BooleanOr_Evaluate()
        => Evaluate(BooleanOr);

    [Benchmark]
    public bool Negation_Evaluate()
        => Evaluate(Negation);

    [Benchmark]
    public bool NegatedEquality_Evaluate()
        => Evaluate(NegatedEquality);

    // --- Complex / nested expressions ---

    [Benchmark]
    public bool Complex_Evaluate()
        => Evaluate(Complex);

    [Benchmark]
    public bool DeepNesting_Evaluate()
        => Evaluate(DeepNesting);

    [Benchmark]
    public bool MultipleAnds_Evaluate()
        => Evaluate(MultipleAnds);

    [Benchmark]
    public bool MixedAndOr_Evaluate()
        => Evaluate(MixedAndOr);

    // --- Function calls ---

    [Benchmark]
    public bool ExistsCheck_Evaluate()
        => Evaluate(ExistsCheck);

    [Benchmark]
    public bool HasTrailingSlash_Evaluate()
        => Evaluate(HasTrailingSlashCheck);

    [Benchmark]
    public bool ExistsWithConcatenation_Evaluate()
        => Evaluate(ExistsWithConcatenation);

    // --- Property concatenation ---

    [Benchmark]
    public bool ConcatenatedComparison_Evaluate()
        => Evaluate(ConcatenatedComparison);

    [Benchmark]
    public bool MultipleProperties_Evaluate()
        => Evaluate(MultipleProperties);

    // --- Boolean literals ---

    [Benchmark]
    public bool BooleanLiteralTrue_Evaluate()
        => Evaluate(BooleanLiteralTrue);

    [Benchmark]
    public bool BooleanLiteralFalse_Evaluate()
        => Evaluate(BooleanLiteralFalse);

    [Benchmark]
    public bool BareBoolean_Evaluate()
        => Evaluate(BareBoolean);

    // --- Item and metadata expressions ---

    [Benchmark]
    public bool ItemListCondition_Evaluate()
        => Evaluate(ItemListCondition);

    [Benchmark]
    public bool MetadataCondition_Evaluate()
        => Evaluate(MetadataCondition);

    // --- Realistic SDK conditions ---

    [Benchmark]
    public bool RealisticSdkCondition_Evaluate()
        => Evaluate(RealisticSdkCondition);

    [Benchmark]
    public bool RealisticMultiTargeting_Evaluate()
        => Evaluate(RealisticMultiTargeting);

    private bool Evaluate(string condition)
        => ConditionEvaluator.EvaluateCondition(
            condition,
            ParserOptions.AllowAll,
            _expander,
            ExpanderOptions.ExpandProperties,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);
}
