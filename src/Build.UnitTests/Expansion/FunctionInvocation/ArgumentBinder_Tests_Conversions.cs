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
    public void StandardBindingPrecedesPropertyFunctionConversion()
    {
        Arguments arguments = new([1]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(string)),
            CreateOverload(typeof(long)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe(1L);
    }

    [Fact]
    public void AmbiguousStandardBindingDoesNotProceedToConversion()
    {
        Arguments arguments = new([(byte)1]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(char)),
            CreateOverload(typeof(short)),
            CreateOverload(typeof(string)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out _);

        result.ShouldBe(BindingResult.Ambiguous);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void ConversionUsesCandidateOrder()
    {
        Arguments arguments = new(["1"]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(short)),
            CreateOverload(typeof(int)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(0);
        convertedArguments[0].ShouldBe((short)1);
    }

    [Fact]
    public void ConversionContinuesAfterCandidateOverflow()
    {
        Arguments arguments = new(["128"]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(sbyte)),
            CreateOverload(typeof(short)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments[0].ShouldBe((short)128);
    }

    [Fact]
    public void OverflowWithoutAnotherCandidateProducesNoApplicableOverload()
    {
        Arguments arguments = new(["128"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(sbyte))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void NumericStringConvertsUsingInvariantCulture()
    {
        Arguments arguments = new(["1.5"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(double))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(1.5D);
    }

    [Fact]
    public void DirectDoubleConversionAcceptsTrailingSign()
    {
        Arguments arguments = new(["1-"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(double))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(-1D);
    }

    [Fact]
    public void GeneralNumericConversionRunsAfterDirectConversion()
    {
        Arguments arguments = new([1.5D]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(int))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(2);
    }

    [Fact]
    public void SingleCharacterStringConvertsToChar()
    {
        Arguments arguments = new(["x"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(char))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe('x');
    }

    [Fact]
    public void MultiCharacterStringDoesNotConvertToChar()
    {
        Arguments arguments = new(["xy"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(char))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void ValueConvertsToCharacterArrayUsingItsStringRepresentation()
    {
        Arguments arguments = new([123]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(char[]))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBeOfType<char[]>().ShouldBe(['1', '2', '3']);
    }

    [Fact]
    public void StringConvertsToVersion()
    {
        Arguments arguments = new(["1.2.3.4"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(Version))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(new Version(1, 2, 3, 4));
    }

    [Fact]
    public void UnqualifiedStringConvertsToEnum()
    {
        Arguments arguments = new(["Second"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(TestFlags))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(TestFlags.Second);
    }

    [Fact]
    public void QualifiedFlagStringConvertsToEnum()
    {
        string typeName = typeof(TestFlags).FullName!;
        Arguments arguments = new([$"{typeName}.First | {nameof(TestFlags)}.Second"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(TestFlags))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(TestFlags.First | TestFlags.Second);
    }

    [Fact]
    public void NumericStringDoesNotConvertToEnum()
    {
        Arguments arguments = new(["1"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(TestFlags))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void InvalidQualifiedStringDoesNotConvertToEnum()
    {
        Arguments arguments = new([$"{nameof(TestFlags)}.Unknown"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(TestFlags))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Fact]
    public void StringConvertsToBoolean()
    {
        Arguments arguments = new(["true"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(bool))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(true);
    }

    [Fact]
    public void ValueConvertsToString()
    {
        Arguments arguments = new([true]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(string))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe("True");
    }

    [Fact]
    public void StringConvertsToDateTimeUsingInvariantCulture()
    {
        Arguments arguments = new(["2024-01-02"]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(DateTime))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(new DateTime(2024, 1, 2));
    }

    [Fact]
    public void EveryArgumentMustConvertWithinTheSameCandidate()
    {
        Arguments arguments = new(["1", "not a number"]);
        ImmutableArray<Overload> overloads =
        [
            CreateOverload(typeof(int), typeof(int)),
            CreateOverload(typeof(double), typeof(string)),
        ];

        BindingResult result = Bind(ref arguments, overloads, out int overloadIndex, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        overloadIndex.ShouldBe(1);
        convertedArguments.ShouldBe([1D, "not a number"]);
    }

    [Fact]
    public void CustomConvertibleParticipatesInConversion()
    {
        Arguments arguments = new([new ConvertibleValue(42)]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(long))];

        BindingResult result = Bind(ref arguments, overloads, out _, out object?[] convertedArguments);

        result.ShouldBe(BindingResult.Success);
        convertedArguments[0].ShouldBe(42L);
    }

    [Fact]
    public void UnconvertibleArgumentProducesNoApplicableOverload()
    {
        Arguments arguments = new([new object()]);
        ImmutableArray<Overload> overloads = [CreateOverload(typeof(Uri))];

        Bind(ref arguments, overloads, out int overloadIndex, out _).ShouldBe(BindingResult.NoApplicableOverload);
        overloadIndex.ShouldBe(-1);
    }

    [Flags]
    private enum TestFlags
    {
        First = 1,
        Second = 2,
    }

    private sealed class ConvertibleValue(IConvertible value) : IConvertible
    {
        public TypeCode GetTypeCode() => value.GetTypeCode();
        public bool ToBoolean(IFormatProvider? provider) => value.ToBoolean(provider);
        public byte ToByte(IFormatProvider? provider) => value.ToByte(provider);
        public char ToChar(IFormatProvider? provider) => value.ToChar(provider);
        public DateTime ToDateTime(IFormatProvider? provider) => value.ToDateTime(provider);
        public decimal ToDecimal(IFormatProvider? provider) => value.ToDecimal(provider);
        public double ToDouble(IFormatProvider? provider) => value.ToDouble(provider);
        public short ToInt16(IFormatProvider? provider) => value.ToInt16(provider);
        public int ToInt32(IFormatProvider? provider) => value.ToInt32(provider);
        public long ToInt64(IFormatProvider? provider) => value.ToInt64(provider);
        public sbyte ToSByte(IFormatProvider? provider) => value.ToSByte(provider);
        public float ToSingle(IFormatProvider? provider) => value.ToSingle(provider);
        public string ToString(IFormatProvider? provider) => value.ToString(provider);
        public object ToType(Type conversionType, IFormatProvider? provider) => value.ToType(conversionType, provider);
        public ushort ToUInt16(IFormatProvider? provider) => value.ToUInt16(provider);
        public uint ToUInt32(IFormatProvider? provider) => value.ToUInt32(provider);
        public ulong ToUInt64(IFormatProvider? provider) => value.ToUInt64(provider);
    }
}
