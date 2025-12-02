// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class ConditionEvaluationBenchmark
{
    private static class TestData
    {
        public const string SimpleConditionsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
                <Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
                <OutputPath Condition="'$(OutputPath)' == ''">bin\$(Configuration)\</OutputPath>
              </PropertyGroup>

              <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                <DebugSymbols>true</DebugSymbols>
                <DebugType>full</DebugType>
                <Optimize>false</Optimize>
                <DefineConstants>DEBUG;TRACE</DefineConstants>
              </PropertyGroup>

              <PropertyGroup Condition="'$(Configuration)' == 'Release'">
                <DebugType>pdbonly</DebugType>
                <Optimize>true</Optimize>
                <DefineConstants>TRACE</DefineConstants>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
                <Reference Include="System" Condition="'$(TargetFrameworkIdentifier)' == '.NETFramework'" />
              </ItemGroup>
            </Project>
            """;

        public const string ComplexConditionsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>

              <!-- Complex AND/OR conditions -->
              <PropertyGroup Condition="'$(Configuration)' == 'Debug' AND '$(Platform)' == 'AnyCPU'">
                <DebugBuild>true</DebugBuild>
              </PropertyGroup>

              <PropertyGroup Condition="'$(Configuration)' == 'Release' OR '$(Optimize)' == 'true'">
                <OptimizeBuild>true</OptimizeBuild>
              </PropertyGroup>

              <!-- Nested conditions -->
              <PropertyGroup Condition="('$(Configuration)' == 'Debug' OR '$(Configuration)' == 'Release') AND '$(Platform)' != 'x86'">
                <StandardBuild>true</StandardBuild>
              </PropertyGroup>

              <!-- Numeric comparisons -->
              <PropertyGroup>
                <MajorVersion>1</MajorVersion>
                <MinorVersion>2</MinorVersion>
              </PropertyGroup>

              <PropertyGroup Condition="'$(MajorVersion)' &gt;= '1'">
                <ModernVersion>true</ModernVersion>
              </PropertyGroup>

              <PropertyGroup Condition="'$(MinorVersion)' &lt; '5'">
                <EarlyVersion>true</EarlyVersion>
              </PropertyGroup>

              <!-- String operations in conditions -->
              <ItemGroup Condition="$(TargetFramework.StartsWith('net'))">
                <DotNetReference Include="System.Runtime" />
              </ItemGroup>

              <ItemGroup Condition="$(TargetFramework.Contains('core'))">
                <CoreReference Include="Microsoft.NETCore.App" />
              </ItemGroup>

              <!-- Negation and complex logic -->
              <PropertyGroup Condition="'$(Configuration)' != 'Debug' AND '$(Configuration)' != 'Release'">
                <CustomConfiguration>true</CustomConfiguration>
              </PropertyGroup>

              <PropertyGroup Condition="!('$(Platform)' == 'x86' OR '$(Platform)' == 'x64')">
                <AnyCPUBuild>true</AnyCPUBuild>
              </PropertyGroup>
            </Project>
            """;

        public const string ExistsConditionsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>

              <!-- File existence checks -->
              <PropertyGroup Condition="Exists('File0.cs')">
                <HasFile0>true</HasFile0>
              </PropertyGroup>

              <PropertyGroup Condition="Exists('File1.cs')">
                <HasFile1>true</HasFile1>
              </PropertyGroup>

              <PropertyGroup Condition="Exists('NonExistent.cs')">
                <HasNonExistent>true</HasNonExistent>
              </PropertyGroup>

              <!-- Directory existence checks -->
              <PropertyGroup Condition="Exists('$(MSBuildProjectDirectory)')">
                <ProjectDirExists>true</ProjectDirExists>
              </PropertyGroup>

              <PropertyGroup Condition="Exists('$(MSBuildProjectDirectory)\bin')">
                <BinDirExists>true</BinDirExists>
              </PropertyGroup>

              <!-- Combined Exists with logic -->
              <PropertyGroup Condition="Exists('File0.cs') AND Exists('File1.cs')">
                <BothFilesExist>true</BothFilesExist>
              </PropertyGroup>

              <PropertyGroup Condition="Exists('File0.cs') OR Exists('NonExistent.cs')">
                <AtLeastOneExists>true</AtLeastOneExists>
              </PropertyGroup>

              <!-- Exists in item conditions -->
              <ItemGroup>
                <Compile Include="File0.cs" Condition="Exists('File0.cs')" />
                <Compile Include="File1.cs" Condition="Exists('File1.cs')" />
                <Compile Include="File2.cs" Condition="Exists('File2.cs')" />
                <Compile Include="File3.cs" Condition="Exists('File3.cs')" />
                <Compile Include="File4.cs" Condition="Exists('File4.cs')" />
              </ItemGroup>

              <!-- HasTrailingSlash checks -->
              <PropertyGroup Condition="HasTrailingSlash('$(MSBuildProjectDirectory)')">
                <DirHasSlash>true</DirHasSlash>
              </PropertyGroup>
            </Project>
            """;
    }
}
