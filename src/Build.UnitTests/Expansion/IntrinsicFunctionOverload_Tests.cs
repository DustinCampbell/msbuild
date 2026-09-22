// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
[UseInvariantCulture]
public class IntrinsicFunctionOverload_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [MemberData(nameof(ArithmeticExpressions))]
    public void ArithmeticOverloadsEvaluate(string expression, string expected)
    {
        using var env = TestEnvironment.Create(_output);
        ChangeWaves.ResetStateForTests();

        string content = $"""
            <Project>
                <PropertyGroup>
                    <Actual>{expression}</Actual>
                </PropertyGroup>
            </Project>
            """;

        using ProjectFromString project = new(content);
        project.Project.GetProperty("Actual").ShouldNotBeNull().EvaluatedValue
            .ShouldBe(expected);
    }

    public static TheoryData<string, string> ArithmeticExpressions => new()
    {
        { "$([MSBuild]::Add($([System.Int64]::MaxValue), 1))", unchecked(long.MaxValue + 1).ToResultString() },
        { "$([MSBuild]::Add(9223372036854775808, 1))", ((long.MaxValue + 1D) + 1).ToResultString() },
        { "$([MSBuild]::Add(-9223372036854775809, 1))", ((long.MinValue - 1D) + 1).ToResultString() },
        { "$([MSBuild]::Add(1.0, 2.0))", 3.0.ToResultString() },
        { "$([MSBuild]::Subtract($([System.Int64]::MaxValue), 9223372036854775806))", 1.ToResultString() },
        { "$([MSBuild]::Subtract(9223372036854775808, 1))", ((long.MaxValue + 1D) - 1).ToResultString() },
        { "$([MSBuild]::Subtract(-9223372036854775809, 1))", ((long.MinValue - 1D) - 1).ToResultString() },
        { "$([MSBuild]::Subtract(2.0, 1.0))", 1.0.ToResultString() },
        { "$([MSBuild]::Multiply($([System.Int64]::MaxValue), 2))", unchecked(long.MaxValue * 2).ToResultString() },
        { "$([MSBuild]::Multiply(9223372036854775808, 1))", ((long.MaxValue + 1D) * 1).ToResultString() },
        { "$([MSBuild]::Multiply(-9223372036854775809, 1))", ((long.MinValue - 1D) * 1).ToResultString() },
        { "$([MSBuild]::Multiply(2.0, 1.0))", 2.0.ToResultString() },
        { "$([MSBuild]::Divide(10, 3))", (10 / 3).ToResultString() },
        { "$([MSBuild]::Divide(9223372036854775808, 1))", ((long.MaxValue + 1D) / 1).ToResultString() },
        { "$([MSBuild]::Divide(-9223372036854775809, 1))", ((long.MinValue - 1D) / 1).ToResultString() },
        { "$([MSBuild]::Divide(1, 0.5))", 2.0.ToResultString() },
        { "$([MSBuild]::Modulo(10, 3))", 1.ToResultString() },
        { "$([MSBuild]::Modulo(9223372036854775808, 1))", ((long.MaxValue + 1D) % 1).ToResultString() },
        { "$([MSBuild]::Modulo(-9223372036854775809, 1))", ((long.MinValue - 1D) % 1).ToResultString() },
        { "$([MSBuild]::Modulo(11.0, 2.5))", 1.ToResultString() },
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileExists(bool exists)
    {
        using var env = TestEnvironment.Create(_output);
        string filePath = "NonExistentFile.txt";
        if (exists)
        {
            filePath = Path.Combine(env.DefaultTestDirectory.Path, "TestFile.txt");
            File.WriteAllText(filePath, "Test content");
            filePath = filePath.Replace(@"\", @"\\");
        }

        string content = $"""
            <Project>
                <PropertyGroup>
                    <TestFilePath>{filePath}</TestFilePath>
                    <FileExists>$([MSBuild]::FileExists($(TestFilePath)))</FileExists>
                </PropertyGroup>
            </Project>
            """;

        using ProjectFromString project = new(content);
        project.Project.GetProperty("FileExists").ShouldNotBeNull().EvaluatedValue
            .ShouldBe(exists.ToResultString());
    }

    [Theory]
    [MemberData(nameof(DirectoryExistsExpressions))]
    public void DirectoryExists(string expression, bool exists)
    {
        using var env = TestEnvironment.Create(_output);
        string directoryPath = "TestDir";
        if (exists)
        {
            directoryPath = Path.Combine(env.DefaultTestDirectory.Path, "TestDir");
            Directory.CreateDirectory(directoryPath);
            directoryPath = directoryPath.Replace(@"\", @"\\");
        }

        string content = $"""
            <Project>
                <PropertyGroup>
                    <TestDirPath>{directoryPath}</TestDirPath>
                    <DirExists>{expression}</DirExists>
                </PropertyGroup>
            </Project>
            """;

        using ProjectFromString project = new(content);
        project.Project.GetProperty("DirExists").ShouldNotBeNull().EvaluatedValue
            .ShouldBe(exists.ToResultString());
    }

    public static TheoryData<string, bool> DirectoryExistsExpressions => new()
    {
        { "$([System.IO.Directory]::Exists($(TestDirPath)))", true },
        { "$([System.IO.Directory]::Exists($(TestDirPath)))", false },
        { "$([MSBuild]::DirectoryExists($(TestDirPath)))", true },
        { "$([MSBuild]::DirectoryExists($(TestDirPath)))", false },
    };

    [Fact]
    public void SystemUriEscapeDataString()
    {
        using var env = TestEnvironment.Create(_output);

        const string Content = """
            <Project>
                <PropertyGroup>
                    <TestInput>hello world &amp; friends</TestInput>
                    <Escaped>$([System.Uri]::EscapeDataString($(TestInput)))</Escaped>
                </PropertyGroup>
            </Project>
            """;

        using ProjectFromString project = new(Content);
        project.Project.GetProperty("Escaped").ShouldNotBeNull().EvaluatedValue
            .ShouldBe(Uri.EscapeDataString("hello world & friends"));
    }
}
