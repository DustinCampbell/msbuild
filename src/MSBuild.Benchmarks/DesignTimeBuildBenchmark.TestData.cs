// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class DesignTimeBuildBenchmark
{
    private static class TestData
    {
        public static string NormalProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
                <OutputPath>bin\$(Configuration)\</OutputPath>
                <IntermediateOutputPath>obj\$(Configuration)\</IntermediateOutputPath>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>

              <ItemGroup>
                <Reference Include="System" />
              </ItemGroup>
            </Project>
            """;

        public const string DesignTimeProject = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
                <OutputPath>bin\$(Configuration)\</OutputPath>
                <IntermediateOutputPath>obj\$(Configuration)\</IntermediateOutputPath>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>

              <!-- Design-time build optimizations -->
              <PropertyGroup Condition="'$(DesignTimeBuild)' == 'true'">
                <SkipCompilerExecution>true</SkipCompilerExecution>
                <CopyBuildOutputToOutputDirectory>false</CopyBuildOutputToOutputDirectory>
                <CopyOutputSymbolsToOutputDirectory>false</CopyOutputSymbolsToOutputDirectory>
                <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>

              <ItemGroup>
                <Reference Include="System" />
              </ItemGroup>

              <!-- Skip expensive targets during design-time build -->
              <Target Name="ResolveReferences" Condition="'$(DesignTimeBuild)' != 'true'" />
              <Target Name="CoreCompile" Condition="'$(DesignTimeBuild)' != 'true'" />
            </Project>
            """;
    }
}
