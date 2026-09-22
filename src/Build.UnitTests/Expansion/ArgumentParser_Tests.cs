// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Evaluation.Expander;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

[Trait("Category", "expansion")]
public class ArgumentParser_Tests
{
    [Theory]
    [MemberData(nameof(ConvertToIntData))]
    public void TryConvertToIntSucceeds(object? value, int expected)
    {
        ArgumentParser.TryConvertToInt(value, out int actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object?, int> ConvertToIntData => new()
    {
        { null, 0 },
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
        { null, 0L },
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
        { null, 0.0 },
        { 10.0, 10.0 },
        { 10L, 10.0 },
        { 10, 10.0 },
        { "10", 10.0 },
    };

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
