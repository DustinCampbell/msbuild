// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests.Shared;

namespace MSBuild.Benchmarks;

/// <summary>
///  Benchmarks for evaluating .NET SDK projects. This exercises the full evaluation
///  pipeline including all SDK imports, condition parsing/evaluation, property
///  expansion, and item globbing against realistic project configurations.
///  Requires a bootstrap build (build.cmd) so the SDK resolver can find its dependencies.
/// </summary>
[MemoryDiagnoser]
public class ProjectEvaluationBenchmark
{
    private string _projectDir = null!;
    private string _consoleAppPath = null!;
    private string _classLibPath = null!;
    private string _multiTargetPath = null!;
    private string? _previousMsBuildExePath;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _previousMsBuildExePath = Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH");
        SetupBootstrapEnvironment();

        _projectDir = Path.Combine(Path.GetTempPath(), "MSBuildBenchmarks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_projectDir);

        // Create a minimal source file so item globbing finds something.
        File.WriteAllText(Path.Combine(_projectDir, "Program.cs"),
            """
            Console.WriteLine("Hello, World!");
            """);

        // Console app — the most common project type.
        _consoleAppPath = Path.Combine(_projectDir, "ConsoleApp.csproj");
        File.WriteAllText(_consoleAppPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <OutputType>Exe</OutputType>
              </PropertyGroup>
            </Project>
            """);

        // Class library with common options.
        _classLibPath = Path.Combine(_projectDir, "ClassLib.csproj");
        File.WriteAllText(_classLibPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <GenerateDocumentationFile>true</GenerateDocumentationFile>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """);

        // Multi-targeting project — hits more condition branches in SDK targets.
        _multiTargetPath = Path.Combine(_projectDir, "MultiTarget.csproj");
        File.WriteAllText(_multiTargetPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0;net8.0;net472</TargetFrameworks>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", _previousMsBuildExePath);

        if (Directory.Exists(_projectDir))
        {
            Directory.Delete(_projectDir, recursive: true);
        }
    }

    /// <summary>
    ///  Evaluate a .NET SDK console app project.
    /// </summary>
    [Benchmark]
    public Project ConsoleApp()
        => Evaluate(_consoleAppPath);

    /// <summary>
    ///  Evaluate a .NET SDK class library project.
    /// </summary>
    [Benchmark]
    public Project ClassLibrary()
        => Evaluate(_classLibPath);

    /// <summary>
    ///  Evaluate a multi-targeting .NET SDK project (outer evaluation only).
    /// </summary>
    [Benchmark]
    public Project MultiTargeting()
        => Evaluate(_multiTargetPath);

    private static Project Evaluate(string projectPath)
    {
        using var collection = new ProjectCollection();
        return new Project(projectPath, globalProperties: null, toolsVersion: null, collection);
    }

    /// <summary>
    ///  Configures <c>MSBUILD_EXE_PATH</c> to point to the bootstrap MSBuild so that
    ///  the SDK resolver can find its dependencies. Uses the bootstrap location
    ///  from the shared test infrastructure.
    /// </summary>
    private static void SetupBootstrapEnvironment()
    {
        // Already configured (e.g., by the bootstrap .bat/.sh script).
        if (Environment.GetEnvironmentVariable("MSBUILD_EXE_PATH") is not null)
        {
            return;
        }

        string msbuildExePath = RunnerUtilities.PathToExecutable;

        if (!File.Exists(msbuildExePath))
        {
            throw new InvalidOperationException(
                $"Bootstrap MSBuild not found at '{msbuildExePath}'. Run 'build.cmd' (or 'build.sh') first.");
        }

        Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", msbuildExePath);
    }
}
