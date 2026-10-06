// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Expansion.FunctionInvocation;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion.FunctionInvocation;

public partial class ArgumentBinder_Tests
{
    [Fact]
    public void NoOverloadsProducesNoApplicableOverload()
    {
        var materializer = new RecordingMaterializer([1]);
        ExpanderContext context = default;
        Arguments arguments = new(["arg0"], materializer, default, in context);

        BindingResult result = Bind(ref arguments, [], out int overloadIndex, out _);

        result.ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
        materializer.MaterializedIndexes.ShouldBeEmpty();
    }

    [Fact]
    public void ParameterlessOverloadBindsEmptyArguments()
    {
        Arguments arguments = new([]);
        ImmutableArray<Overload> overloads = [default];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments.ShouldBeEmpty();
    }

    [Fact]
    public void ExactEnumOverloadIsPreferredOverUnderlyingPrimitive()
    {
        Arguments arguments = new([ByteEnum.One]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(int)),
            CreateOverload(typeof(ByteEnum)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe(ByteEnum.One);
    }

    [Fact]
    public void NullUsesMostSpecificPrimitiveParameter()
    {
        Arguments arguments = new([null]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(long)),
            CreateOverload(typeof(int)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe(0);
    }

    [Fact]
    public void NullBindsToNullableValueType()
    {
        Arguments arguments = new([null]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(int?))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBeNull();
    }

    [Fact]
    public void CompetingParameterPositionsAreAmbiguous()
    {
        Arguments arguments = new([(byte)1, (byte)2]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(short), typeof(long)),
            CreateOverload(typeof(long), typeof(short)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out _);

        result.ShouldBe(BindingResult.Ambiguous);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void UnrelatedAssignableReferenceTypesAreAmbiguous()
    {
        Arguments arguments = new(["value"]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(IComparable)),
            CreateOverload(typeof(ICloneable)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out _);

        result.ShouldBe(BindingResult.Ambiguous);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void IntPtrRequiresExactStandardMatch()
    {
        nint value = 1;
        Arguments arguments = new([value]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(long)),
            CreateOverload(typeof(nint)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe(value);
    }

    [Fact]
    public void ArgumentIsMaterializedOnlyOnceAcrossCandidates()
    {
        var materializer = new RecordingMaterializer([(byte)1]);
        ExpanderContext context = default;
        Arguments arguments = new(["arg0"], materializer, default, in context);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(long)),
            CreateOverload(typeof(short)),
            CreateOverload(typeof(double)),
        ];

        Bind(ref arguments, overloads, out _, out _).ShouldBe(BindingResult.Success);
        materializer.MaterializedIndexes.ShouldBe([0]);
    }

    [Fact]
    public void NullIsRetainedWhileAnotherArgumentIsConverted()
    {
        Arguments arguments = new([null, "1"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(string), typeof(int))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments.ShouldBe([null, 1]);
    }
}
