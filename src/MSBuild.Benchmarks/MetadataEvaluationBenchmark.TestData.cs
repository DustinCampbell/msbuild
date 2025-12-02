// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class MetadataEvaluationBenchmark
{
    private static class TestData
    {
        public const string SimpleMetadataProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>
            </Project>
            """;

        public const string WellKnownMetadataProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>

              <!-- Use well-known metadata in transforms -->
              <ItemGroup>
                <CompileFilename Include="@(Compile->'%(Filename)')" />
                <CompileExtension Include="@(Compile->'%(Extension)')" />
                <CompileDirectory Include="@(Compile->'%(RootDir)%(Directory)')" />
                <CompileFullPath Include="@(Compile->'%(FullPath)')" />
                <CompileIdentity Include="@(Compile->'%(Identity)')" />
                <CompileModifiedTime Include="@(Compile->'%(ModifiedTime)')" />
                <CompileCreatedTime Include="@(Compile->'%(CreatedTime)')" />
                <CompileRecursiveDir Include="@(Compile->'%(RecursiveDir)')" />
              </ItemGroup>
            </Project>
            """;

        public const string CustomMetadataProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs">
                  <CustomMetadata1>Value1</CustomMetadata1>
                  <CustomMetadata2>Value2</CustomMetadata2>
                  <CustomMetadata3>Value3</CustomMetadata3>
                  <Link>src\%(Filename)%(Extension)</Link>
                  <Visible>true</Visible>
                  <DependentUpon>%(Filename).xaml</DependentUpon>
                  <SubType>Code</SubType>
                  <Generator>MSBuild:Compile</Generator>
                  <LastGenOutput>%(Filename).g.cs</LastGenOutput>
                </Compile>
              </ItemGroup>

              <!-- Transform with custom metadata -->
              <ItemGroup>
                <OutputFile Include="@(Compile->'bin\%(CustomMetadata1)\%(Filename).dll')" />
              </ItemGroup>
            </Project>
            """;
    }
}
