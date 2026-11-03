// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Engine.UnitTests;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests.Evaluation;

[UseInvariantCulture]
public sealed class PropertyFunctionComparison_Tests(ITestOutputHelper output)
{
    private const ExpanderOptions ReflectionOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.UseReflectionForPropertyFunctions;

    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void StartingDirectoryIsEmptyWithoutSourceFile(string file)
    {
        ExpansionContext context = new(
            properties: null, ExpanderOptions.ExpandProperties, new MockElementLocation(file),
            new PropertiesUseTracker(null), FileSystems.Default);

        context.StartingDirectory.ShouldBe("");
    }

    [Fact]
    public void StartingDirectoryUsesSourceLocation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "project");
        ExpansionContext context = new(
            properties: null, ExpanderOptions.ExpandProperties, new MockElementLocation(Path.Combine(directory, "test.proj")),
            new PropertiesUseTracker(null), FileSystems.Default);

        context.StartingDirectory.ShouldBe(directory);
    }

    [Fact]
    public void StartingDirectoryIsNullForRoot()
    {
        string root = Path.GetPathRoot(Path.GetFullPath("test.proj")).ShouldNotBeNull();
        ExpansionContext context = new(
            properties: null, ExpanderOptions.ExpandProperties, new MockElementLocation(root),
            new PropertiesUseTracker(null), FileSystems.Default);

        context.StartingDirectory.ShouldBeNull();
    }

    [Fact]
    public void FunctionUsesTheSuppliedExpansionContext()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        Expander<ProjectPropertyInstance, ProjectItemInstance>.Function function =
            Expander<ProjectPropertyInstance, ProjectItemInstance>.Function.ExtractPropertyFunction(
                "[System.String]::Copy('$(Value)')", MockElementLocation.Instance, null);

        PropertyDictionary<ProjectPropertyInstance> firstProperties = new();
        firstProperties.Set(ProjectPropertyInstance.Create("Value", "first"));
        ExpansionContext firstContext = new(
            firstProperties, ExpanderOptions.ExpandProperties, MockElementLocation.Instance,
            new PropertiesUseTracker(null), FileSystems.Default);
        function.Execute(null, in firstContext).ShouldBe("first");

        PropertyDictionary<ProjectPropertyInstance> secondProperties = new();
        secondProperties.Set(ProjectPropertyInstance.Create("Value", "second"));
        ExpansionContext secondContext = new(
            secondProperties, ReflectionOptions, MockElementLocation.Instance,
            new PropertiesUseTracker(null), FileSystems.Default);
        function.Execute(null, in secondContext).ShouldBe("second");
    }

    public static TheoryData<string, object> StaticCallsData => new()
    {
        { "[System.String]::IsNullOrEmpty(null)", true },
        { "[System.String]::IsNullOrEmpty('null')", false },
        { "[System.String]::IsNullOrWhiteSpace(' ')", true },
        { "[System.String]::Copy('abc')", "abc" },
        { "[System.String]::Concat('a', 'b', 'c', 'd', 'e')", "abcde" },
        { "[System.String]::Format('value {0}', 'x')", "value x" },
        { "[System.String]::Equals('a', 'A', StringComparison.OrdinalIgnoreCase)", true },
        { "[System.Int32]::Parse('42')", 42 },
        { "[System.Int32]::MaxValue", int.MaxValue },
        { "[System.Convert]::ToInt32('42')", 42 },
        { "[System.Convert]::ToInt64('42')", 42L },
        { "[System.Convert]::ToDouble('1.25')", 1.25 },
        { "[System.Char]::IsDigit('7')", true },
        { "[System.Char]::IsDigit('a7', '1')", true },
        { "[System.IO.Path]::GetFileName('file.txt')", "file.txt" },
        { "[System.IO.Path]::GetFileNameWithoutExtension('file.txt')", "file" },
        { "[System.IO.Path]::IsPathRooted('file.txt')", false },
        { "[System.Text.RegularExpressions.Regex]::Replace('abc', 'b', 'X')", "aXc" },
        { "[System.Text.RegularExpressions.Regex]::IsMatch('abc', '^A', RegexOptions.IgnoreCase | RegexOptions.Multiline)", true },
        { "[MSBuild]::Add('1', '2')", 3L },
        { "[MSBuild]::Add('1.5', '2')", 3.5 },
        { "[MSBuild]::Subtract('5', '2')", 3L },
        { "[MSBuild]::Multiply('5', '2')", 10L },
        { "[MSBuild]::Divide('5', '2')", 2L },
        { "[MSBuild]::Modulo('5', '2')", 1L },
        { "[MSBuild]::BitwiseOr('1', '2')", 3 },
        { "[MSBuild]::ValueOrDefault('', 'fallback')", "fallback" },
        { "[MSBuild]::GetTargetFrameworkVersion('net8.0', '2')", "8.0" },
        { "[System.String]::new('x', '3')", "xxx" },
        { "[System.Version]::new('1', '2').Major", 1 },
        { "[System.DateTime]::new('2024', '1', '2').Year", 2024 },
        { "[System.Int32]::Parse('42').Equals('42')", true },
        { "[System.Version]::new('1.2').Equals($([System.Version]::new('1.2')))", true },
        { "[System.Version]::Parse('1.2.3.4').ToString('2')", "1.2" },
        { "[System.Int32]::Parse('255').ToString('X')", "FF" }
    };

    [Theory]
    [MemberData(nameof(StaticCallsData))]
    public void StaticCallsMatchReflection(string expression, object expected)
    {
        object? reflectionResult = InvokeFunction(expression, useReflection: true);
        AssertSameResult(reflectionResult, expected);
        AssertSameResult(InvokeFunction(expression), reflectionResult);
    }

    public static TheoryData<string, string, object> StringCallsData => new()
    {
        { "Text.ToUpperInvariant()", "abc", "ABC" },
        { "Text.ToLowerInvariant()", "ABC", "abc" },
        { "Text.Substring('1')", "abc", "bc" },
        { "Text.Substring('1', '1')", "abc", "b" },
        { "Text.Replace('b', 'x')", "abc", "axc" },
        { "Text.Contains('b')", "abc", true },
        { "Text.StartsWith('a')", "abc", true },
        { "Text.EndsWith('c')", "abc", true },
        { "Text.EndsWith('C', StringComparison.OrdinalIgnoreCase)", "abc", true },
        { "Text.IndexOf('B', System.StringComparison.OrdinalIgnoreCase)", "abc", 1 },
        { "Text.IndexOfAny('bc')", "abc", 1 },
        { "Text.LastIndexOfAny('ab')", "abc", 1 },
        { "Text.PadLeft('5', 'x')", "abc", "xxabc" },
        { "Text.PadRight('5', 'x')", "abc", "abcxx" },
        { "Text.TrimStart('ab')", "abc", "c" },
        { "Text.Length", "abc", 3 },
        { "Text.get_Length()", "abc", 3 },
        { "[1]", "abc", 'b' },
        { "Text.Equals('42')", "42", true },
        { "Text.CompareTo('43')", "42", -1 },
        { "Text.Equals($([System.Int32]::Parse('42')))", "42", true },
        { "Text.CompareTo($([System.Double]::Parse('43')))", "42", -1 },
        { "Text.Equals('%34%32')", "42", true },
        { "Text.CompareTo('%34%33')", "42", -1 },
        { "Text.Equals('a%3bb')", "a%3bb", true },
        { "Text.Equals(null)", "abc", false },
        { "Text.sUbStRiNg('1')", "abc", "bc" }
    };

    [Theory]
    [MemberData(nameof(StringCallsData))]
    public void StringCallsMatchReflection(string expression, string receiver, object expected)
    {
        object? reflectionResult = InvokeFunction(expression, receiver, useReflection: true);
        AssertSameResult(reflectionResult, expected);
        AssertSameResult(InvokeFunction(expression, receiver), reflectionResult);
    }

    [Theory]
    [InlineData("Text.Split('-')")]
    [InlineData("Text.Split($([System.String]::Copy('-').ToCharArray()))")]
    public void ArrayResultsMatchReflection(string expression)
    {
        object? reflectionResult = InvokeFunction(expression, "A-B-C", useReflection: true);
        reflectionResult.ShouldBeOfType<string[]>().ShouldBe(["A", "B", "C"]);
        AssertSameResult(InvokeFunction(expression, "A-B-C"), reflectionResult);
    }

    [Fact]
    public void CharacterArrayResultsMatchReflection()
    {
        object? reflectionResult = InvokeFunction("Text.ToCharArray()", "abc", useReflection: true);
        reflectionResult.ShouldBeOfType<char[]>().ShouldBe(['a', 'b', 'c']);
        AssertSameResult(InvokeFunction("Text.ToCharArray()", "abc"), reflectionResult);
    }

    [Theory]
    [InlineData("[1]")]
    [InlineData("Array.GetValue('1')")]
    public void ArrayIndexingMatchesReflection(string expression)
    {
        string[] receiver = ["a", "b", "c"];
        object? reflectionResult = InvokeFunction(expression, receiver, useReflection: true);
        reflectionResult.ShouldBe("b");
        AssertSameResult(InvokeFunction(expression, receiver), reflectionResult);
    }

    [Fact]
    public void PathResultsMatchReflection()
    {
        object? reflectionResult = InvokeFunction("[System.IO.Path]::Combine('parent', 'child')", useReflection: true);
        reflectionResult.ShouldBe(Path.Combine("parent", "child"));
        AssertSameResult(InvokeFunction("[System.IO.Path]::Combine('parent', 'child')"), reflectionResult);

        reflectionResult = InvokeFunction("[System.IO.Path]::DirectorySeparatorChar", useReflection: true);
        reflectionResult.ShouldBeOfType<char>().ShouldBe(Path.DirectorySeparatorChar);
        AssertSameResult(InvokeFunction("[System.IO.Path]::DirectorySeparatorChar"), reflectionResult);
    }

    [Theory]
    [InlineData("[System.Version]::Parse('1.2.3.4')", typeof(Version), "1.2.3.4")]
    [InlineData("[System.Version]::new('1.2.3.4')", typeof(Version), "1.2.3.4")]
    [InlineData("[System.Guid]::new('00112233-4455-6677-8899-aabbccddeeff')", typeof(Guid), "00112233-4455-6677-8899-aabbccddeeff")]
    [InlineData("[System.Globalization.CultureInfo]::GetCultureInfo('en-US')", typeof(CultureInfo), "en-US")]
    public void ObjectResultsMatchReflection(string expression, Type expectedType, string expectedText)
    {
        object? reflectionResult = InvokeFunction(expression, useReflection: true);
        reflectionResult.ShouldNotBeNull();
        reflectionResult.GetType().ShouldBe(expectedType);
        reflectionResult.ToString().ShouldBe(expectedText);
        AssertSameResult(InvokeFunction(expression), reflectionResult);
    }

    [Fact]
    public void NullResultsMatchReflection()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string variableName = $"MSBuild_PropertyFunctionComparison_{Guid.NewGuid().ToString("N")}";
        env.SetEnvironmentVariable(variableName, null);
        string expression = $"[System.Environment]::GetEnvironmentVariable('{variableName}')";

        InvokeFunction(expression, useReflection: true).ShouldBeNull();
        InvokeFunction(expression).ShouldBeNull();

        PropertyDictionary<ProjectPropertyInstance> properties = new();
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, FileSystems.Default);
        ExpanderOptions[] expansionOptions = [ExpanderOptions.ExpandProperties, ReflectionOptions];
        foreach (ExpanderOptions options in expansionOptions)
        {
            expander.ExpandIntoStringLeaveEscaped($"$({expression})", options, MockElementLocation.Instance).ShouldBe("");
            expander.ExpandIntoStringLeaveEscaped($"prefix_$({expression})", options, MockElementLocation.Instance).ShouldBe("prefix_");
        }
    }

    [Theory]
    [InlineData("[System.String]::Copy('a;b')", "a%3bb")]
    [InlineData("[MSBuild]::Escape('a;b')", "a%3bb")]
    [InlineData("[MSBuild]::Unescape('a%3bb')", "a;b")]
    [InlineData("[MSBuild]::ConvertFromBase64('YTti')", "a;b")]
    public void EscapingMatchesReflection(string expression, string expected)
    {
        object? reflectionResult = InvokeFunction(expression, useReflection: true);
        reflectionResult.ShouldBe(expected);
        AssertSameResult(InvokeFunction(expression), reflectionResult);
    }

    [Theory]
    [InlineData("Probe.Select($([System.Int32]::Parse('42')))", "Int32", typeof(int))]
    [InlineData("Probe.Select('value $([System.Int32]::Parse('42'))')", "String", typeof(string))]
    [InlineData("Probe.Select('42')", "String", typeof(string))]
    public void BindingPreservesWholeExpressionArgumentTypes(string expression, string expected, Type argumentType)
    {
        BindingProbe current = new();
        BindingProbe reflection = new();

        InvokeFunction(expression, reflection, useReflection: true).ShouldBe(expected);
        InvokeFunction(expression, current).ShouldBe(expected);
        reflection.LastArgument.ShouldNotBeNull();
        reflection.LastArgument.GetType().ShouldBe(argumentType);
        AssertSameResult(current.LastArgument, reflection.LastArgument);
        current.CallCount.ShouldBe(1);
        reflection.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("Probe.AcceptInt32('42')", 42)]
    [InlineData("Probe.AcceptInt32($([System.Int32]::Parse('42')))", 42)]
    [InlineData("Probe.AcceptInt32(null)", 0)]
    public void BindingInvokesOnceWithConvertedArguments(string expression, int expected)
    {
        BindingProbe current = new();
        BindingProbe reflection = new();

        InvokeFunction(expression, reflection, useReflection: true).ShouldBe(expected);
        InvokeFunction(expression, current).ShouldBe(expected);
        reflection.LastArgument.ShouldBeOfType<int>().ShouldBe(expected);
        AssertSameResult(current.LastArgument, reflection.LastArgument);
        current.CallCount.ShouldBe(1);
        reflection.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("Probe.AcceptInt32('not a number')")]
    [InlineData("Probe.AcceptInt32('2147483648')")]
    [InlineData("Probe.AcceptInt32('1', '2')")]
    public void FailedBindingDoesNotInvokeTarget(string expression)
    {
        BindingProbe current = new();
        BindingProbe reflection = new();

        InvalidProjectFileException expected = Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(expression, reflection, useReflection: true));
        InvalidProjectFileException actual = Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, current));

        AssertSameError(actual, expected);
        current.CallCount.ShouldBe(0);
        reflection.CallCount.ShouldBe(0);
    }

    [Fact]
    public void TargetExceptionDoesNotRetryInvocation()
    {
        const string expression = "Probe.Throw('42')";
        BindingProbe current = new();
        BindingProbe reflection = new();

        InvalidProjectFileException expected = Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(expression, reflection, useReflection: true));
        InvalidProjectFileException actual = Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, current));

        AssertSameError(actual, expected);
        actual.BaseMessage.ShouldContain("probe failure");
        current.CallCount.ShouldBe(1);
        reflection.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("Text.Substring('-1')", "abc")]
    [InlineData("Text.Substring('not a number')", "abc")]
    [InlineData("Text.MissingMethod()", "abc")]
    [InlineData("[System.Convert]::ToInt32('not a number')", null)]
    [InlineData("[System.String]::Copy(null)", null)]
    [InlineData("[System.String]::IsNullOrEmpty('a', 'b')", null)]
    [InlineData("[System.Version]::new('-1', '2')", null)]
    public void ErrorsMatchReflection(string expression, string? receiver)
    {
        InvalidProjectFileException expected = Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(expression, receiver, useReflection: true));
        InvalidProjectFileException actual = Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, receiver));

        AssertSameError(actual, expected);
    }

    [Fact]
    public void LeavePropertiesUnexpandedOnErrorMatchesReflection()
    {
        const string expression = "Text.Substring('-1')";
        object? reflectionResult = InvokeFunction(
            expression, "abc", useReflection: true, additionalOptions: ExpanderOptions.LeavePropertiesUnexpandedOnError);
        reflectionResult.ShouldBeOfType<string>().ShouldContain("Substring");
        AssertSameResult(
            InvokeFunction(expression, "abc", additionalOptions: ExpanderOptions.LeavePropertiesUnexpandedOnError), reflectionResult);
    }

    [Theory]
    [InlineData("[System.String]::new()", null, "[System.String]::new()", true)]
    [InlineData("[System.Int32]::Parse('bad')", null, "[System.Int32]::Parse(bad)", false)]
    [InlineData("[System.String]::Copy(null)", null, "[System.String]::Copy(null)", false)]
    [InlineData("Text.Substring('-1')", "abc", "\"abc\".Substring(-1)", false)]
    [InlineData("Text.Replace('', 'x')", "abc", "\"abc\".Replace('', x)", false)]
    [InlineData("Text.IndexOf('abc', '1', '-1')", "abcdef", "\"abcdef\".IndexOf(abc, 1, -1)", false)]
    public void DiagnosticExpressionsPreserveArgumentFormatting(string expression, string? receiver, string expected, bool useReflection)
    {
        InvokeFunction(
            expression, receiver, useReflection, additionalOptions: ExpanderOptions.LeavePropertiesUnexpandedOnError).ShouldBe(expected);
    }

    [Fact]
    public void NonStringDiagnosticReceiversRemainEmptyQuotedStrings()
    {
        InvokeFunction(
            "Version.ToString('99')", new Version(1, 2),
            additionalOptions: ExpanderOptions.LeavePropertiesUnexpandedOnError).ShouldBe("\"\".ToString(99)");
    }

    [Fact]
    public void PropertyDiagnosticsDoNotIncludeParentheses()
    {
        InvalidProjectFileException error = Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction("Text.MissingProperty", "abc"));

        error.BaseMessage.ShouldContain("\"abc\".MissingProperty");
        error.BaseMessage.ShouldNotContain("MissingProperty(");
    }

    [Theory]
    [InlineData("Probe.TryAccept('42', out _)", true, 2)]
    [InlineData("Probe.tryaccept('42', out _)", null, 0)]
    public void OutArgumentProbingPreservesExistingCallCounts(string expression, bool? expected, int callCount)
    {
        BindingProbe current = new();
        BindingProbe reflection = new();

        AssertSameResult(InvokeFunction(expression, reflection, useReflection: true), expected);
        AssertSameResult(InvokeFunction(expression, current), expected);
        current.CallCount.ShouldBe(callCount);
        reflection.CallCount.ShouldBe(callCount);
    }

    [Fact]
    public void EmptyStringConstructorIsAnMSBuildOnlyOperation()
    {
        const string expression = "[System.String]::new()";
        InvokeFunction(expression).ShouldBe("");
        Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, useReflection: true));
        InvokeFunction(expression).ShouldBe("");
    }

    [Fact]
    public void ReflectionOptionAppliesToNestedArguments()
    {
        const string expression = "[System.String]::Concat($([System.String]::new()), 'tail')";
        InvokeFunction(expression).ShouldBe("tail");
        Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, useReflection: true));
    }

    [Fact]
    public void ReflectionOptionAppliesToChainedCalls()
    {
        const string expression = "[System.String]::Copy('abc').Length()";
        InvokeFunction(expression).ShouldBe(3);
        Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, useReflection: true));
    }

    [Fact]
    public void SimpleEnumNamesDifferFromReflection()
    {
        const string expression = "Text.EndsWith('C', 'OrdinalIgnoreCase')";
        InvokeFunction(expression, "abc").ShouldBe(true);
        Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, "abc", useReflection: true));
    }

    [Theory]
    [InlineData("Min", 1)]
    [InlineData("Max", 2)]
    public void MathFastPathsReturnDoubleForTypedIntegerArguments(string method, int expected)
    {
        string expression = $"[System.Math]::{method}($([System.Int32]::Parse('1')), $([System.Int32]::Parse('2')))";
        InvokeFunction(expression).ShouldBeOfType<double>().ShouldBe(expected);
        InvokeFunction(expression, useReflection: true).ShouldBeOfType<int>().ShouldBe(expected);
    }

    [Fact]
    public void ArithmeticFastPathsPreferLongForWholeDoubleArguments()
    {
        const string expression = "[MSBuild]::Add($([System.Double]::Parse('1')), $([System.Double]::Parse('2')))";
        InvokeFunction(expression).ShouldBeOfType<long>().ShouldBe(3L);
        InvokeFunction(expression, useReflection: true).ShouldBeOfType<double>().ShouldBe(3.0);
    }

    [Fact]
    public void OmittedOptionalArgumentDiffersFromReflection()
    {
        const string expression = "[MSBuild]::GetTargetFrameworkVersion('net8.0')";
        InvokeFunction(expression).ShouldBe("8.0");
        Should.Throw<InvalidProjectFileException>(() => InvokeFunction(expression, useReflection: true));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("tr-TR")]
    public void StringCallsUseTheCurrentCulture(string cultureName)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetCurrentCulture(cultureName);
        object? reflectionResult = InvokeFunction("Text.ToLower()", "iI", useReflection: true);
        reflectionResult.ShouldBe("iI".ToLower());
        AssertSameResult(InvokeFunction("Text.ToLower()", "iI"), reflectionResult);
    }

    [Fact]
    public void ConversionFunctionsUseTheCurrentCulture()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetCurrentCulture("fr-FR");
        const string expression = "[System.Convert]::ToDouble('1,25')";

        InvokeFunction(expression).ShouldBeOfType<double>().ShouldBe(1.25);
        InvokeFunction(expression, useReflection: true).ShouldBeOfType<double>().ShouldBe(1.25);
    }

    [Fact]
    public void BindingArgumentConversionUsesInvariantCulture()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        env.SetCurrentCulture("fr-FR");
        const string expression = "Probe.AcceptDouble('1.25')";
        BindingProbe current = new();
        BindingProbe reflection = new();

        InvokeFunction(expression, reflection, useReflection: true).ShouldBeOfType<double>().ShouldBe(1.25);
        InvokeFunction(expression, current).ShouldBeOfType<double>().ShouldBe(1.25);
        AssertSameResult(current.LastArgument, reflection.LastArgument);
        current.CallCount.ShouldBe(1);
        reflection.CallCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileSearchNeedsTheFileSystemArgument(bool fileExists)
    {
        string directory = Path.GetFullPath("PropertyFunctionComparison");
        string child = Path.Combine(directory, "child");
        TestFileSystem fileSystem = new(fileExists ? [Path.Combine(directory, "marker.props")] : []);
        PropertyDictionary<ProjectPropertyInstance> properties = new();
        properties.Set(ProjectPropertyInstance.Create("StartingDirectory", child));
        const string expression = "[MSBuild]::GetDirectoryNameOfFileAbove('$(StartingDirectory)', 'marker.props')";

        string result = InvokeFunction(expression, properties: properties, fileSystem: fileSystem).ShouldBeOfType<string>();
        EscapingUtilities.UnescapeAll(result).ShouldBe(fileExists ? directory : "");
        Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(expression, useReflection: true, properties: properties, fileSystem: fileSystem));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FileSearchCanUseTheSourceLocation(bool fileExists)
    {
        string directory = Path.GetFullPath("PropertyFunctionComparison");
        string marker = Path.Combine(directory, "marker.props");
        TestFileSystem fileSystem = new(fileExists ? [marker] : []);
        MockElementLocation location = new(Path.Combine(directory, "child", "test.proj"));
        const string expression = "[MSBuild]::GetPathOfFileAbove('marker.props')";

        string result = InvokeFunction(expression, fileSystem: fileSystem, location: location).ShouldBeOfType<string>();
        EscapingUtilities.UnescapeAll(result).ShouldBe(fileExists ? marker : "");
        Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(expression, useReflection: true, fileSystem: fileSystem, location: location));
    }

    [Theory]
    [InlineData("GetPathOfFileAbove", true)]
    [InlineData("getpathoffileabove", false)]
    public void ImplicitFileSearchPreservesCasingAndArguments(string methodName, bool expectedHandled)
    {
        string directory = Path.GetFullPath("PropertyFunctionComparison");
        string marker = Path.Combine(directory, "marker.props");
        MockElementLocation location = new(Path.Combine(directory, "child", "test.proj"));
        ExpansionContext context = new(
            properties: null, ExpanderOptions.ExpandProperties, location, new PropertiesUseTracker(null), new TestFileSystem(marker));
        object[] arguments = ["marker.props"];

        WellKnownFunctions.TryExecuteIntrinsicFunction(methodName, out object? result, in context, arguments).ShouldBe(expectedHandled);

        result.ShouldBe(expectedHandled ? marker : null);
        arguments.Length.ShouldBe(1);
        arguments[0].ShouldBe("marker.props");
    }

    [Fact]
    public void ImplicitFileSearchRemainsUnhandledForRootLocation()
    {
        string root = Path.GetPathRoot(Path.GetFullPath("test.proj")).ShouldNotBeNull();
        ExpansionContext context = new(
            properties: null, ExpanderOptions.ExpandProperties, new MockElementLocation(root),
            new PropertiesUseTracker(null), new TestFileSystem());
        object[] arguments = ["marker.props"];

        WellKnownFunctions.TryExecuteIntrinsicFunction("GetPathOfFileAbove", out _, in context, arguments).ShouldBeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImplicitFileSearchPreservesDiagnosticArguments(bool inMemory)
    {
        string directory = inMemory ? "" : Path.GetFullPath("PropertyFunctionComparison");
        MockElementLocation location = new(inMemory ? "" : Path.Combine(directory, "test.proj"));
        string fileWithPath = Path.Combine("nested", "marker.props");
        PropertyDictionary<ProjectPropertyInstance> properties = new();
        properties.Set(ProjectPropertyInstance.Create("FileName", EscapingUtilities.Escape(fileWithPath)));
        properties.Set(ProjectPropertyInstance.Create("StartingDirectory", EscapingUtilities.Escape(directory)));
        const string implicitExpression = "[MSBuild]::GetPathOfFileAbove('$(FileName)')";
        const string explicitExpression = "[MSBuild]::GetPathOfFileAbove('$(FileName)', '$(StartingDirectory)')";

        InvalidProjectFileException expected = Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(explicitExpression, properties: properties, location: location));
        InvalidProjectFileException actual = Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(implicitExpression, properties: properties, location: location));

        AssertSameError(actual, expected);

        string diagnosticDirectory = inMemory ? "''" : directory;
        string evaluatedExpression = $"[MSBuild]::GetPathOfFileAbove({fileWithPath}, {diagnosticDirectory})";
        actual.BaseMessage.ShouldContain(evaluatedExpression);
        InvokeFunction(
            implicitExpression, properties: properties, location: location,
            additionalOptions: ExpanderOptions.LeavePropertiesUnexpandedOnError).ShouldBe(evaluatedExpression);
    }

    [Fact]
    public void BuildCheckRegistrationUsesProjectAndLoggingContext()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        var directory = env.CreateFolder();
        var assembly = env.CreateFile(directory, fileName: "check.dll");
        string projectPath = Path.Combine(directory.Path, "test.proj");
        PropertyDictionary<ProjectPropertyInstance> properties = new();
        properties.Set(ProjectPropertyInstance.Create("MSBuildProjectFullPath", projectPath, mayBeReserved: true));
        properties.Set(ProjectPropertyInstance.Create("CheckAssembly", assembly.Path));
        MockLogger logger = new(_output);
        ILoggingService loggingService = LoggingService.CreateLoggingService(LoggerMode.Synchronous, 1);
        loggingService.RegisterLogger(logger);
        MockLoggingContext loggingContext = new(
            loggingService, new BuildEventContext(0, 0, BuildEventContext.InvalidProjectContextId, 0, 0));
        const string expression = "[MSBuild]::RegisterBuildCheck('$(CheckAssembly)')";

        InvokeFunction(expression, properties: properties, loggingContext: loggingContext).ShouldBe(true);
        logger.AllBuildEvents.Count.ShouldBe(1);
        BuildCheckAcquisitionEventArgs acquisition = logger.AllBuildEvents[0].ShouldBeOfType<BuildCheckAcquisitionEventArgs>();
        acquisition.ProjectPath.ShouldBe(projectPath);
        acquisition.AcquisitionPath.ShouldBe(assembly.Path);

        Should.Throw<InvalidProjectFileException>(
            () => InvokeFunction(expression, useReflection: true, properties: properties, loggingContext: loggingContext));
        logger.AllBuildEvents.Count.ShouldBe(1);
    }

    private object? InvokeFunction(
        string expression,
        object? receiver = null,
        bool useReflection = false,
        PropertyDictionary<ProjectPropertyInstance>? properties = null,
        IFileSystem? fileSystem = null,
        IElementLocation? location = null,
        LoggingContext? loggingContext = null,
        ExpanderOptions additionalOptions = ExpanderOptions.Invalid)
    {
        _output.WriteLine("{0}: {1}", useReflection ? "Reflection" : "Current", expression);
        properties ??= new PropertyDictionary<ProjectPropertyInstance>();
        fileSystem ??= FileSystems.Default;
        location ??= MockElementLocation.Instance;
        Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new(properties, fileSystem, loggingContext);
        Expander<ProjectPropertyInstance, ProjectItemInstance>.Function function =
            Expander<ProjectPropertyInstance, ProjectItemInstance>.Function.ExtractPropertyFunction(
                expression, location, receiver);
        function.ShouldNotBeNull();

        ExpanderOptions options = (useReflection ? ReflectionOptions : ExpanderOptions.ExpandProperties) | additionalOptions;
        ExpansionContext expansionContext = new(properties, options, location, expander.PropertiesUseTracker, fileSystem);
        return function.Execute(receiver, in expansionContext);
    }

    private static void AssertSameResult(object? actual, object? expected)
    {
        (actual?.GetType()).ShouldBe(expected?.GetType());
        if (expected is Array expectedArray)
        {
            Array? actualArray = actual.ShouldBeAssignableTo<Array>();
            actualArray.ShouldNotBeNull();
            actualArray.Rank.ShouldBe(expectedArray.Rank);
            for (int dimension = 0; dimension < expectedArray.Rank; dimension++)
            {
                actualArray.GetLength(dimension).ShouldBe(expectedArray.GetLength(dimension));
                actualArray.GetLowerBound(dimension).ShouldBe(expectedArray.GetLowerBound(dimension));
            }

            IEnumerator values = actualArray.GetEnumerator();
            foreach (object? expectedValue in expectedArray)
            {
                values.MoveNext().ShouldBeTrue();
                AssertSameResult(values.Current, expectedValue);
            }

            values.MoveNext().ShouldBeFalse();
        }
        else
        {
            actual.ShouldBe(expected);
        }
    }

    private static void AssertSameError(InvalidProjectFileException actual, InvalidProjectFileException expected)
    {
        actual.ErrorCode.ShouldBe(expected.ErrorCode);
        actual.BaseMessage.ShouldBe(expected.BaseMessage);
        actual.ProjectFile.ShouldBe(expected.ProjectFile);
        actual.LineNumber.ShouldBe(expected.LineNumber);
        actual.ColumnNumber.ShouldBe(expected.ColumnNumber);
        actual.EndLineNumber.ShouldBe(expected.EndLineNumber);
        actual.EndColumnNumber.ShouldBe(expected.EndColumnNumber);
        actual.HelpKeyword.ShouldBe(expected.HelpKeyword);
    }

    private sealed class TestFileSystem(params IEnumerable<string> files) : IFileSystem
    {
        private readonly HashSet<string> _files = new(files, StringComparer.Ordinal);

        public bool FileExists(string path)
            => _files.Contains(path);

        public TextReader ReadFile(string path)
            => throw new NotSupportedException();

        public Stream GetFileStream(string path, FileMode mode, System.IO.FileAccess access, FileShare share)
            => throw new NotSupportedException();

        public string ReadFileAllText(string path)
            => throw new NotSupportedException();

        public byte[] ReadFileAllBytes(string path)
            => throw new NotSupportedException();

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
            => throw new NotSupportedException();

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
            => throw new NotSupportedException();

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
            => throw new NotSupportedException();

        public FileAttributes GetAttributes(string path)
            => throw new NotSupportedException();

        public DateTime GetLastWriteTimeUtc(string path)
            => throw new NotSupportedException();

        public bool DirectoryExists(string path)
            => throw new NotSupportedException();

        public bool FileOrDirectoryExists(string path)
            => throw new NotSupportedException();
    }

    public sealed class BindingProbe
    {
        public int CallCount { get; private set; }

        public object? LastArgument { get; private set; }

        public string Select(string value)
        {
            CallCount++;
            LastArgument = value;
            return nameof(String);
        }

        public string Select(int value)
        {
            CallCount++;
            LastArgument = value;
            return nameof(Int32);
        }

        public int AcceptInt32(int value)
        {
            CallCount++;
            LastArgument = value;
            return value;
        }

        public int Throw(int value)
        {
            CallCount++;
            LastArgument = value;
            throw new InvalidOperationException("probe failure");
        }

        public double AcceptDouble(double value)
        {
            CallCount++;
            LastArgument = value;
            return value;
        }

        public bool TryAccept(string value, out int result)
        {
            CallCount++;
            return int.TryParse(value, out result);
        }

        public bool TryAccept(object value, out int result)
        {
            CallCount++;
            return int.TryParse(value.ToString(), out result);
        }
    }
}
