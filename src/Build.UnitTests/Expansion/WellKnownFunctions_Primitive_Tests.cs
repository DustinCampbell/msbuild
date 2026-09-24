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
    [MemberData(nameof(ParseValues))]
    public void Primitive_Parse(Type type, string value, object expected)
    {
        StaticMemberFor(type, nameof(int.Parse)).Invoke([value]).ShouldBe(expected);
        StaticMemberFor(type, nameof(int.Parse)).Invoke([value, NumberStyles.Integer]).ShouldBe(expected);
        StaticMemberFor(type, nameof(int.Parse)).Invoke([value, CultureInfo.InvariantCulture]).ShouldBe(expected);
        StaticMemberFor(type, nameof(int.Parse)).Invoke([value, NumberStyles.Integer, CultureInfo.InvariantCulture]).ShouldBe(expected);
    }

    public static TheoryData<Type, string, object> ParseValues => new()
    {
        { typeof(byte), "42", (byte)42 },
        { typeof(sbyte), "42", (sbyte)42 },
        { typeof(short), "42", (short)42 },
        { typeof(ushort), "42", (ushort)42 },
        { typeof(int), "42", 42 },
        { typeof(uint), "42", 42u },
        { typeof(long), "42", 42L },
        { typeof(ulong), "42", 42UL },
        { typeof(float), "42", 42f },
        { typeof(double), "42", 42d },
        { typeof(decimal), "42", 42m },
    };

    [Fact]
    public void Primitive_Parse_QualifiedNumberStyles()
        => StaticMemberFor(typeof(int), nameof(int.Parse))
            .Invoke(["FF", "System.Globalization.NumberStyles.HexNumber"])
            .ShouldBe(255);

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

#if NET
    [Fact]
    public void Integer_StaticMethods()
    {
        (Type Type, int Bits, bool Signed)[] types =
        [
            (typeof(byte), 8, false),
            (typeof(sbyte), 8, true),
            (typeof(short), 16, true),
            (typeof(ushort), 16, false),
            (typeof(int), 32, true),
            (typeof(uint), 32, false),
            (typeof(long), 64, true),
            (typeof(ulong), 64, false),
        ];

        foreach ((Type type, int bits, bool signed) in types)
        {
            object one = Convert.ChangeType(1, type, CultureInfo.InvariantCulture);
            object two = Convert.ChangeType(2, type, CultureInfo.InvariantCulture);
            object three = Convert.ChangeType(3, type, CultureInfo.InvariantCulture);
            object four = Convert.ChangeType(4, type, CultureInfo.InvariantCulture);
            object five = Convert.ChangeType(5, type, CultureInfo.InvariantCulture);
            object seven = Convert.ChangeType(7, type, CultureInfo.InvariantCulture);
            object eight = Convert.ChangeType(8, type, CultureInfo.InvariantCulture);
            object hundred = Convert.ChangeType(100, type, CultureInfo.InvariantCulture);

            StaticMemberFor(type, nameof(int.Clamp)).Invoke([four, one, three]).ShouldBe(three);
            StaticMemberFor(type, nameof(int.DivRem)).Invoke([five, two])!.ToString().ShouldBe("(2, 1)");
            StaticMemberFor(type, nameof(int.IsEvenInteger)).Invoke([four]).ShouldBe(true);
            StaticMemberFor(type, nameof(int.IsOddInteger)).Invoke([three]).ShouldBe(true);
            StaticMemberFor(type, nameof(int.IsPow2)).Invoke([four]).ShouldBe(true);
            StaticMemberFor(type, nameof(int.LeadingZeroCount)).Invoke([one]).ShouldBe(Convert.ChangeType(bits - 1, type));
            StaticMemberFor(type, nameof(int.Log10)).Invoke([hundred]).ShouldBe(Convert.ChangeType(2, type));
            StaticMemberFor(type, nameof(int.Log2)).Invoke([eight]).ShouldBe(Convert.ChangeType(3, type));
            StaticMemberFor(type, nameof(int.Max)).Invoke([four, two]).ShouldBe(four);
            StaticMemberFor(type, nameof(int.Min)).Invoke([four, two]).ShouldBe(two);
            StaticMemberFor(type, nameof(int.PopCount)).Invoke([seven]).ShouldBe(Convert.ChangeType(3, type));
            StaticMemberFor(type, nameof(int.RotateLeft)).Invoke([one, 1]).ShouldBe(two);
            StaticMemberFor(type, nameof(int.RotateRight)).Invoke([two, 1]).ShouldBe(one);
            StaticMemberFor(type, nameof(int.Sign)).Invoke([four]).ShouldBe(1);
            StaticMemberFor(type, nameof(int.TrailingZeroCount)).Invoke([eight]).ShouldBe(Convert.ChangeType(3, type));

            if (signed)
            {
                object negativeFour = Convert.ChangeType(-4, type, CultureInfo.InvariantCulture);

                StaticMemberFor(type, nameof(int.Abs)).Invoke([negativeFour]).ShouldBe(four);
                StaticMemberFor(type, nameof(int.CopySign)).Invoke([four, negativeFour]).ShouldBe(negativeFour);
                StaticMemberFor(type, nameof(int.IsNegative)).Invoke([negativeFour]).ShouldBe(true);
                StaticMemberFor(type, nameof(int.IsPositive)).Invoke([four]).ShouldBe(true);
                StaticMemberFor(type, nameof(int.MaxMagnitude)).Invoke([negativeFour, two]).ShouldBe(negativeFour);
                StaticMemberFor(type, nameof(int.MinMagnitude)).Invoke([negativeFour, two]).ShouldBe(two);
            }
        }

        StaticMemberFor(typeof(int), nameof(int.BigMul)).Invoke([4, 2])!.ToString().ShouldBe("8");
        StaticMemberFor(typeof(uint), nameof(uint.BigMul)).Invoke([4u, 2u])!.ToString().ShouldBe("8");
        StaticMemberFor(typeof(long), nameof(long.BigMul)).Invoke([4L, 2L])!.ToString().ShouldBe("8");
        StaticMemberFor(typeof(ulong), nameof(ulong.BigMul)).Invoke([4UL, 2UL])!.ToString().ShouldBe("8");
    }

    [Fact]
    public void Integer_StaticMethods_CoerceArguments()
    {
        StaticMember(nameof(int.Max)).Invoke([(byte)4, (short)2]).ShouldBe(4);
        StaticMember(nameof(int.Clamp)).Invoke(["4", (byte)1, 3L]).ShouldBe(3);
    }
#endif

    [Fact]
    public void Primitive_InvalidArguments_NotHandled()
    {
        InstanceMember(nameof(int.ToString)).NotHandled(42, [new object()]);
        InstanceMember(nameof(int.GetHashCode)).NotHandled(42, [1]);
        StaticMember(nameof(int.MaxValue)).NotHandled([1]);
        StaticMember(nameof(int.Parse)).NotHandled([42]);
    }

    private StaticMemberInvoker StaticMemberFor(Type type, string memberName)
        => new(type, memberName);
}
