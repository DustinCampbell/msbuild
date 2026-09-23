// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Build.Evaluation.Expander;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class ArgumentParser_Tests
{
    [Theory]
    [MemberData(nameof(ArithmeticArgumentData))]
    public void TryGetArithmeticArguments(object? left, object? right, object? expected0, object? expected1)
    {
        bool success = ArgumentParser.TryGetArithmeticArguments([left, right], out var arguments);

        success.ShouldBe(expected0 is not null);

        if (expected0 is long expectedLong0)
        {
            arguments.TryGetLongs(out long actual0, out long actual1).ShouldBeTrue();
            arguments.TryGetDoubles(out _, out _).ShouldBeFalse();
            actual0.ShouldBe(expectedLong0);
            actual1.ShouldBe((long)expected1!);
        }
        else if (expected0 is double expectedDouble0)
        {
            arguments.TryGetLongs(out _, out _).ShouldBeFalse();
            arguments.TryGetDoubles(out double actual0, out double actual1).ShouldBeTrue();
            actual0.ShouldBe(expectedDouble0);
            actual1.ShouldBe((double)expected1!);
        }
    }

    public static TheoryData<object?, object?, object?, object?> ArithmeticArgumentData
    {
        get
        {
            TheoryData<object?, object?, object?, object?> data = new();

            foreach ((object? left, object? right, object? expected0, object? expected1) in GetArithmeticArgumentCases())
            {
                data.Add(left, right, expected0, expected1);
            }

            return data;
        }
    }

    internal static IEnumerable<(object? Left, object? Right, object? Expected0, object? Expected1)> GetArithmeticArgumentCases()
    {
        var operands = new[]
        {
            (Left: (object?)null, Right: (object?)null, LeftDouble: 0D, RightDouble: 0D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)"1", Right: (object?)"2", LeftDouble: 1D, RightDouble: 2D,
                DirectLong: true, DirectDouble: true, WidensToLong: false, WidensToDouble: false,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)"1.25", Right: (object?)"2.25", LeftDouble: 1.25D, RightDouble: 2.25D,
                DirectLong: false, DirectDouble: true, WidensToLong: false, WidensToDouble: false,
                CoercesToLong: false, CoercesToDouble: true),
            (Left: (object?)"1-", Right: (object?)"2-", LeftDouble: -1D, RightDouble: -2D,
                DirectLong: false, DirectDouble: true, WidensToLong: false, WidensToDouble: false,
                CoercesToLong: false, CoercesToDouble: false),
            (Left: (object?)(sbyte)1, Right: (object?)(sbyte)2, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)(byte)1, Right: (object?)(byte)2, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)(short)1, Right: (object?)(short)2, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)(ushort)1, Right: (object?)(ushort)2, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1, Right: (object?)2, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: true, DirectDouble: true, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1U, Right: (object?)2U, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1L, Right: (object?)2L, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: true, DirectDouble: true, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1UL, Right: (object?)2UL, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: false, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1.25F, Right: (object?)2.25F, LeftDouble: 1.25D, RightDouble: 2.25D,
                DirectLong: false, DirectDouble: false, WidensToLong: false, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1.25D, Right: (object?)2.25D, LeftDouble: 1.25D, RightDouble: 2.25D,
                DirectLong: false, DirectDouble: true, WidensToLong: false, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)1.25M, Right: (object?)2.25M, LeftDouble: 1.25D, RightDouble: 2.25D,
                DirectLong: false, DirectDouble: false, WidensToLong: false, WidensToDouble: false,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)true, Right: (object?)false, LeftDouble: 1D, RightDouble: 0D,
                DirectLong: false, DirectDouble: false, WidensToLong: false, WidensToDouble: false,
                CoercesToLong: true, CoercesToDouble: true),
            (Left: (object?)'A', Right: (object?)'B', LeftDouble: 65D, RightDouble: 66D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: false),
            (Left: (object?)DayOfWeek.Monday, Right: (object?)DayOfWeek.Tuesday, LeftDouble: 1D, RightDouble: 2D,
                DirectLong: false, DirectDouble: false, WidensToLong: true, WidensToDouble: true,
                CoercesToLong: true, CoercesToDouble: true),
        };

        foreach (var left in operands)
        {
            foreach (var right in operands)
            {
                bool useLong = left.DirectLong && right.DirectLong;
                bool useDouble = !useLong && left.DirectDouble && right.DirectDouble;

                if (!useLong && !useDouble)
                {
                    useLong = left.WidensToLong && right.WidensToLong;
                    useDouble = !useLong && left.WidensToDouble && right.WidensToDouble;

                    if (!useLong && !useDouble)
                    {
                        useLong = left.CoercesToLong && right.CoercesToLong;
                        useDouble = !useLong && left.CoercesToDouble && right.CoercesToDouble;
                    }
                }

                if (useLong)
                {
                    yield return (
                        left.Left,
                        right.Right,
                        Convert.ToInt64(left.LeftDouble),
                        Convert.ToInt64(right.RightDouble));
                }
                else if (useDouble)
                {
                    yield return (left.Left, right.Right, left.LeftDouble, right.RightDouble);
                }
                else
                {
                    yield return (left.Left, right.Right, null, null);
                }
            }
        }
    }

    [Fact]
    public void TryGetArithmeticArgumentsRequiresTwoArguments()
    {
        ArgumentParser.TryGetArithmeticArguments([], out _).ShouldBeFalse();
        ArgumentParser.TryGetArithmeticArguments([1], out _).ShouldBeFalse();
        ArgumentParser.TryGetArithmeticArguments([1, 2, 3], out _).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(ConvertToIntData))]
    public void TryConvertToIntSucceeds(object? value, int expected)
    {
        ArgumentParser.TryConvertToInt(value, out int actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object?, int> ConvertToIntData => new()
    {
        { 10.0, 10 },
        { 10L, 10 },
        { 10, 10 },
        { "10", 10 },
        { (double)int.MinValue, int.MinValue },
        { (double)int.MaxValue, int.MaxValue },
    };

    [Theory]
    [MemberData(nameof(InvalidIntConversionData))]
    public void TryConvertToIntFails(object? value)
    {
        ArgumentParser.TryConvertToInt(value, out int actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    public static TheoryData<object?> InvalidIntConversionData => new()
    {
        (object?)null,
        int.MinValue - 1.0,
        int.MaxValue + 1.0,
        int.MaxValue + 1L,
    };

    [Theory]
    [MemberData(nameof(ConvertToLongData))]
    public void TryConvertToLongSucceeds(object? value, long expected)
    {
        ArgumentParser.TryConvertToLong(value, out long actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object?, long> ConvertToLongData => new()
    {
        { 10.0, 10L },
        { 10L, 10L },
        { 10, 10L },
        { "10", 10L },
        { (double)long.MinValue, long.MinValue },

        // The double loses precision, but the conversion still passes the tolerance check.
        { (double)(long.MaxValue - 512), 9223372036854774784L },
    };

    [Theory]
    [MemberData(nameof(InvalidLongConversionData))]
    public void TryConvertToLongFails(object? value)
    {
        ArgumentParser.TryConvertToLong(value, out long actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    public static TheoryData<object?> InvalidLongConversionData => new()
    {
        (object?)null,
        -92233720368547758081D,
        (double)long.MaxValue + long.MaxValue,
    };

    /// <summary>
    ///  Convert.ToInt64(double) throws near long.MaxValue because of lost precision.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueShouldNotThrow()
        => Should.NotThrow(static () =>
            ArgumentParser.TryConvertToLong((double)long.MaxValue, out _));

    [WindowsFullFrameworkOnlyFact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueFramework()
    {
        // long.MaxValue does not round-trip through double on .NET Framework.
        ArgumentParser.TryConvertToLong((double)long.MaxValue, out long actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [DotNetOnlyFact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueDotNet()
    {
        ArgumentParser.TryConvertToLong((double)long.MaxValue, out long actual).ShouldBeTrue();
        actual.ShouldBe(long.MaxValue);
    }

    [Theory]
    [MemberData(nameof(ConvertToDoubleData))]
    public void TryConvertToDoubleSucceeds(object? value, double expected)
    {
        ArgumentParser.TryConvertToDouble(value, out double actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object?, double> ConvertToDoubleData => new()
    {
        { 10.0, 10.0 },
        { 10L, 10.0 },
        { 10, 10.0 },
        { "10", 10.0 },
    };

    [Fact]
    public void TryConvertToDoubleGivenNullFails()
    {
        ArgumentParser.TryConvertToDouble(null, out double actual).ShouldBeFalse();
        actual.ShouldBe(0D);
    }

    [Fact]
    public void TryConvertToDoubleGivenStringAndLocale()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // en-ZA uses a decimal comma; invariant parsing treats the comma as a group separator.
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("en-ZA");

            ArgumentParser.TryConvertToDouble("1,2", out double actual).ShouldBeTrue();
            actual.ShouldBe(12.0);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
