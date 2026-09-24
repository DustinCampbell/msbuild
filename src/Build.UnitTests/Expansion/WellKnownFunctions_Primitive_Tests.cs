// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Primitive_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(int), output)
{
    [Theory]
    [InlineData(42, "D4", "0042")]
    [InlineData(255, "X", "FF")]
    public void Primitive_ToString_Format(int value, string format, string expected)
        => InstanceMember(nameof(int.ToString))
            .Invoke(value, [format])
            .ShouldBe(expected);

    [Fact]
    public void Primitive_InstanceMembers()
    {
        InstanceMember(nameof(int.CompareTo)).Invoke(42, [41]).ShouldBe(1);
        InstanceMember(nameof(int.Equals)).Invoke(42, [42]).ShouldBe(true);
        InstanceMember(nameof(double.CompareTo)).Invoke(42d, [41]).ShouldBe(1);
        InstanceMember(nameof(double.Equals)).Invoke(42d, [42]).ShouldBe(true);
        InstanceMember(nameof(double.Equals)).Invoke(42d, ["42"]).ShouldBe(false);
        InstanceMember(nameof(int.GetHashCode)).Invoke(42).ShouldBe(42.GetHashCode());
        InstanceMember(nameof(int.GetTypeCode)).Invoke(42).ShouldBe(TypeCode.Int32);
        InstanceMember(nameof(int.ToString)).Invoke(42).ShouldBe("42");
        InstanceMember(nameof(int.ToString)).Invoke(42, [CultureInfo.InvariantCulture]).ShouldBe("42");
        InstanceMember(nameof(int.ToString)).Invoke(42, ["D4", CultureInfo.InvariantCulture]).ShouldBe("0042");
        InstanceMember(nameof(bool.ToString)).Invoke(true).ShouldBe(bool.TrueString);
        InstanceMember(nameof(char.ToString)).Invoke('x').ShouldBe("x");
#if NET
        InstanceMember(nameof(IntPtr.ToString)).Invoke(new IntPtr(42), ["X"]).ShouldBe("2A");
#endif
    }

    [Theory]
    [MemberData(nameof(MinMaxValues))]
    public void Primitive_MinMaxValue(Type type, object minValue, object maxValue)
    {
        StaticMemberFor(type, nameof(int.MinValue)).Invoke().ShouldBe(minValue);
        StaticMemberFor(type, nameof(int.MaxValue)).Invoke().ShouldBe(maxValue);
    }

    public static TheoryData<Type, object, object> MinMaxValues => new()
    {
        { typeof(byte), byte.MinValue, byte.MaxValue },
        { typeof(sbyte), sbyte.MinValue, sbyte.MaxValue },
        { typeof(short), short.MinValue, short.MaxValue },
        { typeof(ushort), ushort.MinValue, ushort.MaxValue },
        { typeof(int), int.MinValue, int.MaxValue },
        { typeof(uint), uint.MinValue, uint.MaxValue },
        { typeof(long), long.MinValue, long.MaxValue },
        { typeof(ulong), ulong.MinValue, ulong.MaxValue },
        { typeof(float), float.MinValue, float.MaxValue },
        { typeof(double), double.MinValue, double.MaxValue },
        { typeof(decimal), decimal.MinValue, decimal.MaxValue },
        { typeof(char), char.MinValue, char.MaxValue },
    };

    [Theory]
    [MemberData(nameof(FloatingPointFields))]
    public void Primitive_FloatingPointFields(Type type, string name, object expected)
        => StaticMemberFor(type, name).Invoke().ShouldBe(expected);

    public static IEnumerable<object[]> FloatingPointFields
    {
        get
        {
            yield return [typeof(float), nameof(float.Epsilon), float.Epsilon];
            yield return [typeof(float), nameof(float.NaN), float.NaN];
            yield return [typeof(float), nameof(float.NegativeInfinity), float.NegativeInfinity];
            yield return [typeof(float), nameof(float.PositiveInfinity), float.PositiveInfinity];
            yield return [typeof(double), nameof(double.Epsilon), double.Epsilon];
            yield return [typeof(double), nameof(double.NaN), double.NaN];
            yield return [typeof(double), nameof(double.NegativeInfinity), double.NegativeInfinity];
            yield return [typeof(double), nameof(double.PositiveInfinity), double.PositiveInfinity];
#if NET
            yield return [typeof(float), nameof(float.E), float.E];
            yield return [typeof(float), nameof(float.NegativeZero), float.NegativeZero];
            yield return [typeof(float), nameof(float.Pi), float.Pi];
            yield return [typeof(float), nameof(float.Tau), float.Tau];
            yield return [typeof(double), nameof(double.E), double.E];
            yield return [typeof(double), nameof(double.NegativeZero), double.NegativeZero];
            yield return [typeof(double), nameof(double.Pi), double.Pi];
            yield return [typeof(double), nameof(double.Tau), double.Tau];
#endif
        }
    }

    [Fact]
    public void Decimal_Fields()
    {
        StaticMemberFor(typeof(decimal), nameof(decimal.MinusOne)).Invoke().ShouldBe(decimal.MinusOne);
        StaticMemberFor(typeof(decimal), nameof(decimal.One)).Invoke().ShouldBe(decimal.One);
        StaticMemberFor(typeof(decimal), nameof(decimal.Zero)).Invoke().ShouldBe(decimal.Zero);
    }

    [Fact]
    public void Primitive_InvalidArguments_NotHandled()
    {
        InstanceMember(nameof(int.ToString)).NotHandled(42, [new object()]);
        InstanceMember(nameof(int.GetHashCode)).NotHandled(42, [1]);
        StaticMember(nameof(int.MaxValue)).NotHandled([1]);
        StaticMember(nameof(int.Parse)).NotHandled(["42"]);
    }

    private StaticMemberInvoker StaticMemberFor(Type type, string memberName)
        => new(type, memberName);
}
