// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace MSBuild.Benchmarks;

public partial class ProjectImportBenchmark
{
    private static class TestData
    {
        /// <summary>
        /// Common properties file that would be imported.
        /// </summary>
        public const string CommonPropsXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <ImportedProperty>FromCommonProps</ImportedProperty>
                <OutputPath Condition="'$(OutputPath)' == ''">bin\$(Configuration)\</OutputPath>
                <IntermediateOutputPath>obj\$(Configuration)\</IntermediateOutputPath>
                <TargetFramework Condition="'$(TargetFramework)' == ''">net8.0</TargetFramework>
              </PropertyGroup>

              <ItemGroup>
                <ImportedCompile Include="SharedCode.cs" />
              </ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Common targets file that would be imported.
        /// </summary>
        public const string CommonTargetsXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <Target Name="ImportedTarget">
                <Message Text="Running imported target" />
              </Target>

              <Target Name="BeforeBuild">
                <Message Text="Before build from import" />
              </Target>

              <Target Name="AfterBuild">
                <Message Text="After build from import" />
              </Target>
            </Project>
            """;

        /// <summary>
        /// Gets a project with explicit imports.
        /// </summary>
        public static string GetProjectWithImportsXml(string commonPropsPath, string commonTargetsPath) => $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
                <Import Project="{{commonPropsPath}}" />
              
                <PropertyGroup>
                <Configuration Condition="'$(Configuration)' == ''">Debug</Configuration>
                <Platform Condition="'$(Platform)' == ''">AnyCPU</Platform>
                <ProjectProperty>LocalValue</ProjectProperty>
                </PropertyGroup>

                <ItemGroup>
                <Compile Include="Program.cs" />
                <Reference Include="System" />
                </ItemGroup>

                <Import Project="{{commonTargetsPath}}" />

                <Import Project="{{commonTargetsPath}}" Condition="'$(Configuration)' == 'Debug'" />
            </Project>
            """;

        /// <summary>
        /// Gets a project with multiple imports to test import resolution overhead.
        /// </summary>
        public static string GetProjectWithMultipleImportsXml(string baseDirectory)
        {
            var builder = new StringBuilder();
            builder.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
            builder.AppendLine(@"<Project xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"" ToolsVersion=""Current"">");

            // Add multiple imports
            for (int i = 0; i < 5; i++)
            {
                var importPath = Path.Combine(baseDirectory, $"Import{i}.props");
                builder.AppendLine($@"  <Import Project=""{importPath}"" />");
            }

            builder.AppendLine("  <PropertyGroup>");
            builder.AppendLine("    <Configuration>Debug</Configuration>");
            builder.AppendLine("    <Platform>AnyCPU</Platform>");
            builder.AppendLine("  </PropertyGroup>");

            builder.AppendLine("  <ItemGroup>");
            builder.AppendLine(@"    <Compile Include=""Program.cs"" />");
            builder.AppendLine("  </ItemGroup>");

            // Conditional imports
            for (int i = 0; i < 3; i++)
            {
                var importPath = Path.Combine(baseDirectory, $"Import{i}.props");
                builder.AppendLine($@"  <Import Project=""{importPath}"" Condition=""'$(Configuration)' == 'Debug'"" />");
            }

            builder.AppendLine("</Project>");
            return builder.ToString();
        }

        /// <summary>
        /// Gets a generic import file for testing.
        /// </summary>
        public static string GetGenericImportXml(int index) => $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <ImportedProperty{{index}}>Value{{index}}</ImportedProperty{{index}}>
              </PropertyGroup>

              <ItemGroup>
                <ImportedItem{{index}} Include="File{{index}}.cs" />
              </ItemGroup>

              <Target Name="ImportedTarget{{index}}">
                <Message Text="Target from import {{index}}" />
              </Target>
            </Project>
            """;
    }
}
