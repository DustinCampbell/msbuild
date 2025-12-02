// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class MultiProjectBenchmark
{
    private static class TestData
    {
        public static string GetProjectContent(int projectIndex) => $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
                <ProjectName>Project{{projectIndex}}</ProjectName>
                <OutputPath>bin\$(Configuration)\</OutputPath>
                <IntermediateOutputPath>obj\$(Configuration)\</IntermediateOutputPath>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="Project{{projectIndex}}\*.cs" />
              </ItemGroup>

              <ItemGroup>
                <Reference Include="System" />
                <Reference Include="System.Core" />
              </ItemGroup>
            </Project>
            """;
    }
}
