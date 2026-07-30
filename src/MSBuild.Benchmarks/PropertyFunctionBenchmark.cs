// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

/// <summary>
/// Benchmarks for property-function expansion: instance string functions (<c>$(Prop.Substring(..))</c>),
/// static functions (<c>$([MSBuild]::Add(..))</c>, <c>$([System.IO.Path]::Combine(..))</c>), and
/// chained calls. These exercise the shared argument-extraction path
/// (<c>ExtractFunctionArguments</c> / <c>RealizeFunctionArguments</c>) and value coercion
/// (<c>ConversionUtilities</c>) that the StringSegment work touches but the other Expander/shredder
/// benchmarks do not reach (their transforms take the quoted metadata fast path).
/// </summary>
[MemoryDiagnoser]
public class PropertyFunctionBenchmark
{
    private Expander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private IElementLocation _location = null!;

    // Instance string functions.
    private string _instanceToLower = null!;
    private string _instanceSubstring = null!;
    private string _instanceReplace = null!;
    private string _instanceChained = null!;

    // Static functions.
    private string _staticMathAdd = null!;
    private string _staticValueOrDefault = null!;
    private string _staticPathCombine = null!;

    // A function whose argument is itself an expression that must expand first.
    private string _nestedArgument = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _location = ElementLocation.EmptyLocation;

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("Configuration", "Release"));
        properties.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        properties.Set(ProjectPropertyInstance.Create("OutputPath", @"bin\Release\net11.0"));
        properties.Set(ProjectPropertyInstance.Create("RootNamespace", "MyProject.Core"));
        properties.Set(ProjectPropertyInstance.Create("AssemblyName", "MyProject.Core"));
        properties.Set(ProjectPropertyInstance.Create("TargetFramework", "net11.0"));

        _expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);

        // Instance functions (call a method on the string value of a property).
        _instanceToLower = "$(Configuration.ToLowerInvariant())";
        _instanceSubstring = "$(OutputPath.Substring(0, 3))";
        _instanceReplace = "$(RootNamespace.Replace('.', '_'))";
        _instanceChained = "$(AssemblyName.Substring(0, 9).ToUpperInvariant())";

        // Static functions.
        _staticMathAdd = "$([MSBuild]::Add(40, 2))";
        _staticValueOrDefault = "$([MSBuild]::ValueOrDefault('$(Configuration)', 'Debug'))";
        _staticPathCombine = @"$([System.IO.Path]::Combine('$(OutputPath)', 'app.dll'))";

        // Argument that expands before the function runs.
        _nestedArgument = "$(Configuration.Replace('$(Platform)', 'x64'))";
    }

    [Benchmark]
    public string Instance_ToLower()
        => _expander.ExpandIntoStringLeaveEscaped(_instanceToLower, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Instance_Substring()
        => _expander.ExpandIntoStringLeaveEscaped(_instanceSubstring, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Instance_Replace()
        => _expander.ExpandIntoStringLeaveEscaped(_instanceReplace, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Instance_Chained()
        => _expander.ExpandIntoStringLeaveEscaped(_instanceChained, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Static_MathAdd()
        => _expander.ExpandIntoStringLeaveEscaped(_staticMathAdd, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Static_ValueOrDefault()
        => _expander.ExpandIntoStringLeaveEscaped(_staticValueOrDefault, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string Static_PathCombine()
        => _expander.ExpandIntoStringLeaveEscaped(_staticPathCombine, ExpanderOptions.ExpandProperties, _location);

    [Benchmark]
    public string NestedArgument()
        => _expander.ExpandIntoStringLeaveEscaped(_nestedArgument, ExpanderOptions.ExpandProperties, _location);
}
