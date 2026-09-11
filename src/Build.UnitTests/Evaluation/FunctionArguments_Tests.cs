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

        arguments.Count.ShouldBe(2);
        arguments.GetValue(0).ShouldBe("42");
        arguments.GetValue(1).ShouldBe("value");
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

        arguments.GetValue(0).ShouldBe("42");
        arguments.GetValue(1).ShouldBe("value");
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
    }

    [Fact]
    public void SnapshotReturnsRawValuesWithoutMaterializing()
    {
        const string text = "null, '', value";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);

        arguments.SnapshotValues().ShouldBe([null, string.Empty, "value"]);
    }

    [Fact]
    public void MaterializesOnlyAccessedArguments()
    {
        const string text = "$(First), $(Second)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(index => $"expanded-{index}");
        arguments.SetMaterializer(materializer);

        arguments.GetValue(0).ShouldBe("expanded-0");
        materializer.Indices.ShouldBe([0]);

        arguments.GetValue(0).ShouldBe("expanded-0");
        materializer.Indices.ShouldBe([0]);

        arguments.GetValue(1).ShouldBe("expanded-1");
        materializer.Indices.ShouldBe([0, 1]);
    }

    [Fact]
    public void CachesMaterializedNull()
    {
        const string text = "$(Value)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(_ => null);
        arguments.SetMaterializer(materializer);

        arguments.GetValue(0).ShouldBeNull();
        arguments.GetValue(0).ShouldBeNull();
        materializer.Indices.ShouldBe([0]);
    }

    [Fact]
    public void WellKnownFunctionMaterializesAccessedArgument()
    {
        const string text = "$(Value)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(_ => "value");
        arguments.SetMaterializer(materializer);

        bool handled = WellKnownFunctions.TryInvokeInstance(
            "value-suffix",
            nameof(string.StartsWith),
            ref arguments,
            out object? result);

        handled.ShouldBeTrue();
        result.ShouldBe(true);
        materializer.Indices.ShouldBe([0]);
    }

    [Fact]
    public void MaterializesAllArgumentsOnce()
    {
        const string text = "$(First), $(Second)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(index => $"expanded-{index}");
        arguments.SetMaterializer(materializer);

        arguments.MaterializeAll().ShouldBe(["expanded-0", "expanded-1"]);
        arguments.MaterializeAll().ShouldBe(["expanded-0", "expanded-1"]);
        materializer.Indices.ShouldBe([0, 1]);
    }

    [Fact]
    public void SnapshotDoesNotMaterializeUnresolvedArguments()
    {
        const string text = "$(First), $(Second)";
        ArgumentList source = PropertyFunctionParser.ParseArguments(text, text, MockElementLocation.Instance);
        FunctionArguments arguments = new(source);
        var materializer = new TrackingMaterializer(index => $"expanded-{index}");
        arguments.SetMaterializer(materializer);

        arguments.GetValue(1).ShouldBe("expanded-1");

        arguments.SnapshotValues().ShouldBe(["$(First)", "expanded-1"]);
        materializer.Indices.ShouldBe([1]);
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
