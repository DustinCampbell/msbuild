// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation;

public class FunctionArguments_Tests
{
    [Fact]
    public void RawSegmentsRemainUnmaterialized()
    {
        const string text = "42, value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        arguments.IsMaterialized.ShouldBeFalse();
        arguments.Count.ShouldBe(2);
        arguments.TryGetArgs(out int number, out StringSegment value).ShouldBeTrue();
        number.ShouldBe(42);
        value.Value.ShouldBe("value");
        value.Buffer.ShouldBeSameAs(text);
        arguments.IsMaterialized.ShouldBeFalse();
    }

#if NET
    [Fact]
    public void ReadingRawSegmentsAllocatesNoMemory()
    {
        const string text = "42, value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        for (int i = 0; i < 100; i++)
        {
            arguments.TryGetArgs(out int _, out StringSegment _).ShouldBeTrue();
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        int checksum = 0;
        bool allSucceeded = true;
        for (int i = 0; i < 1_000; i++)
        {
            allSucceeded &= arguments.TryGetArgs(out int number, out StringSegment value);
            checksum += number + value.Length;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allSucceeded.ShouldBeTrue();
        checksum.ShouldBeGreaterThan(0);
        allocated.ShouldBe(0);
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void ReadingDoubleArgumentsAllocatesNoMemory()
    {
        const string text = "1.5, 2.5";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        for (int i = 0; i < 100; i++)
        {
            arguments.TryGetArgs(out double _, out double _).ShouldBeTrue();
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        double checksum = 0;
        bool allSucceeded = true;
        for (int i = 0; i < 1_000; i++)
        {
            allSucceeded &= arguments.TryGetArgs(out double first, out double second);
            checksum += first + second;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allSucceeded.ShouldBeTrue();
        checksum.ShouldBeGreaterThan(0);
        allocated.ShouldBe(0);
        arguments.IsMaterialized.ShouldBeFalse();
    }
#endif

    [Theory]
    [InlineData("value", FunctionArgumentRequirements.None)]
    [InlineData("$(Value)", FunctionArgumentRequirements.ExpandProperties)]
    [InlineData("%(Identity)", FunctionArgumentRequirements.None)]
    [InlineData("%28", FunctionArgumentRequirements.Unescape)]
    [InlineData("%ZZ", FunctionArgumentRequirements.None)]
    [InlineData("%24(Value)", FunctionArgumentRequirements.Unescape)]
    [InlineData("$(Value)%28", FunctionArgumentRequirements.ExpandProperties | FunctionArgumentRequirements.Unescape)]
    internal void DetectsMaterializationRequirements(string text, FunctionArgumentRequirements expected)
    {
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        FunctionArguments stringArguments = new([text]);

        source.GetRequirements(0).ShouldBe(expected);
        arguments.ContainsMaterializationRequirement().ShouldBe(expected != FunctionArgumentRequirements.None);
        stringArguments.ContainsMaterializationRequirement().ShouldBe(expected != FunctionArgumentRequirements.None);
    }

    [Fact]
    public void SourceStringsRemainUnmaterialized()
    {
        FunctionArguments arguments = new(["42", "value"]);

        arguments.IsMaterialized.ShouldBeFalse();
        arguments.TryGetArgs(out int number, out StringSegment value).ShouldBeTrue();
        number.ShouldBe(42);
        value.Value.ShouldBe("value");
    }

    [Theory]
    [InlineData("CurrentCulture", StringComparison.CurrentCulture)]
    [InlineData("CurrentCultureIgnoreCase", StringComparison.CurrentCultureIgnoreCase)]
    [InlineData("InvariantCulture", StringComparison.InvariantCulture)]
    [InlineData("InvariantCultureIgnoreCase", StringComparison.InvariantCultureIgnoreCase)]
    [InlineData("Ordinal", StringComparison.Ordinal)]
    [InlineData("OrdinalIgnoreCase", StringComparison.OrdinalIgnoreCase)]
    [InlineData("StringComparison.Ordinal", StringComparison.Ordinal)]
    [InlineData("StringComparison.4", StringComparison.Ordinal)]
    [InlineData("System.StringComparison.OrdinalIgnoreCase", StringComparison.OrdinalIgnoreCase)]
    [InlineData("StringComparison. Ordinal", StringComparison.Ordinal)]
    public void ParsesStringComparisonWithoutMaterializing(string text, StringComparison expected)
    {
        string argumentsText = $"value, {text}";
        ArgumentList source = PropertyFunctionParser.ParseArguments(argumentsText, argumentsText, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        arguments.TryGetArgs(out StringSegment _, out StringComparison result).ShouldBeTrue();

        result.ShouldBe(expected);
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Theory]
    [InlineData("4")]
    [InlineData("ordinal")]
    [InlineData("StringComparison.ordinal")]
    [InlineData("NotAStringComparison")]
    public void RejectsInvalidStringComparison(string text)
    {
        string argumentsText = $"value, {text}";
        ArgumentList source = PropertyFunctionParser.ParseArguments(argumentsText, argumentsText, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        arguments.TryGetArgs(out StringSegment _, out StringComparison _).ShouldBeFalse();

        arguments.IsMaterialized.ShouldBeFalse();
    }

#if NET
    [Fact]
    public void ReadingStringComparisonAllocatesNoMemory()
    {
        const string text = "value, System.StringComparison.OrdinalIgnoreCase";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        for (int i = 0; i < 100; i++)
        {
            arguments.TryGetArgs(out StringSegment _, out StringComparison _).ShouldBeTrue();
        }

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        bool allSucceeded = true;
        int checksum = 0;
        for (int i = 0; i < 1_000; i++)
        {
            allSucceeded &= arguments.TryGetArgs(out StringSegment value, out StringComparison comparison);
            checksum += value.Length + (int)comparison;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        allSucceeded.ShouldBeTrue();
        checksum.ShouldBeGreaterThan(0);
        allocated.ShouldBe(0);
        arguments.IsMaterialized.ShouldBeFalse();
    }
#endif

    [Fact]
    public void WellKnownStringFunctionConsumesRawSegment()
    {
        const string text = "value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        bool handled = WellKnownFunctions.TryExecuteStringFunction(
            nameof(string.Contains),
            "prefix-value-suffix",
            arguments,
            out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(true);
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void RawValuesMaterializeOnlyWhenRequested()
    {
        const string text = "null, '', value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        object?[] values = arguments.ToObjectArray();

        values.Length.ShouldBe(3);
        values[0].ShouldBeNull();
        values[1].ShouldBe(string.Empty);
        values[2].ShouldBe("value");
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Fact]
    public void MaterializesOnlyAccessedArguments()
    {
        const string text = "$(First), $(Second)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(index => $"expanded-{index}");

        arguments.ConfigureMaterialization(materializer, materializeAllArguments: false);

        arguments[0].ShouldBe("expanded-0");
        materializer.Indices.ShouldBe([0]);
        arguments.IsMaterialized.ShouldBeFalse();

        arguments[0].ShouldBe("expanded-0");
        materializer.Indices.ShouldBe([0]);

        arguments[1].ShouldBe("expanded-1");
        materializer.Indices.ShouldBe([0, 1]);
        arguments.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void CachesMaterializedNull()
    {
        const string text = "$(Value)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(_ => null);

        arguments.ConfigureMaterialization(materializer, materializeAllArguments: false);

        arguments[0].ShouldBeNull();
        arguments[0].ShouldBeNull();
        materializer.Indices.ShouldBe([0]);
        arguments.IsMaterialized.ShouldBeTrue();
    }

    [Fact]
    public void WellKnownFunctionMaterializesAccessedArgument()
    {
        const string text = "$(Value)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(_ => "value");
        arguments.ConfigureMaterialization(materializer, materializeAllArguments: false);

        bool handled = WellKnownFunctions.TryExecuteStringFunction(
            nameof(string.StartsWith),
            "value-suffix",
            arguments,
            out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(true);
        materializer.Indices.ShouldBe([0]);
        arguments.IsMaterialized.ShouldBeTrue();
    }

    private sealed class TrackingMaterializer(Func<int, object?> materialize) : IFunctionArgumentMaterializer
    {
        public List<int> Indices { get; } = [];

        public object? Materialize(StringSegment source, int index, FunctionArgumentRequirements requirements)
        {
            Indices.Add(index);
            return materialize(index);
        }
    }
}
