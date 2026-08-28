// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public sealed class WellKnownFunctions_Tests
{
    private const BindingFlags StaticFunctionFlags =
        BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Static | BindingFlags.InvokeMethod;

    [Fact]
    public void EveryProductionIntrinsicFunctionHasAWellKnownHandler()
    {
        foreach (MethodInfo method in typeof(IntrinsicFunctions).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (!method.Name.StartsWith("__", StringComparison.Ordinal))
            {
                WellKnownFunctions.IsIntrinsicFunctionHandled(method.Name).ShouldBeTrue(method.Name);
            }
        }
    }

    [Fact]
    public void StringConcatMatchesDefaultBinderForScalarArguments()
    {
        AssertStringConcatMatchesDefaultBinder("a");
        AssertStringConcatMatchesDefaultBinder("a", "b");
        AssertStringConcatMatchesDefaultBinder("a", "b", "c");
        AssertStringConcatMatchesDefaultBinder("a", "b", "c", "d");
        AssertStringConcatMatchesDefaultBinder("a", "b", "c", "d", "e");
        AssertStringConcatMatchesDefaultBinder(1L, "x");
        AssertStringConcatMatchesDefaultBinder("a", 2, true);
        AssertStringConcatMatchesDefaultBinder(null, "b");
        AssertStringConcatMatchesDefaultBinder(new NullStringValue());
    }

    [Fact]
    public void StringConcatMatchesDefaultBinderForCollectionArguments()
    {
        AssertStringConcatMatchesDefaultBinder((object)new string?[] { "a", null, "b" });
        AssertStringConcatMatchesDefaultBinder((object)new object?[] { "a", 1, null });
        AssertStringConcatMatchesDefaultBinder(new List<string?> { "a", null, "b" });

        // Type.DefaultBinder does not infer T for Concat<T>(IEnumerable<T>), so this must use the List's
        // parameterless ToString() rather than concatenating its elements.
        AssertStringConcatMatchesDefaultBinder(new List<int> { 1, 2 });
    }

    [Fact]
    public void StringConcatLeavesZeroArgumentCallToDefaultBinder()
    {
        AssertStringConcatFallsBackToDefaultBinder([]);
    }

    [Fact]
    public void StringConcatTreatsSingleNullAsEmpty()
    {
        // Type.DefaultBinder considers the untyped null ambiguous, but Concat(object?) defines null as empty.
        FunctionArguments arguments = CreateArguments([null]);

        bool handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void StringConcatDirectlyHandlesMixedParamsArguments()
    {
        // Type.DefaultBinder can select a ReadOnlySpan<object?> overload that reflection cannot invoke. The
        // well-known path applies the documented object-concatenation semantics directly instead.
        FunctionArguments arguments = CreateArguments(["a", 2, true, 4L]);

        bool handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe("a2True4");

        arguments = CreateArguments(["a", new NullStringValue(), "b", 4]);

        handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out result);

        handled.ShouldBeTrue();
        result.ShouldBe("ab4");
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a,b", "ab")]
    [InlineData("a,b,c", "abc")]
    [InlineData("a,b,c,d", "abcd")]
    [InlineData("a,b,c,d,e", "abcde")]
    public void StringConcatConsumesRawSegmentsWithoutMaterializing(string text, string expected)
    {
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        bool handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(expected);
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void StringConcatReturnsTheOnlyNonEmptyStringWithoutCopying()
    {
        string value = new string(['v', 'a', 'l', 'u', 'e']);
        string[][] argumentSets =
        [
            [string.Empty, value],
            [string.Empty, value, string.Empty],
            [string.Empty, string.Empty, value, string.Empty],
        ];

        foreach (string[] values in argumentSets)
        {
            FunctionArguments arguments = new(values);

            bool handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out object? result);

            handled.ShouldBeTrue();
            result.ShouldBeSameAs(value);
            arguments.IsMaterialized.ShouldBeFalse();
        }
    }

    [Fact]
    public void StringJoinConsumesRawSegmentsWithoutMaterializing()
    {
        FunctionArguments arguments = new(["-", "a", "b", "c"]);

        bool handled = WellKnownFunctions.TryExecuteStaticStringFunction(
            nameof(string.Join),
            arguments,
            out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe("a-b-c");
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void StringJoinLeavesCollectionArgumentsToDefaultBinder()
    {
        FunctionArguments arguments = CreateArguments([",", new[] { "a", "b" }]);

        bool handled = WellKnownFunctions.TryExecuteStaticStringFunction(
            nameof(string.Join),
            arguments,
            out object? result);

        handled.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void PathCombineConsumesRawSegments()
    {
        string root = Path.GetPathRoot(Environment.CurrentDirectory)!;
        string rootedPath = Path.Combine(root, "rooted");
        string[] values = ["ignored", rootedPath, "child", "file.txt"];
        FunctionArguments arguments = new(values);

        bool handled = WellKnownFunctions.TryExecutePathFunction(nameof(Path.Combine), out object? result, arguments);

        handled.ShouldBeTrue();
        result.ShouldBe(Path.Combine(values));
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void PathCombineLeavesNullArgumentsToDefaultBinder()
    {
        FunctionArguments arguments = CreateArguments(["root", null]);

        bool handled = WellKnownFunctions.TryExecutePathFunction(
            nameof(Path.Combine),
            out object? result,
            arguments);

        handled.ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData("GetExtension", @"directory\file.txt", ".txt")]
    [InlineData("GetFileName", @"directory\file.txt", "file.txt")]
    [InlineData("GetFileNameWithoutExtension", @"directory\file.txt", "file")]
    [InlineData("HasExtension", @"directory\file.txt", true)]
    [InlineData("HasExtension", @"directory\file", false)]
    public void PathFunctionsMatchSystemPath(string methodName, string path, object expected)
    {
        FunctionArguments arguments = new([path]);

        bool handled = WellKnownFunctions.TryExecutePathFunction(methodName, out object? result, arguments);

        handled.ShouldBeTrue();
        result.ShouldBe(expected);
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void VersionFunctionsConsumeRawSegments()
    {
        FunctionArguments arguments = new(["1.2.3.4"]);

        bool handled = WellKnownFunctions.TryExecuteStaticVersionFunction(
            nameof(Version.Parse),
            arguments,
            out object? result);

        handled.ShouldBeTrue();
        Version version = result.ShouldBeOfType<Version>();
        version.ShouldBe(new Version(1, 2, 3, 4));
        arguments.IsMaterialized.ShouldBeFalse();

        arguments = new(Array.Empty<string>());
        handled = WellKnownFunctions.TryExecuteVersionFunction(
            nameof(Version.Major),
            version,
            arguments,
            out result);

        handled.ShouldBeTrue();
        result.ShouldBe(1);
    }

    [Fact]
    public void ConvertToUInt32ConsumesRawSegments()
    {
        const string text = "prefix4294967295suffix";
        ArgumentList source = PropertyFunctionParser.ParseArguments(
            new StringSegment(text, 6, 10),
            text,
            MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        bool handled = WellKnownFunctions.TryExecuteConvertFunction(
            nameof(Convert.ToUInt32),
            arguments,
            out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(uint.MaxValue);
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void ConvertToUInt32LeavesInvalidTextToDefaultBinder()
    {
        FunctionArguments arguments = new(["4294967296"]);

        bool handled = WellKnownFunctions.TryExecuteConvertFunction(
            nameof(Convert.ToUInt32),
            arguments,
            out object? result);

        handled.ShouldBeFalse();
        result.ShouldBeNull();
    }

    private static void AssertStringConcatMatchesDefaultBinder(params object?[] values)
    {
        object? expected = InvokeStringConcatWithDefaultBinder((object?[])values.Clone());
        FunctionArguments arguments = CreateArguments(values);

        bool handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(expected);
    }

    private static void AssertStringConcatFallsBackToDefaultBinder(object?[] values)
    {
        FunctionArguments arguments = CreateArguments(values);

        bool handled = WellKnownFunctions.TryExecuteStringConcat(arguments, out object? result);

        handled.ShouldBeFalse();
        result.ShouldBeNull();
        Should.Throw<AmbiguousMatchException>(() => InvokeStringConcatWithDefaultBinder((object?[])values.Clone()));
    }

    private static object? InvokeStringConcatWithDefaultBinder(object?[] values)
        => typeof(string).InvokeMember(
            nameof(string.Concat),
            StaticFunctionFlags,
            Type.DefaultBinder,
            target: null,
            values,
            CultureInfo.InvariantCulture);

    private static FunctionArguments CreateArguments(object?[] values)
    {
        FunctionArguments arguments = new(new string[values.Length]);
        arguments.ConfigureMaterialization(new ValueMaterializer(values), materializeAllArguments: true);
        return arguments;
    }

    private sealed class ValueMaterializer(object?[] values) : IFunctionArgumentMaterializer
    {
        public object? Materialize(StringSegment source, int index, FunctionArgumentRequirements requirements)
            => values[index];
    }

    private sealed class NullStringValue
    {
        public override string? ToString() => null;
    }
}
