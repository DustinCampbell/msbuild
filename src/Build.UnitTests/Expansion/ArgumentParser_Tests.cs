// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Evaluation.Expander;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Expansion;

/// <summary>
///  Tests numeric argument conversion for property functions.
/// </summary>
/// <param name="output">The output helper for the test.</param>
[Trait("Category", "expansion")]
public sealed class ArgumentParserTests(ITestOutputHelper output) : TestBase(output)
{
    /// <summary>
    ///  Verifies that a null argument cannot be converted to an integer.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenNull()
        => ArgumentParser.TryConvertToInt(null, out _).ShouldBeFalse();

    /// <summary>
    ///  Verifies conversion of an integral double to an integer.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenDouble()
    {
        ArgumentParser.TryConvertToInt(10.0D, out int result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of a long to an integer.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenLong()
    {
        ArgumentParser.TryConvertToInt(10L, out int result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of an argument that is already an integer.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenInt()
    {
        ArgumentParser.TryConvertToInt(10, out int result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of a numeric string to an integer.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenString()
    {
        ArgumentParser.TryConvertToInt("10", out int result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of a double at the minimum integer value.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenDoubleWithIntMinValue()
    {
        ArgumentParser.TryConvertToInt((double)int.MinValue, out int result).ShouldBeTrue();
        result.ShouldBe(int.MinValue);
    }

    /// <summary>
    ///  Verifies conversion of a double at the maximum integer value.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenDoubleWithIntMaxValue()
    {
        ArgumentParser.TryConvertToInt((double)int.MaxValue, out int result).ShouldBeTrue();
        result.ShouldBe(int.MaxValue);
    }

    /// <summary>
    ///  Verifies rejection of a double below the minimum integer value.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenDoubleWithLessThanIntMinValue()
    {
        const double value = int.MinValue - 1.0;
        ArgumentParser.TryConvertToInt(value, out _).ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies rejection of a double above the maximum integer value.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenDoubleWithGreaterThanIntMaxValue()
    {
        const double Value = int.MaxValue + 1.0;
        ArgumentParser.TryConvertToInt(Value, out _).ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies rejection of a long above the maximum integer value.
    /// </summary>
    [Fact]
    public void TryConvertToIntGivenLongWithGreaterThanIntMaxValue()
    {
        const long Value = int.MaxValue + 1L;
        ArgumentParser.TryConvertToInt(Value, out _).ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies that a null argument cannot be converted to a long.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenNull()
        => ArgumentParser.TryConvertToLong(null, out _).ShouldBeFalse();

    /// <summary>
    ///  Verifies conversion of an integral double to a long.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenDouble()
    {
        ArgumentParser.TryConvertToLong(10.0D, out long result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of an argument that is already a long.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenLong()
    {
        ArgumentParser.TryConvertToLong(10L, out long result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of an integer to a long.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenInt()
    {
        ArgumentParser.TryConvertToLong(10, out long result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of a numeric string to a long.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenString()
    {
        ArgumentParser.TryConvertToLong("10", out long result).ShouldBeTrue();
        result.ShouldBe(10);
    }

    /// <summary>
    ///  Verifies conversion of a double at the minimum long value.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenDoubleWithLongMinValue()
    {
        ArgumentParser.TryConvertToLong((double)long.MinValue, out long result).ShouldBeTrue();
        result.ShouldBe(long.MinValue);
    }

    /// <summary>
    ///  Verifies that converting the rounded double representation of <see cref="long.MaxValue"/> does not throw.
    /// </summary>
    /// <remarks>
    ///  <see cref="System.Convert.ToInt64(double)"/> throws for values near the upper long boundary.
    ///  Argument conversion must handle that boundary without leaking an overflow exception.
    /// </remarks>
    [Fact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueShouldNotThrow()
        => Should.NotThrow(() =>
            ArgumentParser.TryConvertToLong((double)long.MaxValue, out _));

    /// <summary>
    ///  Verifies rejection of the rounded double representation of <see cref="long.MaxValue"/> on .NET Framework.
    /// </summary>
    /// <remarks>
    ///  Precision loss prevents the value from round-tripping from long to double to long.
    /// </remarks>
    [WindowsFullFrameworkOnlyFact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueFramework()
        => ArgumentParser.TryConvertToLong((double)long.MaxValue, out _).ShouldBeFalse();

    /// <summary>
    ///  Verifies conversion of the rounded double representation of <see cref="long.MaxValue"/> on .NET.
    /// </summary>
    /// <remarks>
    ///  The expected result differs from .NET Framework; this behavior was also observed on macOS 12 on Apple Silicon.
    /// </remarks>
    [DotNetOnlyFact]
    public void TryConvertToLongGivenDoubleWithLongMaxValueDotNet()
    {
        ArgumentParser.TryConvertToLong((double)long.MaxValue, out long result).ShouldBeTrue();
        result.ShouldBe(long.MaxValue);
    }

    /// <summary>
    ///  Verifies that a large long converted through double is accepted within the conversion tolerance.
    /// </summary>
    /// <remarks>
    ///  Precision loss changes the result even though the conversion succeeds.
    /// </remarks>
    [Fact]
    public void TryConvertToLongGivenDoubleWithVeryLargeLongValue()
    {
        const long VeryLargeLong = long.MaxValue - 512;
        ArgumentParser.TryConvertToLong((double)VeryLargeLong, out long result).ShouldBeTrue();
        result.ShouldBe(9223372036854774784L);
    }

    /// <summary>
    ///  Verifies rejection of a double below the minimum long value.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenDoubleWithLessThanLongMinValue()
        => ArgumentParser.TryConvertToLong(-92233720368547758081D, out _).ShouldBeFalse();

    /// <summary>
    ///  Verifies rejection of a double above the maximum long value.
    /// </summary>
    [Fact]
    public void TryConvertToLongGivenDoubleWithGreaterThanLongMaxValue()
    {
        const double value = (double)long.MaxValue + long.MaxValue;
        ArgumentParser.TryConvertToLong(value, out _).ShouldBeFalse();
    }

    /// <summary>
    ///  Verifies that a null argument cannot be converted to a double.
    /// </summary>
    [Fact]
    public void TryConvertToDoubleGivenNull()
        => ArgumentParser.TryConvertToDouble(null, out _).ShouldBeFalse();

    /// <summary>
    ///  Verifies conversion of an argument that is already a double.
    /// </summary>
    [Fact]
    public void TryConvertToDoubleGivenDouble()
    {
        ArgumentParser.TryConvertToDouble(10.0D, out double result).ShouldBeTrue();
        result.ShouldBe(10.0);
    }

    /// <summary>
    ///  Verifies conversion of a long to a double.
    /// </summary>
    [Fact]
    public void TryConvertToDoubleGivenLong()
    {
        ArgumentParser.TryConvertToDouble(10L, out double result).ShouldBeTrue();
        result.ShouldBe(10.0);
    }

    /// <summary>
    ///  Verifies conversion of an integer to a double.
    /// </summary>
    [Fact]
    public void TryConvertToDoubleGivenInt()
    {
        ArgumentParser.TryConvertToDouble(10, out double result).ShouldBeTrue();
        result.ShouldBe(10.0);
    }

    /// <summary>
    ///  Verifies conversion of a numeric string to a double.
    /// </summary>
    [Fact]
    public void TryConvertToDoubleGivenString()
    {
        ArgumentParser.TryConvertToDouble("10", out double result).ShouldBeTrue();
        result.ShouldBe(10.0);
    }

    /// <summary>
    ///  Verifies that string conversion uses invariant culture rather than the current culture's decimal separator.
    /// </summary>
    [Fact]
    public void TryConvertToDoubleGivenStringAndLocale()
    {
        Environment.SetCurrentCulture(CultureInfo.CreateSpecificCulture("en-ZA"));

        // English South Africa uses ',' as its decimal separator; invariant parsing treats it as a thousands separator.
        ArgumentParser.TryConvertToDouble("1,2", out double result).ShouldBeTrue();
        result.ShouldBe(12.0);
    }
}
