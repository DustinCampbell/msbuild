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

[MemoryDiagnoser]
public class ExpanderBenchmark
{
    private IExpander<ProjectPropertyInstance, ProjectItemInstance> _expander = null!;
    private IElementLocation _elementLocation = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _elementLocation = ElementLocation.EmptyLocation;

        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("MSBuildProjectDirectory", @"C:\projects\myapp\src", mayBeReserved: true));
        properties.Set(ProjectPropertyInstance.Create("OutputPath", @"bin\Release\"));
        properties.Set(ProjectPropertyInstance.Create("AssemblyName", "MyApp"));
        properties.Set(ProjectPropertyInstance.Create("TargetFramework", "net10.0"));
        properties.Set(ProjectPropertyInstance.Create("Configuration", "Release"));
        properties.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        properties.Set(ProjectPropertyInstance.Create("IntermediateOutputPath", @"obj\Release\net10.0\"));
        properties.Set(ProjectPropertyInstance.Create("PackageVersion", "1.2.3-preview.4567"));

        _expander = ExpanderFactory.Create<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);
    }

    [Benchmark]
    public string ComplexPathExpansion()
    {
        return _expander.ExpandIntoStringLeaveEscaped(@"$(MSBuildProjectDirectory)\$(OutputPath)$(TargetFramework)\$(AssemblyName).dll", ExpanderOptions.ExpandProperties, _elementLocation);
    }
}
