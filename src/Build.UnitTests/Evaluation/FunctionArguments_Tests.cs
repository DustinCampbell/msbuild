// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
#endif

    [Theory]
    [InlineData("value", false)]
    [InlineData("$(Value)", true)]
    [InlineData("%(Identity)", true)]
    public void DetectsExpandableSourceArguments(string text, bool expected)
    {
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        arguments.ContainsExpandableExpression().ShouldBe(expected);
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

    [Fact]
    public void MaterializedValuesReplaceSourceSegments()
    {
        const string text = "1, value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        arguments.SetMaterialized([42, "expanded"]);

        arguments.IsMaterialized.ShouldBeTrue();
        arguments.TryGetArgs(out int number, out string? value).ShouldBeTrue();
        number.ShouldBe(42);
        value.ShouldBe("expanded");
    }

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
}
