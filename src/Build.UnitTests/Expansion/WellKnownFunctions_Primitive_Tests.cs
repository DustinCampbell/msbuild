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

    [Fact]
    public void FloatingPoint_StaticMethods()
    {
        AssertFloatingPointStaticMethods<float>();
        AssertFloatingPointStaticMethods<double>();
    }

    [Fact]
    public void FloatingPoint_StaticMethods_CoerceArguments()
    {
        StaticMemberFor(typeof(float), nameof(float.Max)).Invoke([(byte)4, (short)2]).ShouldBe(4f);
        StaticMemberFor(typeof(float), nameof(float.Clamp)).Invoke(["4", (byte)1, 3L]).ShouldBe(3f);
        StaticMemberFor(typeof(double), nameof(double.Max)).Invoke([(byte)4, (short)2]).ShouldBe(4d);
        StaticMemberFor(typeof(double), nameof(double.Clamp)).Invoke(["4", (byte)1, 3L]).ShouldBe(3d);
        StaticMemberFor(typeof(float), nameof(float.Max)).NotHandled(['A', 0f]);
        StaticMemberFor(typeof(double), nameof(double.Max)).NotHandled(['A', 0d]);
    }

    private void AssertFloatingPointStaticMethods<T>()
        where T : unmanaged, System.Numerics.IFloatingPointIeee754<T>, System.Numerics.IMinMaxValue<T>
    {
        Type type = typeof(T);
        T negativeOne = T.CreateChecked(-1);
        T zero = T.Zero;
        T quarter = T.CreateChecked(0.25);
        T half = T.CreateChecked(0.5);
        T one = T.One;
        T two = T.CreateChecked(2);
        T three = T.CreateChecked(3);
        T four = T.CreateChecked(4);
        T eight = T.CreateChecked(8);

        StaticMemberFor(type, nameof(float.Abs)).Invoke([negativeOne]).ShouldBe(T.Abs(negativeOne));
        StaticMemberFor(type, nameof(float.Acos)).Invoke([half]).ShouldBe(T.Acos(half));
        StaticMemberFor(type, nameof(float.Acosh)).Invoke([two]).ShouldBe(T.Acosh(two));
        StaticMemberFor(type, nameof(float.AcosPi)).Invoke([half]).ShouldBe(T.AcosPi(half));
        StaticMemberFor(type, nameof(float.Asin)).Invoke([half]).ShouldBe(T.Asin(half));
        StaticMemberFor(type, nameof(float.Asinh)).Invoke([half]).ShouldBe(T.Asinh(half));
        StaticMemberFor(type, nameof(float.AsinPi)).Invoke([half]).ShouldBe(T.AsinPi(half));
        StaticMemberFor(type, nameof(float.Atan)).Invoke([one]).ShouldBe(T.Atan(one));
        StaticMemberFor(type, nameof(float.Atan2)).Invoke([one, two]).ShouldBe(T.Atan2(one, two));
        StaticMemberFor(type, nameof(float.Atan2Pi)).Invoke([one, two]).ShouldBe(T.Atan2Pi(one, two));
        StaticMemberFor(type, nameof(float.Atanh)).Invoke([half]).ShouldBe(T.Atanh(half));
        StaticMemberFor(type, nameof(float.AtanPi)).Invoke([one]).ShouldBe(T.AtanPi(one));
        StaticMemberFor(type, nameof(float.BitDecrement)).Invoke([one]).ShouldBe(T.BitDecrement(one));
        StaticMemberFor(type, nameof(float.BitIncrement)).Invoke([one]).ShouldBe(T.BitIncrement(one));
        StaticMemberFor(type, nameof(float.Cbrt)).Invoke([eight]).ShouldBe(T.Cbrt(eight));
        StaticMemberFor(type, nameof(float.Ceiling)).Invoke([half]).ShouldBe(T.Ceiling(half));
        StaticMemberFor(type, nameof(float.Clamp)).Invoke([two, zero, one]).ShouldBe(T.Clamp(two, zero, one));
        StaticMemberFor(type, nameof(float.ClampNative)).Invoke([two, zero, one]).ShouldBe(T.ClampNative(two, zero, one));
        StaticMemberFor(type, nameof(float.CopySign)).Invoke([one, negativeOne]).ShouldBe(T.CopySign(one, negativeOne));
        StaticMemberFor(type, nameof(float.Cos)).Invoke([one]).ShouldBe(T.Cos(one));
        StaticMemberFor(type, nameof(float.Cosh)).Invoke([one]).ShouldBe(T.Cosh(one));
        StaticMemberFor(type, nameof(float.CosPi)).Invoke([one]).ShouldBe(T.CosPi(one));
        StaticMemberFor(type, nameof(float.DegreesToRadians)).Invoke([one]).ShouldBe(T.DegreesToRadians(one));
        StaticMemberFor(type, nameof(float.Exp)).Invoke([one]).ShouldBe(T.Exp(one));
        StaticMemberFor(type, nameof(float.Exp10)).Invoke([one]).ShouldBe(T.Exp10(one));
        StaticMemberFor(type, nameof(float.Exp10M1)).Invoke([one]).ShouldBe(T.Exp10M1(one));
        StaticMemberFor(type, nameof(float.Exp2)).Invoke([one]).ShouldBe(T.Exp2(one));
        StaticMemberFor(type, nameof(float.Exp2M1)).Invoke([one]).ShouldBe(T.Exp2M1(one));
        StaticMemberFor(type, nameof(float.ExpM1)).Invoke([one]).ShouldBe(T.ExpM1(one));
        StaticMemberFor(type, nameof(float.Floor)).Invoke([half]).ShouldBe(T.Floor(half));
        StaticMemberFor(type, nameof(float.FusedMultiplyAdd)).Invoke([two, three, four]).ShouldBe(T.FusedMultiplyAdd(two, three, four));
        StaticMemberFor(type, nameof(float.Hypot)).Invoke([three, four]).ShouldBe(T.Hypot(three, four));
        StaticMemberFor(type, nameof(float.Ieee754Remainder)).Invoke([three, two]).ShouldBe(T.Ieee754Remainder(three, two));
        StaticMemberFor(type, nameof(float.ILogB)).Invoke([four]).ShouldBe(T.ILogB(four));
        StaticMemberFor(type, nameof(float.IsEvenInteger)).Invoke([four]).ShouldBe(T.IsEvenInteger(four));
        StaticMemberFor(type, nameof(float.IsFinite)).Invoke([one]).ShouldBe(T.IsFinite(one));
        StaticMemberFor(type, nameof(float.IsInfinity)).Invoke([T.PositiveInfinity]).ShouldBe(T.IsInfinity(T.PositiveInfinity));
        StaticMemberFor(type, nameof(float.IsInteger)).Invoke([one]).ShouldBe(T.IsInteger(one));
        StaticMemberFor(type, nameof(float.IsNaN)).Invoke([T.NaN]).ShouldBe(T.IsNaN(T.NaN));
        StaticMemberFor(type, nameof(float.IsNegative)).Invoke([negativeOne]).ShouldBe(T.IsNegative(negativeOne));
        StaticMemberFor(type, nameof(float.IsNegativeInfinity))
            .Invoke([T.NegativeInfinity])
            .ShouldBe(T.IsNegativeInfinity(T.NegativeInfinity));
        StaticMemberFor(type, nameof(float.IsNormal)).Invoke([one]).ShouldBe(T.IsNormal(one));
        StaticMemberFor(type, nameof(float.IsOddInteger)).Invoke([three]).ShouldBe(T.IsOddInteger(three));
        StaticMemberFor(type, nameof(float.IsPositive)).Invoke([one]).ShouldBe(T.IsPositive(one));
        StaticMemberFor(type, nameof(float.IsPositiveInfinity))
            .Invoke([T.PositiveInfinity])
            .ShouldBe(T.IsPositiveInfinity(T.PositiveInfinity));
        StaticMemberFor(type, nameof(float.IsPow2)).Invoke([four]).ShouldBe(true);
        StaticMemberFor(type, nameof(float.IsRealNumber)).Invoke([one]).ShouldBe(T.IsRealNumber(one));
        StaticMemberFor(type, nameof(float.IsSubnormal)).Invoke([T.Epsilon]).ShouldBe(T.IsSubnormal(T.Epsilon));
        StaticMemberFor(type, nameof(float.Lerp)).Invoke([one, three, half]).ShouldBe(T.Lerp(one, three, half));
        StaticMemberFor(type, nameof(float.Log)).Invoke([two]).ShouldBe(T.Log(two));
        StaticMemberFor(type, nameof(float.Log)).Invoke([four, two]).ShouldBe(T.Log(four, two));
        StaticMemberFor(type, nameof(float.Log10)).Invoke([two]).ShouldBe(T.Log10(two));
        StaticMemberFor(type, nameof(float.Log10P1)).Invoke([one]).ShouldBe(T.Log10P1(one));
        StaticMemberFor(type, nameof(float.Log2)).Invoke([two]).ShouldBe(T.Log2(two));
        StaticMemberFor(type, nameof(float.Log2P1)).Invoke([one]).ShouldBe(T.Log2P1(one));
        StaticMemberFor(type, nameof(float.LogP1)).Invoke([one]).ShouldBe(T.LogP1(one));
        StaticMemberFor(type, nameof(float.Max)).Invoke([one, two]).ShouldBe(T.Max(one, two));
        StaticMemberFor(type, nameof(float.MaxMagnitude)).Invoke([negativeOne, two]).ShouldBe(T.MaxMagnitude(negativeOne, two));
        StaticMemberFor(type, nameof(float.MaxMagnitudeNumber)).Invoke([negativeOne, two]).ShouldBe(T.MaxMagnitudeNumber(negativeOne, two));
        StaticMemberFor(type, nameof(float.MaxNative)).Invoke([one, two]).ShouldBe(T.MaxNative(one, two));
        StaticMemberFor(type, nameof(float.MaxNumber)).Invoke([one, two]).ShouldBe(T.MaxNumber(one, two));
        StaticMemberFor(type, nameof(float.Min)).Invoke([one, two]).ShouldBe(T.Min(one, two));
        StaticMemberFor(type, nameof(float.MinMagnitude)).Invoke([negativeOne, two]).ShouldBe(T.MinMagnitude(negativeOne, two));
        StaticMemberFor(type, nameof(float.MinMagnitudeNumber)).Invoke([negativeOne, two]).ShouldBe(T.MinMagnitudeNumber(negativeOne, two));
        StaticMemberFor(type, nameof(float.MinNative)).Invoke([one, two]).ShouldBe(T.MinNative(one, two));
        StaticMemberFor(type, nameof(float.MinNumber)).Invoke([one, two]).ShouldBe(T.MinNumber(one, two));
        StaticMemberFor(type, nameof(float.MultiplyAddEstimate))
            .Invoke([two, three, four])
            .ShouldBe(T.MultiplyAddEstimate(two, three, four));
        StaticMemberFor(type, nameof(float.Pow)).Invoke([two, three]).ShouldBe(T.Pow(two, three));
        StaticMemberFor(type, nameof(float.RadiansToDegrees)).Invoke([one]).ShouldBe(T.RadiansToDegrees(one));
        StaticMemberFor(type, nameof(float.ReciprocalEstimate)).Invoke([two]).ShouldBe(T.ReciprocalEstimate(two));
        StaticMemberFor(type, nameof(float.ReciprocalSqrtEstimate)).Invoke([four]).ShouldBe(T.ReciprocalSqrtEstimate(four));
        StaticMemberFor(type, nameof(float.RootN)).Invoke([four, 2]).ShouldBe(T.RootN(four, 2));
        StaticMemberFor(type, nameof(float.Round)).Invoke([half]).ShouldBe(T.Round(half));
        StaticMemberFor(type, nameof(float.Round)).Invoke([quarter, 1]).ShouldBe(T.Round(quarter, 1));
        StaticMemberFor(type, nameof(float.Round))
            .Invoke([half, MidpointRounding.AwayFromZero])
            .ShouldBe(T.Round(half, MidpointRounding.AwayFromZero));
        StaticMemberFor(type, nameof(float.Round)).Invoke([quarter, 1, MidpointRounding.AwayFromZero])
            .ShouldBe(T.Round(quarter, 1, MidpointRounding.AwayFromZero));
        StaticMemberFor(type, nameof(float.ScaleB)).Invoke([one, 2]).ShouldBe(T.ScaleB(one, 2));
        StaticMemberFor(type, nameof(float.Sign)).Invoke([negativeOne]).ShouldBe(T.Sign(negativeOne));
        StaticMemberFor(type, nameof(float.Sin)).Invoke([one]).ShouldBe(T.Sin(one));
        StaticMemberFor(type, nameof(float.SinCos)).Invoke([one])!.ToString().ShouldBe(T.SinCos(one).ToString());
        StaticMemberFor(type, nameof(float.SinCosPi)).Invoke([one])!.ToString().ShouldBe(T.SinCosPi(one).ToString());
        StaticMemberFor(type, nameof(float.Sinh)).Invoke([one]).ShouldBe(T.Sinh(one));
        StaticMemberFor(type, nameof(float.SinPi)).Invoke([half]).ShouldBe(T.SinPi(half));
        StaticMemberFor(type, nameof(float.Sqrt)).Invoke([four]).ShouldBe(T.Sqrt(four));
        StaticMemberFor(type, nameof(float.Tan)).Invoke([one]).ShouldBe(T.Tan(one));
        StaticMemberFor(type, nameof(float.Tanh)).Invoke([one]).ShouldBe(T.Tanh(one));
        StaticMemberFor(type, nameof(float.TanPi)).Invoke([quarter]).ShouldBe(T.TanPi(quarter));
        StaticMemberFor(type, nameof(float.Truncate)).Invoke([half]).ShouldBe(T.Truncate(half));
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
