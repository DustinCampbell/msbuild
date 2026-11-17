// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;
using static Microsoft.Build.UnitTests.Evaluation.EvaluationTestHelpers;

namespace Microsoft.Build.UnitTests.Expansion;

/// <summary>
///  Tests arithmetic overload selection and filesystem property functions through expression expansion.
/// </summary>
/// <param name="output">The output helper for the test.</param>
[Trait("Category", "expansion")]
[UseInvariantCulture]
public sealed class PropertyFunctionTests(ITestOutputHelper output) : TestBase(output)
{
    /// <summary>
    ///  Resets change-wave state before evaluation and registers its cleanup.
    /// </summary>
    /// <returns>
    ///  A completed task after initialization.
    /// </returns>
    protected override ValueTask InitializeAsync()
    {
        RegisterCleanup(ChangeWaves.ResetStateForTests);
        ChangeWaves.ResetStateForTests();
        return default;
    }

    public static readonly TheoryData<string, long> IntegerArithmeticTests =
    [
        ("$([MSBuild]::Add($([System.Int64]::MaxValue), 1))", unchecked(long.MaxValue + 1)),
        ("$([MSBuild]::Subtract($([System.Int64]::MaxValue), 9223372036854775806))", 1L),
        ("$([MSBuild]::Multiply($([System.Int64]::MaxValue), 2))", unchecked(long.MaxValue * 2)),
        ("$([MSBuild]::Divide(10, 3))", 10L / 3),
        ("$([MSBuild]::Modulo(10, 3))", 1L),
    ];

    /// <summary>
    ///  Verifies integer arithmetic, including unchecked overflow, precision, and division truncation.
    /// </summary>
    /// <param name="expression">The arithmetic property function to expand.</param>
    /// <param name="expected">The expected integer result.</param>
    [Theory]
    [MemberData(nameof(IntegerArithmeticTests))]
    public void MSBuildIntegerArithmetic(string expression, long expected)
        => ExpandExpression(expression).ShouldBe(expected.ToString());

    public static readonly TheoryData<string, double> RealArithmeticTests =
    [
        ("$([MSBuild]::Add(9223372036854775808, 1))", long.MaxValue + 1D + 1),
        ("$([MSBuild]::Add(-9223372036854775809, 1))", long.MinValue - 1D + 1),
        ("$([MSBuild]::Add(1.0, 2.0))", 3.0),
        ("$([MSBuild]::Add(1.25, 2.5))", 3.75),
        ("$([MSBuild]::Subtract(9223372036854775808, 1))", long.MaxValue + 1D - 1),
        ("$([MSBuild]::Subtract(-9223372036854775809, 1))", long.MinValue - 1D - 1),
        ("$([MSBuild]::Subtract(2.0, 1.0))", 1.0),
        ("$([MSBuild]::Subtract(2.5, 1.25))", 1.25),
        ("$([MSBuild]::Multiply(9223372036854775808, 1))", (long.MaxValue + 1D) * 1),
        ("$([MSBuild]::Multiply(-9223372036854775809, 1))", (long.MinValue - 1D) * 1),
        ("$([MSBuild]::Multiply(2.0, 1.0))", 2.0),
        ("$([MSBuild]::Multiply(1.5, 2.5))", 3.75),
        ("$([MSBuild]::Divide(9223372036854775808, 1))", (long.MaxValue + 1D) / 1),
        ("$([MSBuild]::Divide(-9223372036854775809, 1))", (long.MinValue - 1D) / 1),
        ("$([MSBuild]::Divide(1, 0.5))", 2.0),
        ("$([MSBuild]::Modulo(9223372036854775808, 1))", (long.MaxValue + 1D) % 1),
        ("$([MSBuild]::Modulo(-9223372036854775809, 1))", (long.MinValue - 1D) % 1),
        ("$([MSBuild]::Modulo(11.0, 2.5))", 1.0),
        ("$([MSBuild]::Modulo(11.5, 2.5))", 1.5),
    ];

    /// <summary>
    ///  Verifies real arithmetic, including out-of-range integer fallback and fractional results.
    /// </summary>
    /// <param name="expression">The arithmetic property function to expand.</param>
    /// <param name="expected">The expected double result.</param>
    [Theory]
    [MemberData(nameof(RealArithmeticTests))]
    public void MSBuildRealArithmetic(string expression, double expected)
        => ExpandExpression(expression).ShouldBe(expected.ToString());

