// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace MSBuild.Benchmarks;

public partial class ProjectParserBenchmark
{
    private static class TestData
    {
        /// <summary>
        /// Simple project with basic elements (properties, items, single target).
        /// </summary>
        public const string SimpleProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Program.cs" />
                <Reference Include="System" />
              </ItemGroup>
              <Target Name="Build">
                <Message Text="Building project..." />
              </Target>
            </Project>
            """;

        /// <summary>
        /// Complex project with conditions, Choose elements, ItemDefinitionGroup, and multiple targets.
        /// </summary>
        public const string ComplexProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" DefaultTargets="Build" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
                <Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
                <OutputPath>bin\$(Configuration)\</OutputPath>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="**\*.cs" Exclude="obj\**" />
                <Reference Include="System" />
                <Reference Include="System.Core" />
              </ItemGroup>

              <ItemDefinitionGroup>
                <Compile>
                  <Optimize>true</Optimize>
                </Compile>
              </ItemDefinitionGroup>

              <Choose>
                <When Condition="'$(Configuration)' == 'Debug'">
                  <PropertyGroup>
                    <DebugSymbols>true</DebugSymbols>
                  </PropertyGroup>
                </When>
                <Otherwise>
                  <PropertyGroup>
                    <Optimize>true</Optimize>
                  </PropertyGroup>
                </Otherwise>
              </Choose>

              <Target Name="Build" DependsOnTargets="CoreBuild">
                <Message Text="Configuration: $(Configuration)" />
              </Target>

              <Target Name="CoreBuild">
                <ItemGroup>
                  <IntermediateAssembly Include="@(Compile->'%(Filename).dll')" />
                </ItemGroup>
              </Target>

              <UsingTask TaskName="CustomTask" AssemblyFile="Tasks.dll" />
            </Project>
            """;

        /// <summary>
        /// Large project with many properties (50), items (100), and targets (20).
        /// </summary>
        public static string LargeProjectXml { get; } = GenerateLargeProject();

        private static string GenerateLargeProject()
        {
            var builder = new StringBuilder();
            builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            builder.AppendLine(@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""Current"">");
            builder.AppendLine("  <PropertyGroup>");

            for (int i = 0; i < 50; i++)
            {
                builder.AppendLine($"    <Property{i}>Value{i}</Property{i}>");
            }

            builder.AppendLine("  </PropertyGroup>");
            builder.AppendLine("  <ItemGroup>");

            for (int i = 0; i < 100; i++)
            {
                builder.AppendLine($"    <Compile Include=\"File{i}.cs\">");
                builder.AppendLine($"      <Link>Folder\\File{i}.cs</Link>");
                builder.AppendLine("    </Compile>");
            }

            builder.AppendLine("  </ItemGroup>");

            for (int i = 0; i < 20; i++)
            {
                builder.AppendLine($"  <Target Name=\"Target{i}\">");
                builder.AppendLine($"    <Message Text=\"Target {i}\" />");
                builder.AppendLine("  </Target>");
            }

            builder.AppendLine("</Project>");
            return builder.ToString();
        }
    }
}
