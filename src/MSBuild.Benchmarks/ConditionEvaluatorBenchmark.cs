// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class ConditionEvaluatorBenchmark
{
    private const ParserOptions DefaultOptions = ParserOptions.AllowAll;

    // Simple expressions
    private const string SimpleStringEquality = "'Debug' == 'Debug'";
    private const string SimplePropertyEquality = "$(Configuration) == 'Debug'";
    private const string SimpleNumericComparison = "1 > 0";
    private const string SimpleBooleanLiteral = "true";

    // Complex expressions
    private const string ComplexCondition = "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU' and '$(TargetFramework)' != ''";
    private const string NestedProperties = "$(Prop1.StartsWith('$(Prop2)')) and '$(Prop3)' == '$(Prop4)'";
    private const string MultipleOr = "'$(Configuration)' == 'Debug' or '$(Configuration)' == 'Release' or '$(Configuration)' == 'Test'";
    private const string FunctionCall = "Exists('$(ProjectFile)') and HasTrailingSlash('$(OutputPath)')";

    // Real-world SDK conditions
    private const string TypicalSdkCondition = "'$(Configuration)|$(Platform)' == 'Debug|AnyCPU' or '$(Configuration)|$(Platform)' == 'Release|AnyCPU'";
    private const string VersionCheck = "'$(TargetFrameworkVersion)' >= '4.7.2' and '$(LangVersion)' != 'latest'";
    private const string EmptyCheck = "'$(SomeProperty)' != ''";
    private const string PropertyConcatenation = "'$(RootNamespace).$(AssemblyName)' == 'MyCompany.MyProduct'";

    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private ExpanderOptions _expanderOptions;
    private ElementLocation _elementLocation = null!;
    private string _evaluationDirectory = null!;
    private PropertyDictionary<ProjectPropertyInstance> _properties = null!;

    [GlobalSetup]
    public void Setup()
    {
        _properties = new PropertyDictionary<ProjectPropertyInstance>();
        _properties.Set(ProjectPropertyInstance.Create("Configuration", "Debug"));
        _properties.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        _properties.Set(ProjectPropertyInstance.Create("TargetFramework", "net8.0"));
        _properties.Set(ProjectPropertyInstance.Create("TargetFrameworkVersion", "4.8"));
        _properties.Set(ProjectPropertyInstance.Create("LangVersion", "12.0"));
        _properties.Set(ProjectPropertyInstance.Create("ProjectFile", "test.csproj"));
        _properties.Set(ProjectPropertyInstance.Create("OutputPath", "bin\\Debug\\"));
        _properties.Set(ProjectPropertyInstance.Create("Prop1", "value1"));
        _properties.Set(ProjectPropertyInstance.Create("Prop2", "value"));
        _properties.Set(ProjectPropertyInstance.Create("Prop3", "test"));
        _properties.Set(ProjectPropertyInstance.Create("Prop4", "test"));
        _properties.Set(ProjectPropertyInstance.Create("RootNamespace", "MyCompany"));
        _properties.Set(ProjectPropertyInstance.Create("AssemblyName", "MyProduct"));

        _expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(_properties, FileSystems.Default);
        _expanderOptions = ExpanderOptions.ExpandProperties;
        _elementLocation = ElementLocation.EmptyLocation;
        _evaluationDirectory = Environment.CurrentDirectory;
    }

    [Benchmark]
    public bool SimpleStringEquality_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            SimpleStringEquality,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool SimplePropertyEquality_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            SimplePropertyEquality,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool SimpleNumericComparison_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            SimpleNumericComparison,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool SimpleBooleanLiteral_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            SimpleBooleanLiteral,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool ComplexCondition_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            ComplexCondition,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool NestedProperties_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            NestedProperties,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool MultipleOr_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            MultipleOr,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool FunctionCall_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            FunctionCall,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool TypicalSdkCondition_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            TypicalSdkCondition,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool VersionCheck_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            VersionCheck,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool EmptyCheck_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            EmptyCheck,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);

    [Benchmark]
    public bool PropertyConcatenation_Evaluate() =>
        ConditionEvaluator.EvaluateCondition(
            PropertyConcatenation,
            DefaultOptions,
            _expander,
            _expanderOptions,
            _evaluationDirectory,
            _elementLocation,
            FileSystems.Default,
            loggingContext: null);
}
