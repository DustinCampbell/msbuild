// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class ProjectGlobBenchmark
{
    private static class TestData
    {
        /// <summary>
        /// Gets a project with a simple (non-recursive) glob pattern.
        /// </summary>
        public const string ProjectWithSimpleGlobXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>

              <ItemGroup>
                <None Include="*.txt" />
                <None Include="*.config" />
              </ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Gets a project with recursive glob patterns (like SDK-style projects).
        /// </summary>
        public const string ProjectWithRecursiveGlobsXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>

              <ItemGroup>
                <!-- Recursive glob - searches all subdirectories -->
                <Compile Include="**/*.cs" />
              </ItemGroup>

              <ItemGroup>
                <EmbeddedResource Include="**/*.resx" />
              </ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Gets a project with multiple glob patterns (common in real projects).
        /// </summary>
        public const string ProjectWithMultipleGlobsXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>

              <ItemGroup>
                <!-- Multiple glob patterns -->
                <Compile Include="**/*.cs" />
                <Compile Include="**/*.vb" />
                <Compile Include="**/*.fs" />
              </ItemGroup>

              <ItemGroup>
                <Content Include="**/*.html" />
                <Content Include="**/*.css" />
                <Content Include="**/*.js" />
              </ItemGroup>

              <ItemGroup>
                <EmbeddedResource Include="**/*.resx" />
                <EmbeddedResource Include="**/*.resources" />
              </ItemGroup>

              <ItemGroup>
                <None Include="**/*.json" />
                <None Include="**/*.xml" />
                <None Include="**/*.config" />
              </ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Gets a project with glob patterns and excludes (like SDK-style projects).
        /// </summary>
        public const string ProjectWithExcludesXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
                <BaseIntermediateOutputPath>obj\</BaseIntermediateOutputPath>
                <OutputPath>bin\$(Configuration)\</OutputPath>
              </PropertyGroup>

              <ItemGroup>
                <!-- Include all .cs files recursively -->
                <Compile Include="**/*.cs" 
                         Exclude="$(BaseIntermediateOutputPath)**;$(OutputPath)**;**/*.Designer.cs" />
              </ItemGroup>

              <ItemGroup>
                <EmbeddedResource Include="**/*.resx" 
                                  Exclude="$(BaseIntermediateOutputPath)**;$(OutputPath)**" />
              </ItemGroup>

              <ItemGroup>
                <!-- Exclude common directories -->
                <None Include="**/*" 
                      Exclude="$(BaseIntermediateOutputPath)**;$(OutputPath)**;bin/**;obj/**;**/*.user;**/*.suo" />
              </ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Gets a project with glob and Remove (SDK-style pattern for removing default items).
        /// </summary>
        public const string ProjectWithGlobAndRemoveXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>

              <ItemGroup>
                <!-- Include all files -->
                <Compile Include="**/*.cs" />

                <!-- Remove specific files that were included by glob -->
                <Compile Remove="**/Temp*.cs" />
                <Compile Remove="**/obj/**/*.cs" />
                <Compile Remove="**/bin/**/*.cs" />
              </ItemGroup>

              <ItemGroup>
                <EmbeddedResource Include="**/*.resx" />
                <EmbeddedResource Remove="**/*.Designer.resx" />
              </ItemGroup>

              <ItemGroup>
                <!-- Include then selectively remove -->
                <Content Include="**/*.txt" />
                <Content Remove="**/readme.txt" />
                <Content Remove="**/temp/**" />
              </ItemGroup>
            </Project>
            """;

        /// <summary>
        /// Gets a project with glob and metadata assignments.
        /// </summary>
        public const string ProjectWithGlobAndMetadataXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="Current">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
              </PropertyGroup>

              <ItemGroup>
                <!-- Glob with metadata -->
                <Compile Include="**/*.cs">
                  <AutoGen>False</AutoGen>
                  <DesignTime>False</DesignTime>
                  <DependentUpon>%(Filename).xaml</DependentUpon>
                </Compile>
              </ItemGroup>

              <ItemGroup>
                <!-- Different metadata for different patterns -->
                <Content Include="**/*.css">
                  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                  <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
                </Content>

                <Content Include="**/*.js">
                  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
                  <Link>scripts\%(RecursiveDir)%(Filename)%(Extension)</Link>
                </Content>
              </ItemGroup>

              <ItemGroup>
                <EmbeddedResource Include="**/*.resx">
                  <ManifestResourceName>$(RootNamespace).%(RecursiveDir)%(Filename)</ManifestResourceName>
                  <Generator>ResXFileCodeGenerator</Generator>
                  <LastGenOutput>%(Filename).Designer.cs</LastGenOutput>
                </EmbeddedResource>
              </ItemGroup>

              <ItemGroup>
                <!-- Conditional metadata -->
                <None Include="**/*.json">
                  <CopyToOutputDirectory Condition="'$(Configuration)' == 'Debug'">PreserveNewest</CopyToOutputDirectory>
                  <CopyToOutputDirectory Condition="'$(Configuration)' == 'Release'">Never</CopyToOutputDirectory>
                </None>
              </ItemGroup>
            </Project>
            """;
    }
}
