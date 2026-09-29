// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class Arguments_Tests
{
    [Fact]
    public void EagerArgumentsReuseProvidedArray()
    {
        object?[] values = ["first", null, 3];
        Arguments arguments = new(values);

        arguments.AllMaterialized.ShouldBeTrue();
        arguments.ToObjectArray().ShouldBeSameAs(values);
    }

    [Fact]
    public void LazyArgumentsMaterializeAndCacheRequestedValue()
    {
        var materializer = new RecordingMaterializer(["first", null, "third"]);
        ExpanderContext context = default;
        Arguments arguments = new(["arg0", "arg1", "arg2"], materializer, default, in context);

        arguments.Length.ShouldBe(3);
        arguments.AllMaterialized.ShouldBeFalse();
        materializer.MaterializedIndexes.ShouldBeEmpty();

        arguments.TryGetArg(1, out object? first).ShouldBeTrue();
        first.ShouldBeNull();
        arguments.TryGetArg(1, out object? second).ShouldBeTrue();
        second.ShouldBeNull();

        arguments.AllMaterialized.ShouldBeFalse();
        materializer.MaterializedIndexes.ShouldBe([1]);
    }

    [Fact]
    public void FailedMaterializationPropagatesAndLeavesArgumentsIncomplete()
    {
        var failure = new InvalidOperationException("Materialization failed.");
        var materializer = new ThrowingMaterializer(failure);
        ExpanderContext context = default;
        Arguments arguments = new(["arg0"], materializer, default, in context);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() =>
            arguments.TryGetArg(0, out object? _));

        exception.ShouldBeSameAs(failure);
        arguments.AllMaterialized.ShouldBeFalse();
        materializer.CallCount.ShouldBe(1);
    }

    [Fact]
    public void ToObjectArrayMaterializesRemainingValuesInIndexOrder()
    {
        var materializer = new RecordingMaterializer(["first", null, "third"]);
        ExpanderContext context = default;
        Arguments arguments = new(["arg0", "arg1", "arg2"], materializer, default, in context);

        arguments.TryGetArg(1, out object? _).ShouldBeTrue();

        object?[] values = arguments.ToObjectArray();

        arguments.AllMaterialized.ShouldBeTrue();
        values.ShouldBe(["first", null, "third"]);
        materializer.MaterializedIndexes.ShouldBe([1, 0, 2]);
        arguments.ToObjectArray().ShouldBeSameAs(values);
        materializer.MaterializedIndexes.ShouldBe([1, 0, 2]);
    }

    [Fact]
    public void EmptyLazyArgumentsMaterializeToEmptyArray()
    {
        var materializer = new RecordingMaterializer([]);
        ExpanderContext context = default;
        Arguments arguments = new([], materializer, default, in context);

        arguments.AllMaterialized.ShouldBeTrue();

        object?[] values = arguments.ToObjectArray();

        values.ShouldBeEmpty();
        arguments.AllMaterialized.ShouldBeTrue();
        materializer.MaterializedIndexes.ShouldBeEmpty();
    }

    private sealed class RecordingMaterializer(object?[] values) : IArgumentMaterializer
    {
        public List<int> MaterializedIndexes { get; } = [];

        public object? MaterializeArgument(string argText, int argIndex, ExpanderOptions options, ref readonly ExpanderContext context)
        {
            MaterializedIndexes.Add(argIndex);
            return values[argIndex];
        }
    }

    private sealed class ThrowingMaterializer(InvalidOperationException failure) : IArgumentMaterializer
    {
        public int CallCount { get; private set; }

        public object? MaterializeArgument(string argText, int argIndex, ExpanderOptions options, ref readonly ExpanderContext context)
        {
            CallCount++;
            throw failure;
        }
    }
}