    /// <summary>
    ///  Exercises <see cref="IntrinsicFunctionOverload"/> to verify that late-bound arithmetic prefers
    ///  the long overload over the double overload.
    /// </summary>
    /// <param name="methodName">The intrinsic arithmetic operation to invoke.</param>
    /// <param name="expected">The expected result of applying the operation to 255 and 2 as longs.</param>
    [Theory]
    [InlineData("Add", 257L)]
    [InlineData("Subtract", 253L)]
    [InlineData("Multiply", 510L)]
    [InlineData("Divide", 127L)]
    [InlineData("Modulo", 1L)]
    public void MSBuildLateBoundArithmeticPrefersLong(string methodName, long expected)
    {
        Environment.SetAppContextSwitch("Microsoft.Build.EnableAllPropertyFunctions", true);

        // Byte bypasses the arithmetic fast path; the string argument requires late-bound coercion.
        string functionCall = $"[MSBuild]::{methodName}($([System.Byte]::MaxValue), '2')";

        ExpandExpression($"$({functionCall})").ShouldBe(expected.ToString());
        ExpandExpression($"$({functionCall}.GetType().FullName)").ShouldBe(typeof(long).FullName);
    }

    /// <summary>
    ///  Verifies that the intrinsic file-existence function finds an existing file.
    /// </summary>
    [Fact]
    public void FileExists_WhenFileExists_ReturnsTrue()
    {
        string testFilePath = Environment.CreateFile("TestFile.txt", "Test content").Path;

        ExpandExpression("$([MSBuild]::FileExists($(TestFilePath)))", Properties("TestFilePath", testFilePath))
            .ShouldBe("True");
    }

    /// <summary>
    ///  Verifies that the intrinsic file-existence function rejects a missing file.
    /// </summary>
    [Fact]
    public void FileExists_WhenFileDoesNotExist_ReturnsFalse()
    {
        string testFilePath = Path.Combine(Environment.DefaultTestDirectory.Path, "MissingFile.txt");

        ExpandExpression("$([MSBuild]::FileExists($(TestFilePath)))", Properties("TestFilePath", testFilePath))
            .ShouldBe("False");
    }

    /// <summary>
    ///  Verifies that the framework directory-existence function finds an existing directory.
    /// </summary>
    [Fact]
    public void SystemIODirectoryExists_WhenDirectoryExists_ReturnsTrue()
    {
        string testDirPath = Environment.CreateFolder().Path;

        ExpandExpression("$([System.IO.Directory]::Exists($(TestDirPath)))", Properties("TestDirPath", testDirPath))
            .ShouldBe("True");
    }

    /// <summary>
    ///  Verifies that the framework directory-existence function rejects a missing directory.
    /// </summary>
    [Fact]
    public void SystemIODirectoryExists_WhenDirectoryDoesNotExist_ReturnsFalse()
    {
        string testDirPath = Path.Combine(Environment.DefaultTestDirectory.Path, "MissingDirectory");

        ExpandExpression("$([System.IO.Directory]::Exists($(TestDirPath)))", Properties("TestDirPath", testDirPath))
            .ShouldBe("False");
    }

    /// <summary>
    ///  Verifies that the intrinsic directory-existence function finds an existing directory.
    /// </summary>
    [Fact]
    public void DirectoryExists_WhenDirectoryExists_ReturnsTrue()
    {
        string testDirPath = Environment.CreateFolder().Path;

        ExpandExpression("$([MSBuild]::DirectoryExists($(TestDirPath)))", Properties("TestDirPath", testDirPath))
            .ShouldBe("True");
    }

    /// <summary>
    ///  Verifies that the intrinsic directory-existence function rejects a missing directory.
    /// </summary>
    [Fact]
    public void DirectoryExists_WhenDirectoryDoesNotExists_ReturnsFalse()
    {
        string testDirPath = Path.Combine(Environment.DefaultTestDirectory.Path, "MissingDirectory");

        ExpandExpression("$([MSBuild]::DirectoryExists($(TestDirPath)))", Properties("TestDirPath", testDirPath))
            .ShouldBe("False");
    }

    /// <summary>
    ///  Verifies dispatch to the framework URI-escaping function.
    /// </summary>
    [Fact]
    public void SystemUriEscapeDataString()
        => ExpandExpression("$([System.Uri]::EscapeDataString($(TestInput)))", Properties("TestInput", "hello world & friends"))
            .ShouldBe(Uri.EscapeDataString("hello world & friends"));

    /// <summary>
    ///  Expands a property-function expression without creating or evaluating a project.
    /// </summary>
    /// <param name="expression">The expression to expand.</param>
    /// <param name="properties">The available properties, or <see langword="null"/> to use an empty provider.</param>
    /// <returns>
    ///  The expanded and unescaped value.
    /// </returns>
    private static string ExpandExpression(string expression, IPropertyProvider<ProjectPropertyInstance>? properties = null)
    {
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties ?? NoProperties, FileSystems.Default);
        return expander.ExpandIntoStringAndUnescape(expression, ExpanderOptions.ExpandProperties, ElementLocation.Empty);
    }
}
