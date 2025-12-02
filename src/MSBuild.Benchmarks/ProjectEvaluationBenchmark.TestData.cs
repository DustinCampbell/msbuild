// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace MSBuild.Benchmarks;

public partial class ProjectEvaluationBenchmark
{
    private static class TestData
    {
        /// <summary>
        /// Simple project with basic properties and items for evaluation.
        /// </summary>
        public const string SimpleProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
                <Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
                <OutputPath>bin\$(Configuration)\</OutputPath>
                <IntermediateOutputPath>obj\$(Configuration)\</IntermediateOutputPath>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="Program.cs" />
                <Compile Include="Helper.cs" />
                <Reference Include="System" />
                <Reference Include="System.Core" />
              </ItemGroup>

              <Target Name="Build">
                <Message Text="Building $(Configuration) configuration..." />
              </Target>
            </Project>
            """;

        /// <summary>
        /// Complex project with property functions, item transforms, and conditions.
        /// </summary>
        public const string ComplexProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" DefaultTargets="Build" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
                <Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
                <OutputPath>bin\$(Configuration)\</OutputPath>
                <DefineConstants Condition="'$(Configuration)' == 'Debug'">DEBUG;TRACE</DefineConstants>
                <DefineConstants Condition="'$(Configuration)' == 'Release'">TRACE</DefineConstants>
                <OutputType>Library</OutputType>
                <RootNamespace>TestProject</RootNamespace>
                <AssemblyName>TestProject</AssemblyName>
                <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="**\*.cs" Exclude="obj\**;bin\**" />
                <Reference Include="System" />
                <Reference Include="System.Core" />
                <Reference Include="System.Xml" />
              </ItemGroup>

              <ItemGroup>
                <None Include="App.config" />
                <None Include="packages.config" />
              </ItemGroup>

              <ItemDefinitionGroup>
                <Compile>
                  <Optimize Condition="'$(Configuration)' == 'Release'">true</Optimize>
                  <DebugType Condition="'$(Configuration)' == 'Debug'">full</DebugType>
                </Compile>
              </ItemDefinitionGroup>

              <Choose>
                <When Condition="'$(Configuration)' == 'Debug'">
                  <PropertyGroup>
                    <DebugSymbols>true</DebugSymbols>
                    <Optimize>false</Optimize>
                  </PropertyGroup>
                </When>
                <Otherwise>
                  <PropertyGroup>
                    <DebugSymbols>false</DebugSymbols>
                    <Optimize>true</Optimize>
                  </PropertyGroup>
                </Otherwise>
              </Choose>

              <Target Name="Build" DependsOnTargets="CoreBuild">
                <Message Text="Configuration: $(Configuration)" />
                <Message Text="Platform: $(Platform)" />
                <Message Text="Output: $(OutputPath)" />
              </Target>

              <Target Name="CoreBuild">
                <ItemGroup>
                  <IntermediateAssembly Include="@(Compile->'$(IntermediateOutputPath)%(Filename).obj')" />
                </ItemGroup>
                <Message Text="Intermediate files: @(IntermediateAssembly)" />
              </Target>

              <Target Name="Clean">
                <Message Text="Cleaning $(OutputPath)..." />
              </Target>
            </Project>
            """;

        /// <summary>
        /// Large project with many properties, items, and property/item evaluations.
        /// </summary>
        internal static string LargeProjectXml { get; } = GenerateLargeProject();

        private static string GenerateLargeProject()
        {
            var builder = new StringBuilder();
            builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            builder.AppendLine(@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""Current"">");

            // Properties with conditions and references
            builder.AppendLine("  <PropertyGroup>");
            builder.AppendLine(@"    <Configuration Condition=""'$(Configuration)' == ''"">Debug</Configuration>");
            builder.AppendLine(@"    <Platform Condition=""'$(Platform)' == ''"">AnyCPU</Platform>");

            for (int i = 0; i < 50; i++)
            {
                // Some properties reference other properties to test evaluation order
                if (i > 0 && i % 5 == 0)
                {
                    builder.AppendLine($"    <Property{i}>$(Property{i - 1})_Extended</Property{i}>");
                }
                else
                {
                    builder.AppendLine($"    <Property{i}>Value{i}</Property{i}>");
                }
            }

            builder.AppendLine("  </PropertyGroup>");

            // Items with metadata
            builder.AppendLine("  <ItemGroup>");

            for (int i = 0; i < 100; i++)
            {
                builder.AppendLine($"    <Compile Include=\"File{i}.cs\">");
                builder.AppendLine($"      <Link>Folder\\File{i}.cs</Link>");
                builder.AppendLine($"      <DependentUpon>File{i}.Designer.cs</DependentUpon>");
                builder.AppendLine("    </Compile>");
            }

            builder.AppendLine("  </ItemGroup>");

            // Conditional item groups
            builder.AppendLine(@"  <ItemGroup Condition=""'$(Configuration)' == 'Debug'"">");
            for (int i = 0; i < 20; i++)
            {
                builder.AppendLine($"    <DebugFile Include=\"Debug{i}.cs\" />");
            }

            builder.AppendLine("  </ItemGroup>");

            // Targets with dependencies
            for (int i = 0; i < 20; i++)
            {
                if (i == 0)
                {
                    builder.AppendLine($"  <Target Name=\"Target{i}\">");
                }
                else
                {
                    builder.AppendLine($"  <Target Name=\"Target{i}\" DependsOnTargets=\"Target{i - 1}\">");
                }

                builder.AppendLine($"    <Message Text=\"Executing Target {i}: $(Property{i % 50})\" />");
                builder.AppendLine("  </Target>");
            }

            // Target with item transforms
            builder.AppendLine("  <Target Name=\"ItemTransforms\">");
            builder.AppendLine("    <ItemGroup>");
            builder.AppendLine(@"      <OutputFile Include=""@(Compile->'$(OutputPath)%(Filename).dll')"" />");
            builder.AppendLine("    </ItemGroup>");
            builder.AppendLine(@"    <Message Text=""Output files: @(OutputFile)"" />");
            builder.AppendLine("  </Target>");

            builder.AppendLine("</Project>");
            return builder.ToString();
        }
    }
}
