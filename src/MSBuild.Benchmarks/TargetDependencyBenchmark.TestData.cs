// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class TargetDependencyBenchmark
{
    private static class TestData
    {
        public const string SimpleTargetsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
              </PropertyGroup>

              <Target Name="Build">
                <Message Text="Building..." />
              </Target>

              <Target Name="Clean">
                <Message Text="Cleaning..." />
              </Target>

              <Target Name="Rebuild" DependsOnTargets="Clean;Build">
                <Message Text="Rebuilding..." />
              </Target>
            </Project>
            """;

        public const string ComplexTargetsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
              </PropertyGroup>

              <Target Name="BeforeBuild">
                <Message Text="Before build" />
              </Target>

              <Target Name="CoreBuild" DependsOnTargets="BeforeBuild">
                <Message Text="Core build" />
              </Target>

              <Target Name="AfterBuild" DependsOnTargets="CoreBuild">
                <Message Text="After build" />
              </Target>

              <Target Name="Build" DependsOnTargets="BeforeBuild;CoreBuild;AfterBuild">
                <Message Text="Build complete" />
              </Target>

              <Target Name="BeforeClean">
                <Message Text="Before clean" />
              </Target>

              <Target Name="CoreClean" DependsOnTargets="BeforeClean">
                <Message Text="Core clean" />
              </Target>

              <Target Name="Clean" DependsOnTargets="BeforeClean;CoreClean">
                <Message Text="Clean complete" />
              </Target>

              <Target Name="Rebuild" DependsOnTargets="Clean;Build">
                <Message Text="Rebuild complete" />
              </Target>

              <Target Name="Test" DependsOnTargets="Build">
                 <Message Text="Running tests" />
              </Target>

              <Target Name="Package" DependsOnTargets="Build">
                  <Message Text="Creating package" />
              </Target>
            </Project>
            """;

        public const string DeepDependenciesProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
              </PropertyGroup>

              <!-- Create a deep dependency chain -->
              <Target Name="Level1">
                <Message Text="Level 1" />
              </Target>

              <Target Name="Level2" DependsOnTargets="Level1">
                <Message Text="Level 2" />
              </Target>

              <Target Name="Level3" DependsOnTargets="Level2">
                <Message Text="Level 3" />
              </Target>

              <Target Name="Level4" DependsOnTargets="Level3">
                <Message Text="Level 4" />
              </Target>

              <Target Name="Level5" DependsOnTargets="Level4">
                <Message Text="Level 5" />
              </Target>

              <Target Name="Level6" DependsOnTargets="Level5">
                <Message Text="Level 6" />
              </Target>

              <Target Name="Level7" DependsOnTargets="Level6">
                <Message Text="Level 7" />
              </Target>

              <Target Name="Level8" DependsOnTargets="Level7">
                <Message Text="Level 8" />
              </Target>

              <Target Name="Level9" DependsOnTargets="Level8">
                <Message Text="Level 9" />
              </Target>

              <Target Name="Level10" DependsOnTargets="Level9">
                <Message Text="Level 10" />
              </Target>

              <!-- Multiple dependency branches -->
              <Target Name="Branch1A" DependsOnTargets="Level5">
                <Message Text="Branch 1A" />
              </Target>

              <Target Name="Branch1B" DependsOnTargets="Level5">
                <Message Text="Branch 1B" />
              </Target>

              <Target Name="Branch2" DependsOnTargets="Branch1A;Branch1B">
                <Message Text="Branch 2" />
              </Target>

              <Target Name="FinalTarget" DependsOnTargets="Level10;Branch2">
                <Message Text="Final target" />
              </Target>
            </Project>
            """;
    }
}
