// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Expansion.Legacy;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion.Legacy;

[Trait("Category", "expansion")]
public class LegacyArgumentParser_Tests
{
    [Theory]
    [MemberData(nameof(ConvertToIntData))]
    public void TryConvertToIntSucceeds(object value, int expected)
    {
        LegacyArgumentParser.TryConvertToInt(value, out int actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object, int> ConvertToIntData => new()
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
        LegacyArgumentParser.TryConvertToInt(value, out int actual).ShouldBeFalse();
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
    public void TryConvertToLongSucceeds(object value, long expected)
    {
        LegacyArgumentParser.TryConvertToLong(value, out long actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object, long> ConvertToLongData => new()
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
        LegacyArgumentParser.TryConvertToLong(value, out long actual).ShouldBeFalse();
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
            LegacyArgumentParser.TryConvertToLong((double)long.MaxValue, out _));

    [WindowsFullFrameworkOnlyFact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueFramework()
    {
        // long.MaxValue does not round-trip through double on .NET Framework.
        LegacyArgumentParser.TryConvertToLong((double)long.MaxValue, out long actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [DotNetOnlyFact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueDotNet()
    {
        LegacyArgumentParser.TryConvertToLong((double)long.MaxValue, out long actual).ShouldBeTrue();
        actual.ShouldBe(long.MaxValue);
    }

    [Theory]
    [MemberData(nameof(ConvertToDoubleData))]
    public void TryConvertToDoubleSucceeds(object value, double expected)
    {
        LegacyArgumentParser.TryConvertToDouble(value, out double actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    public static TheoryData<object, double> ConvertToDoubleData => new()
    {
        { 10.0, 10.0 },
        { 10L, 10.0 },
        { 10, 10.0 },
        { "10", 10.0 },
    };

    [Fact]
    public void TryConvertToDoubleGivenNull()
    {
        LegacyArgumentParser.TryConvertToDouble(null, out double actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void TryConvertToDoubleGivenStringAndLocale()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;

        try
        {
            // en-ZA uses a decimal comma; invariant parsing treats the comma as a group separator.
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("en-ZA");

            LegacyArgumentParser.TryConvertToDouble("1,2", out double actual).ShouldBeTrue();
            actual.ShouldBe(12.0);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
