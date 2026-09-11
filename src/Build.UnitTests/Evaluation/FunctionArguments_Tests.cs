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
        arguments.TryGetArgs(out string? number, out string? value).ShouldBeTrue();
        number.ShouldBe("42");
        value.ShouldBe("value");
        arguments.IsMaterialized.ShouldBeFalse();
    }

    [Theory]
    [InlineData("value", false)]
    [InlineData("$(Value)", true)]
    [InlineData("%(Identity)", false)]
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
        arguments.TryGetArgs(out string? number, out string? value).ShouldBeTrue();
        number.ShouldBe("42");
        value.ShouldBe("value");
    }

    [Fact]
    public void WellKnownStringFunctionConsumesRawSegment()
    {
        const string text = "value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        bool handled = WellKnownFunctions.TryInvokeInstance(
            "prefix-value-suffix",
            nameof(string.Contains),
            ref arguments,
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

        arguments.ConfigureMaterialization(materializer, materializeOnAccess: true);

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

        arguments.ConfigureMaterialization(materializer, materializeOnAccess: true);

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
        arguments.ConfigureMaterialization(materializer, materializeOnAccess: true);

        bool handled = WellKnownFunctions.TryInvokeInstance(
            "value-suffix",
            nameof(string.StartsWith),
            ref arguments,
            out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(true);
        materializer.Indices.ShouldBe([0]);
        arguments.IsMaterialized.ShouldBeTrue();
    }

    private sealed class TrackingMaterializer(Func<int, object?> materialize) : IFunctionArgumentMaterializer
    {
        public List<int> Indices { get; } = [];

        public object? Materialize(StringSegment source, int index)
        {
            Indices.Add(index);
            return materialize(index);
        }
    }
}
