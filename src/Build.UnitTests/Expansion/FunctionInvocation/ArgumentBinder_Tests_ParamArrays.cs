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
    public void ParamArrayAcceptsNoArguments()
    {
        Arguments arguments = new([]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(string))];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments.ShouldBeEmpty();
    }

    [Fact]
    public void ParamArrayAcceptsAndConvertsTrailingArguments()
    {
        Arguments arguments = new([(byte)1, 2]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(long))];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments.ShouldBe([1L, 2L]);
    }

    [Fact]
    public void ParamArraySupportsFixedParameters()
    {
        Arguments arguments = new(["prefix", 1, 2]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(long), typeof(string))];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments.ShouldBe(["prefix", 1L, 2L]);
    }

    [Fact]
    public void ParamArrayReceivesAssignableArrayWithoutExpansion()
    {
        string[] values = ["first", "second"];
        Arguments arguments = new([values]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(string))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBeSameAs(values);
    }

    [Fact]
    public void FixedOverloadIsPreferredOverExpandedParamArray()
    {
        Arguments arguments = new(["value"]);
        ImmutableArray<Overload> overloads =
        [
            CreateParamArrayOverload(typeof(object)),
            CreateOverload(typeof(string)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out _);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
    }

    [Fact]
    public void NarrowerParamArrayElementTypeIsPreferred()
    {
        Arguments arguments = new([(byte)1]);
        ImmutableArray<Overload> overloads =
        [
            CreateParamArrayOverload(typeof(long)),
            CreateParamArrayOverload(typeof(short)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe((short)1);
    }

    [Fact]
    public void NullDoesNotStandardBindAsPrimitiveParamArrayElement()
    {
        Arguments arguments = new([null]);
        ImmutableArray<Overload> overloads =
        [
            CreateParamArrayOverload(typeof(int)),
            CreateParamArrayOverload(typeof(object)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBeNull();
    }

    [Fact]
    public void LongerParamArraySignatureBreaksOtherwiseEqualTie()
    {
        Arguments arguments = new(["value"]);
        ImmutableArray<Overload> overloads =
        [
            CreateParamArrayOverload(typeof(object)),
            CreateParamArrayOverload(typeof(object), typeof(object)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out _);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
    }

    [Fact]
    public void ConversionFallbackDoesNotExpandParamArray()
    {
        Arguments arguments = new(["1", "2"]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(int))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void ConversionFallbackDoesNotSynthesizeOmittedParamArray()
    {
        Arguments arguments = new(["1"]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(string), typeof(int))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void ConversionFallbackAcceptsExplicitParamArray()
    {
        string[] values = ["first", "second"];
        Arguments arguments = new(["1", values]);
        ImmutableArray<Overload> overloads = [CreateParamArrayOverload(typeof(string), typeof(int))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments.ShouldBe([1, values]);
    }
}
