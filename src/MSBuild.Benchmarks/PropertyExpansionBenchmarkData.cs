// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

internal static class PropertyExpansionBenchmarkData
{
    internal static Expander<ProjectPropertyInstance, ProjectItemInstance> CreateExpander()
    {
        var properties = new PropertyDictionary<ProjectPropertyInstance>();
        properties.Set(ProjectPropertyInstance.Create("Configuration", "Release"));
        properties.Set(ProjectPropertyInstance.Create("Platform", "AnyCPU"));
        properties.Set(ProjectPropertyInstance.Create("TargetFramework", "net11.0"));
        properties.Set(ProjectPropertyInstance.Create("OutputPath", @"bin\Release\net11.0"));
        properties.Set(ProjectPropertyInstance.Create("Prefix", "prefix-"));
        properties.Set(ProjectPropertyInstance.Create("Suffix", "suffix"));
        properties.Set(ProjectPropertyInstance.Create("Text", "prefix-value"));
        properties.Set(ProjectPropertyInstance.Create("LongValue", new string('x', 1024)));

        return new Expander<ProjectPropertyInstance, ProjectItemInstance>(properties, FileSystems.Default);
    }
}
