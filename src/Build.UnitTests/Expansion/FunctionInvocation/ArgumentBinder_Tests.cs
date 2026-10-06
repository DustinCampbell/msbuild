// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Expansion.FunctionInvocation;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion.FunctionInvocation;

[Trait("Category", "expansion")]
public partial class ArgumentBinder_Tests
{
    [Fact]
    public void ExactMatchIsPreferredOverWidening()
    {
        Arguments arguments = new([1]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(long)),
            CreateOverload(typeof(int)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe(1);
    }

    [Fact]
    public void NarrowestPrimitiveWideningIsSelected()
    {
        Arguments arguments = new([(byte)1]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(long)),
            CreateOverload(typeof(short)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe((short)1);
    }

    [Fact]
    public void PrimitiveNarrowingUsesPropertyFunctionConversion()
    {
        Arguments arguments = new([1L]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(int))];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments[0].ShouldBe(1);
    }

    [Fact]
    public void DecimalUsesPropertyFunctionConversionToDouble()
    {
        Arguments arguments = new([1m]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(double))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(1D);
    }

    [Fact]
    public void AssignableReferenceTypesUseTheMostSpecificParameter()
    {
        using var stream = new MemoryStream();
        Arguments arguments = new([stream]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(object)),
            CreateOverload(typeof(Stream)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBeSameAs(stream);
    }

    [Fact]
    public void NullUsesTheMostSpecificRelatedReferenceType()
    {
        Arguments arguments = new([null]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(object)),
            CreateOverload(typeof(string)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBeNull();
    }

    [Fact]
    public void NullIsAmbiguousForUnrelatedReferenceTypes()
    {
        Arguments arguments = new([null]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(string)),
            CreateOverload(typeof(Stream)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out _);

        result.ShouldBe(BindingResult.Ambiguous);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void NullBindsAsTheDefaultValueOfAValueType()
    {
        Arguments arguments = new([null]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(int))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(0);
    }

    [Fact]
    public void EnumUsesItsUnderlyingPrimitiveTypeForWidening()
    {
        Arguments arguments = new([ByteEnum.One]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(long)),
            CreateOverload(typeof(short)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe((short)1);
    }

    [Fact]
    public void EquallySpecificPrimitiveConversionsAreAmbiguous()
    {
        Arguments arguments = new([(byte)1]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(char)),
            CreateOverload(typeof(short)),
        ];

        Bind(ref arguments, overloads, out _, out _).ShouldBe(BindingResult.Ambiguous);
    }

    [Fact]
    public void ArityFilteringDoesNotMaterializeArguments()
    {
        var materializer = new RecordingMaterializer([1]);
        ExpanderContext context = default;
        Arguments arguments = new(["arg0"], materializer, default, in context);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(),
            CreateOverload(typeof(int), typeof(int)),
        ];

        Bind(ref arguments, overloads, out _, out _).ShouldBe(BindingResult.NoApplicableOverload);
        materializer.MaterializedIndexes.ShouldBeEmpty();
    }

    [Fact]
    public void SupportsMoreThanFourArguments()
    {
        Arguments arguments = new([1, 2, 3, 4, 5]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments.ShouldBe([1, 2, 3, 4, 5]);
    }

    private static BindingResult Bind(
        ref Arguments arguments, ImmutableArray<Overload> overloads, out int overloadIndex, out object?[] convertedArguments)
    {
        convertedArguments = new object?[arguments.Length];
        return ArgumentBinder.TryBind(ref arguments, overloads, convertedArguments, out overloadIndex);
    }

    private static Overload CreateOverload(params Type[] parameterTypes)
        => new([.. parameterTypes.Select(static p => new Parameter(p))]);

    private static Overload CreateParamArrayOverload(Type elementType, params Type[] fixedParameterTypes)
        => new(
        [
            .. fixedParameterTypes.Select(static p => new Parameter(p)),
            new Parameter(elementType.MakeArrayType(), ParameterFlags.ParamArray),
        ]);

    private enum ByteEnum : byte
    {
        One = 1,
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
}
