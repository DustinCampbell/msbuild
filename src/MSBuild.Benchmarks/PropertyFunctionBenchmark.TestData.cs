// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace MSBuild.Benchmarks;

public partial class PropertyFunctionBenchmark
{
    private static class TestData
    {
        public const string SimplePropertiesProjectXml = """
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
            </Project>
            """;

        public const string PropertyFunctionsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>

                <!-- MSBuild property functions -->
                <ProjectDir>$([System.IO.Path]::GetFullPath('$(MSBuildProjectDirectory)'))</ProjectDir>
                <ProjectName>$([System.IO.Path]::GetFileNameWithoutExtension('$(MSBuildProjectFile)'))</ProjectName>
                <CurrentYear>$([System.DateTime]::Now.Year)</CurrentYear>
                <RandomGuid>$([System.Guid]::NewGuid())</RandomGuid>

                <!-- String manipulation -->
                <UpperConfig>$(Configuration.ToUpper())</UpperConfig>
                <LowerConfig>$(Configuration.ToLower())</LowerConfig>
                <ConfigLength>$(Configuration.Length)</ConfigLength>

                <!-- Path operations -->
                <OutputPath>$([System.IO.Path]::Combine('bin', '$(Configuration)'))</OutputPath>
                <IntermediateOutputPath>$([System.IO.Path]::Combine('obj', '$(Configuration)'))</IntermediateOutputPath>

                <!-- Environment variables -->
                <UserName>$([System.Environment]::GetEnvironmentVariable('USERNAME'))</UserName>
                <TempPath>$([System.Environment]::GetEnvironmentVariable('TEMP'))</TempPath>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />
              </ItemGroup>
            </Project>
            """;

        public const string ItemTransformsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <OutputPath>bin\$(Configuration)\</OutputPath>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs" />

                <!-- Item transforms -->
                <CompileWithoutExtension Include="@(Compile->'%(Filename)')" />
                <CompileFullPath Include="@(Compile->'%(FullPath)')" />
                <CompileDirectory Include="@(Compile->'%(RootDir)%(Directory)')" />

                <!-- Transform to output path -->
                <OutputFile Include="@(Compile->'$(OutputPath)%(Filename).dll')" />

                <!-- Multiple transforms -->
                <TransformedItem Include="@(Compile->'%(Filename).obj')" />
                <TransformedItem Include="@(Compile->'%(Filename).pdb')" />
              </ItemGroup>

              <ItemGroup>
                <!-- Filtered transforms -->
                <DebugCompile Include="@(Compile)" Condition="'$(Configuration)' == 'Debug'" />
                <ReleaseCompile Include="@(Compile)" Condition="'$(Configuration)' == 'Release'" />
              </ItemGroup>
            </Project>
            """;

        public const string ComplexFunctionsProjectXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <Configuration>Debug</Configuration>
                <Platform>AnyCPU</Platform>

                <!-- Complex nested functions -->
                <NormalizedPath>$([System.IO.Path]::GetFullPath($([System.IO.Path]::Combine('$(MSBuildProjectDirectory)', '..', 'shared'))))</NormalizedPath>

                <!-- String operations with conditions -->
                <DefineConstants Condition="'$(Configuration)' == 'Debug'">DEBUG;TRACE</DefineConstants>
                <DefineConstants Condition="'$(Configuration)' == 'Release'">TRACE</DefineConstants>
                <AllDefines>$(DefineConstants.Replace(';', ','))</AllDefines>

                <!-- Mathematical operations -->
                <MajorVersion>1</MajorVersion>
                <MinorVersion>2</MinorVersion>
                <BuildNumber>3</BuildNumber>
                <VersionString>$(MajorVersion).$(MinorVersion).$(BuildNumber)</VersionString>

                <!-- MSBuild intrinsic functions -->
                <ProjectRootElement>$([MSBuild]::GetDirectoryNameOfFileAbove('$(MSBuildProjectDirectory)', '.git'))</ProjectRootElement>
                <IsUnix>$([MSBuild]::IsOSPlatform('Linux'))</IsUnix>
                <IsWindows>$([MSBuild]::IsOSPlatform('Windows'))</IsWindows>

                <!-- Registry access (Windows only) -->
                <VisualStudioVersion Condition="'$(OS)' == 'Windows_NT'">$([MSBuild]::GetRegistryValueFromView('HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\SxS\VS7', '17.0', null, RegistryView.Registry32, RegistryView.Registry64))</VisualStudioVersion>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="*.cs">
                  <Link>$([System.IO.Path]::Combine('Generated', '%(Filename)%(Extension)'))</Link>
                  <DependentUpon Condition="Exists('%(Filename).xaml')">%(Filename).xaml</DependentUpon>
                </Compile>
              </ItemGroup>

              <ItemGroup>
                <!-- Item transforms -->
                <DistinctCompile Include="@(Compile->Distinct())" />
                <ReversedCompile Include="@(Compile->Reverse())" />
                <DirectoryList Include="@(Compile->'%(Directory)'->Distinct())" />
              </ItemGroup>

              <PropertyGroup>
                <!-- Item function that returns a scalar value must be a property -->
                <CompileCount>@(Compile->Count())</CompileCount>
              </PropertyGroup>
            </Project>
            """;
    }
}
