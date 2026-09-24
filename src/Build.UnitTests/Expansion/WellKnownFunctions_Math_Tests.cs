// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

public class WellKnownFunctions_Math_Tests(ITestOutputHelper output)
    : WellKnownFunctionsTestBase(typeof(Math), output)
{
    [Fact]
    public void Math_Max_Double()
        => StaticMember(nameof(Math.Max))
            .Invoke([1.5d, 2.5d])
            .ShouldBe(2.5d);

    [Theory]
    [InlineData(1L, 2L, 2d)]
    [InlineData("1.5", "2.5", 2.5d)]
    public void Math_Max_ConvertibleToDouble(object left, object right, double expected)
        => StaticMember(nameof(Math.Max))
            .Invoke([left, right])
            .ShouldBe(expected);

    [Fact]
    public void Math_Max_Decimal()
        => StaticMember(nameof(Math.Max))
            .Invoke([1m, 2m])
            .ShouldBe(2m);

    [Fact]
    public void Math_Max_InvalidArgument_NotHandled()
        => StaticMember(nameof(Math.Max))
            .NotHandled(["invalid", 2d]);

    [Fact]
    public void Math_Max_OneArgument_NotHandled()
        => StaticMember(nameof(Math.Max))
            .NotHandled([1d]);

    [Fact]
    public void Math_Min_Double()
        => StaticMember(nameof(Math.Min))
            .Invoke([1.5d, 2.5d])
            .ShouldBe(1.5d);

    [Fact]
    public void Math_Min_InvalidArgument_NotHandled()
        => StaticMember(nameof(Math.Min))
            .NotHandled([1d, "invalid"]);

    [Fact]
    public void Math_CommonMembers()
    {
        StaticMember(nameof(Math.E)).Invoke().ShouldBe(Math.E);
        StaticMember(nameof(Math.PI)).Invoke().ShouldBe(Math.PI);
        StaticMember(nameof(Math.Acos)).Invoke(["1"]).ShouldBe(Math.Acos(1));
        StaticMember(nameof(Math.Asin)).Invoke(["1"]).ShouldBe(Math.Asin(1));
        StaticMember(nameof(Math.Atan)).Invoke(["1"]).ShouldBe(Math.Atan(1));
        StaticMember(nameof(Math.Atan2)).Invoke(["1", "2"]).ShouldBe(Math.Atan2(1, 2));
        StaticMember(nameof(Math.Cos)).Invoke(["1"]).ShouldBe(Math.Cos(1));
        StaticMember(nameof(Math.Cosh)).Invoke(["1"]).ShouldBe(Math.Cosh(1));
        StaticMember(nameof(Math.Exp)).Invoke(["1"]).ShouldBe(Math.Exp(1));
        StaticMember(nameof(Math.IEEERemainder)).Invoke(["5", "2"]).ShouldBe(Math.IEEERemainder(5, 2));
        StaticMember(nameof(Math.Log)).Invoke(["2"]).ShouldBe(Math.Log(2));
        StaticMember(nameof(Math.Log)).Invoke(["8", "2"]).ShouldBe(3d);
        StaticMember(nameof(Math.Log10)).Invoke(["100"]).ShouldBe(2d);
        StaticMember(nameof(Math.Pow)).Invoke(["2", "3"]).ShouldBe(8d);
        StaticMember(nameof(Math.Sin)).Invoke(["1"]).ShouldBe(Math.Sin(1));
        StaticMember(nameof(Math.Sinh)).Invoke(["1"]).ShouldBe(Math.Sinh(1));
        StaticMember(nameof(Math.Sqrt)).Invoke(["4"]).ShouldBe(2d);
        StaticMember(nameof(Math.Tan)).Invoke(["1"]).ShouldBe(Math.Tan(1));
        StaticMember(nameof(Math.Tanh)).Invoke(["1"]).ShouldBe(Math.Tanh(1));
#if NET
        StaticMember(nameof(Math.Tau)).Invoke().ShouldBe(Math.Tau);
        StaticMember(nameof(Math.Acosh)).Invoke(["2"]).ShouldBe(Math.Acosh(2));
        StaticMember(nameof(Math.Asinh)).Invoke(["2"]).ShouldBe(Math.Asinh(2));
        StaticMember(nameof(Math.Atanh)).Invoke(["0.5"]).ShouldBe(Math.Atanh(0.5));
        StaticMember(nameof(Math.BitDecrement)).Invoke(["2"]).ShouldBe(Math.BitDecrement(2));
        StaticMember(nameof(Math.BitIncrement)).Invoke(["2"]).ShouldBe(Math.BitIncrement(2));
        StaticMember(nameof(Math.Cbrt)).Invoke(["8"]).ShouldBe(2d);
        StaticMember(nameof(Math.CopySign)).Invoke(["2", "-1"]).ShouldBe(-2d);
        StaticMember(nameof(Math.FusedMultiplyAdd)).Invoke(["2", "3", "4"]).ShouldBe(10d);
        StaticMember(nameof(Math.ILogB)).Invoke(["8"]).ShouldBe(3);
        StaticMember(nameof(Math.Log2)).Invoke(["8"]).ShouldBe(3d);
        StaticMember(nameof(Math.MaxMagnitude)).Invoke(["-3", "2"]).ShouldBe(-3d);
        StaticMember(nameof(Math.MinMagnitude)).Invoke(["-3", "2"]).ShouldBe(2d);
        StaticMember(nameof(Math.ReciprocalEstimate)).Invoke(["4"]).ShouldBe(Math.ReciprocalEstimate(4));
        StaticMember(nameof(Math.ReciprocalSqrtEstimate)).Invoke(["4"]).ShouldBe(Math.ReciprocalSqrtEstimate(4));
        StaticMember(nameof(Math.ScaleB)).Invoke(["2", "3"]).ShouldBe(16d);
        StaticMember(nameof(Math.SinCos)).Invoke(["0"]).ShouldBe((0d, 1d));
#endif
    }

    [Fact]
    public void Math_Abs_Overloads()
    {
        StaticMember(nameof(Math.Abs)).Invoke([-1.5m]).ShouldBe(1.5m);
        StaticMember(nameof(Math.Abs)).Invoke([-1.5d]).ShouldBe(1.5d);
        StaticMember(nameof(Math.Abs)).Invoke([-1.5f]).ShouldBe(1.5f);
        StaticMember(nameof(Math.Abs)).Invoke([-1L]).ShouldBe(1L);
        StaticMember(nameof(Math.Abs)).Invoke([-1]).ShouldBe(1);
        StaticMember(nameof(Math.Abs)).Invoke([(short)-1]).ShouldBe((short)1);
        StaticMember(nameof(Math.Abs)).Invoke([(sbyte)-1]).ShouldBe((sbyte)1);
        StaticMember(nameof(Math.Abs)).Invoke([(byte)1]).ShouldBe((short)1);
        StaticMember(nameof(Math.Abs)).Invoke([(ushort)1]).ShouldBe(1);
        StaticMember(nameof(Math.Abs)).Invoke([1u]).ShouldBe(1L);
        StaticMember(nameof(Math.Abs)).Invoke([1UL]).ShouldBe(1f);
    }

    [Fact]
    public void Math_BigMul_Overloads()
    {
        StaticMember(nameof(Math.BigMul)).Invoke([3, 4]).ShouldBe(12L);
        StaticMember(nameof(Math.BigMul)).NotHandled([(byte)3, (byte)4]);
        StaticMember(nameof(Math.BigMul)).NotHandled([(ushort)3, (ushort)4]);
        StaticMember(nameof(Math.BigMul)).NotHandled(['3', '4']);
#if NET
        StaticMember(nameof(Math.BigMul)).Invoke([3u, 4u]).ShouldBe(12UL);
        StaticMember(nameof(Math.BigMul)).Invoke([3L, 4L]).ShouldBe((Int128)12);
        StaticMember(nameof(Math.BigMul)).Invoke([3UL, 4UL]).ShouldBe((UInt128)12);
#endif
    }

    [Fact]
    public void Math_Ceiling_Floor_And_Truncate_Overloads()
    {
        StaticMember(nameof(Math.Ceiling)).Invoke([1.2m]).ShouldBe(2m);
        StaticMember(nameof(Math.Ceiling)).Invoke([1.2d]).ShouldBe(2d);
        StaticMember(nameof(Math.Floor)).Invoke([1.8m]).ShouldBe(1m);
        StaticMember(nameof(Math.Floor)).Invoke([1.8d]).ShouldBe(1d);
        StaticMember(nameof(Math.Truncate)).Invoke([1.8m]).ShouldBe(1m);
        StaticMember(nameof(Math.Truncate)).Invoke([1.8d]).ShouldBe(1d);
    }

    [Fact]
    public void Math_Min_And_Max_Overloads()
    {
        AssertMinMax((sbyte)1, (sbyte)2);
        AssertMinMax((byte)1, (byte)2);
        AssertMinMax((short)1, (short)2);
        AssertMinMax((ushort)1, (ushort)2);
        AssertMinMax(1, 2);
        AssertMinMax(1u, 2u);
        AssertMinMax(1L, 2L);
        AssertMinMax(1UL, 2UL);
        AssertMinMax(1f, 2f);
        AssertMinMax(1d, 2d);
        AssertMinMax(1m, 2m);
    }

    [Fact]
    public void Math_Round_Overloads()
    {
        StaticMember(nameof(Math.Round)).Invoke([1.5d]).ShouldBe(2d);
        StaticMember(nameof(Math.Round)).Invoke([1.25d, 1]).ShouldBe(1.2d);
        StaticMember(nameof(Math.Round)).Invoke([1.5d, MidpointRounding.AwayFromZero]).ShouldBe(2d);
        StaticMember(nameof(Math.Round)).Invoke([1.25d, 1, MidpointRounding.AwayFromZero]).ShouldBe(1.3d);
        StaticMember(nameof(Math.Round)).Invoke([1.5m]).ShouldBe(2m);
        StaticMember(nameof(Math.Round)).Invoke([1.25m, 1]).ShouldBe(1.2m);
        StaticMember(nameof(Math.Round)).Invoke([1.5m, MidpointRounding.AwayFromZero]).ShouldBe(2m);
        StaticMember(nameof(Math.Round)).Invoke([1.25m, 1, MidpointRounding.AwayFromZero]).ShouldBe(1.3m);
    }

    [Fact]
    public void Math_Sign_Overloads()
    {
        StaticMember(nameof(Math.Sign)).Invoke([-1.5m]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([-1.5d]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([-1.5f]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([-1L]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([-1]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([(short)-1]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([(sbyte)-1]).ShouldBe(-1);
        StaticMember(nameof(Math.Sign)).Invoke([(byte)1]).ShouldBe(1);
        StaticMember(nameof(Math.Sign)).Invoke([(ushort)1]).ShouldBe(1);
        StaticMember(nameof(Math.Sign)).Invoke([1u]).ShouldBe(1);
        StaticMember(nameof(Math.Sign)).Invoke([1UL]).ShouldBe(1);
    }

    [Fact]
    public void Math_Widens_Boxed_Numeric_Arguments()
    {
        StaticMember(nameof(Math.Sqrt)).Invoke([4f]).ShouldBe(2d);
        StaticMember(nameof(Math.BigMul)).Invoke([(short)3, 4]).ShouldBe(12L);
        StaticMember(nameof(Math.Max)).Invoke(['A', 'B']).ShouldBe((ushort)'B');
        StaticMember(nameof(Math.Max)).Invoke([(short)1, 2]).ShouldBe(2);
        StaticMember(nameof(Math.Max)).Invoke([1u, 2L]).ShouldBe(2L);
        StaticMember(nameof(Math.Max)).Invoke([1L, 2UL]).ShouldBe(2f);
        StaticMember(nameof(Math.Max)).Invoke([1f, 2d]).ShouldBe(2d);
        StaticMember(nameof(Math.Max)).NotHandled([1f, 2m]);
#if NET
        StaticMember(nameof(Math.Clamp)).Invoke(['C', 'A', 'B']).ShouldBe((ushort)'B');
        StaticMember(nameof(Math.Clamp)).Invoke([(short)3, 0, 2]).ShouldBe(2);
        StaticMember(nameof(Math.DivRem)).Invoke(['G', 'B']).ShouldBe(((ushort)1, (ushort)5));
        StaticMember(nameof(Math.DivRem)).Invoke([(short)7, 2]).ShouldBe((3, 1));
#endif
    }

#if NET
    [Fact]
    public void Math_Clamp_Overloads()
    {
        AssertClamp((sbyte)3, (sbyte)0, (sbyte)2);
        AssertClamp((byte)3, (byte)0, (byte)2);
        AssertClamp((short)3, (short)0, (short)2);
        AssertClamp((ushort)3, (ushort)0, (ushort)2);
        AssertClamp(3, 0, 2);
        AssertClamp(3u, 0u, 2u);
        AssertClamp(3L, 0L, 2L);
        AssertClamp(3UL, 0UL, 2UL);
        AssertClamp(3f, 0f, 2f);
        AssertClamp(3d, 0d, 2d);
        AssertClamp(3m, 0m, 2m);
    }

    [Fact]
    public void Math_DivRem_Overloads()
    {
        StaticMember(nameof(Math.DivRem)).Invoke([(sbyte)7, (sbyte)2]).ShouldBe(((sbyte)3, (sbyte)1));
        StaticMember(nameof(Math.DivRem)).Invoke([(byte)7, (byte)2]).ShouldBe(((byte)3, (byte)1));
        StaticMember(nameof(Math.DivRem)).Invoke([(short)7, (short)2]).ShouldBe(((short)3, (short)1));
        StaticMember(nameof(Math.DivRem)).Invoke([(ushort)7, (ushort)2]).ShouldBe(((ushort)3, (ushort)1));
        StaticMember(nameof(Math.DivRem)).Invoke([7, 2]).ShouldBe((3, 1));
        StaticMember(nameof(Math.DivRem)).Invoke([7u, 2u]).ShouldBe((3u, 1u));
        StaticMember(nameof(Math.DivRem)).Invoke([7L, 2L]).ShouldBe((3L, 1L));
        StaticMember(nameof(Math.DivRem)).Invoke([7UL, 2UL]).ShouldBe((3UL, 1UL));
    }
#endif

    private void AssertMinMax<T>(T left, T right)
    {
        StaticMember(nameof(Math.Min)).Invoke([left, right]).ShouldBe(left);
        StaticMember(nameof(Math.Max)).Invoke([left, right]).ShouldBe(right);
    }

#if NET
    private void AssertClamp<T>(T value, T min, T max)
        => StaticMember(nameof(Math.Clamp)).Invoke([value, min, max]).ShouldBe(max);
#endif
}
