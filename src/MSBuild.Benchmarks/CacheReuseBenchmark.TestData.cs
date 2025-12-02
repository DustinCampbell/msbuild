// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class CacheReuseBenchmark
{
    private static class TestData
    {
        public const string ProjectContentXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>
                <OutputPath>bin\$(Configuration)\</OutputPath>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>
            </Project>
            """;
    }
}
