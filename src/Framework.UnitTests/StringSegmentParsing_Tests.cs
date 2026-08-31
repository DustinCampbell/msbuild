// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class StringSegmentParsing_Tests
{
    private const NumberStyles FloatingPointStyles = NumberStyles.Number | NumberStyles.Float;

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-0", 0)]
    [InlineData("+42", 42)]
    [InlineData(" -42 ", -42)]
    [InlineData("\t\r\n+42\f\v ", 42)]
    [InlineData("0000000000002147483647", int.MaxValue)]
    [InlineData("-0000000000002147483648", int.MinValue)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void Int32TryParse_ParsesIntegerSegments(string text, int expected)
    {
        StringSegment segment = CreateSegment(text);

        int.TryParse(segment, out int result).ShouldBeTrue();
        result.ShouldBe(expected);

        int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int styledResult).ShouldBeTrue();
        styledResult.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("+")]
    [InlineData("++1")]
    [InlineData("--1")]
    [InlineData("+-1")]
    [InlineData("4 2")]
    [InlineData("42-")]
    [InlineData("1.0")]
    [InlineData("0x2a")]
    [InlineData("\u00a042")]
    [InlineData("42\u00a0")]
    [InlineData("2147483648")]
    [InlineData("-2147483649")]
    public void Int32TryParse_RejectsInvalidIntegerSegments(string text)
    {
        StringSegment segment = CreateSegment(text);

        int.TryParse(segment, out int result).ShouldBeFalse();
        result.ShouldBe(0);

        int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int styledResult).ShouldBeFalse();
        styledResult.ShouldBe(0);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("+42", 42L)]
    [InlineData(" -42 ", -42L)]
    [InlineData("\t\r\n+42\f\v ", 42L)]
    [InlineData("000000000009223372036854775807", long.MaxValue)]
    [InlineData("-000000000009223372036854775808", long.MinValue)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    public void Int64TryParse_ParsesIntegerSegments(string text, long expected)
    {
        StringSegment segment = CreateSegment(text);

        long.TryParse(segment, out long result).ShouldBeTrue();
        result.ShouldBe(expected);

        long.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out long styledResult).ShouldBeTrue();
        styledResult.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("++1")]
    [InlineData("--1")]
    [InlineData("-+1")]
    [InlineData("4 2")]
    [InlineData("42+")]
    [InlineData("1.0")]
    [InlineData("0x2a")]
    [InlineData("\u00a042")]
    [InlineData("42\u00a0")]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    public void Int64TryParse_RejectsInvalidIntegerSegments(string text)
    {
        StringSegment segment = CreateSegment(text);

        long.TryParse(segment, out long result).ShouldBeFalse();
        result.ShouldBe(0);

        long.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out long styledResult).ShouldBeFalse();
        styledResult.ShouldBe(0);
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("+42", 42u)]
    [InlineData(" 42 ", 42u)]
    [InlineData("00000000004294967295", uint.MaxValue)]
    [InlineData("4294967295", uint.MaxValue)]
    public void UInt32TryParse_ParsesIntegerSegments(string text, uint expected)
    {
        StringSegment segment = CreateSegment(text);

        uint.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result).ShouldBeTrue();
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("4 2")]
    [InlineData("4294967296")]
    public void UInt32TryParse_RejectsInvalidIntegerSegments(string text)
    {
        StringSegment segment = CreateSegment(text);

        uint.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result).ShouldBeFalse();
        result.ShouldBe(0u);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-0.5")]
    [InlineData("+1.25E3")]
    [InlineData("1,234.5")]
    [InlineData(" NaN ")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("-0")]
    [InlineData("-0.0")]
    [InlineData("42-")]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    [InlineData("9223372036854775808")]
    [InlineData("1e500")]
    [InlineData("1.2.3")]
    [InlineData("--1")]
    public void DoubleTryParse_MatchesRuntimeStringParsing(string text)
    {
        StringSegment segment = CreateSegment(text);

        double.TryParse(segment, FloatingPointStyles, NumberFormatInfo.InvariantInfo, out double result)
            .ShouldBe(double.TryParse(text, FloatingPointStyles, NumberFormatInfo.InvariantInfo, out double expected));

        result.ShouldBe(expected);

        // Signed-zero and NaN representations are part of the runtime-specific parsing result.
        BitConverter.DoubleToInt64Bits(result).ShouldBe(BitConverter.DoubleToInt64Bits(expected));
    }

    [Fact]
    public void NumericTryParse_RejectsNullSegments()
    {
        StringSegment segment = default;

        int.TryParse(segment, out int intResult).ShouldBeFalse();
        intResult.ShouldBe(0);

        int.TryParse(segment, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out intResult).ShouldBeFalse();
        intResult.ShouldBe(0);

        long.TryParse(segment, out long longResult).ShouldBeFalse();
        longResult.ShouldBe(0);

        long.TryParse(segment, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out longResult).ShouldBeFalse();
        longResult.ShouldBe(0);

        uint.TryParse(segment, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out uint uintResult).ShouldBeFalse();
        uintResult.ShouldBe(0u);

        double.TryParse(segment, FloatingPointStyles, NumberFormatInfo.InvariantInfo, out double doubleResult).ShouldBeFalse();
        doubleResult.ShouldBe(0);
    }

    [Fact]
    public void IntegerTryParse_ParsesThousands()
    {
        StringSegment segment = CreateSegment("1,234");
        const NumberStyles style = NumberStyles.Integer | NumberStyles.AllowThousands;

        int.TryParse(segment, style, NumberFormatInfo.InvariantInfo, out int intResult).ShouldBeTrue();
        intResult.ShouldBe(1234);

        long.TryParse(segment, style, NumberFormatInfo.InvariantInfo, out long longResult).ShouldBeTrue();
        longResult.ShouldBe(1234);

        uint.TryParse(segment, style, NumberFormatInfo.InvariantInfo, out uint uintResult).ShouldBeTrue();
        uintResult.ShouldBe(1234u);
    }

    [Theory]
    [InlineData("10", 0x10)]
    [InlineData("7fffffff", int.MaxValue)]
    public void IntegerTryParse_ParsesHexadecimalSegments(string text, int expected)
    {
        StringSegment segment = CreateSegment(text);

        int.TryParse(segment, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out int intResult).ShouldBeTrue();
        intResult.ShouldBe(expected);

        long.TryParse(segment, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out long longResult).ShouldBeTrue();
        longResult.ShouldBe(expected);

        uint.TryParse(segment, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out uint uintResult).ShouldBeTrue();
        uintResult.ShouldBe((uint)expected);
    }

    [Theory]
    [InlineData("42", true, 42)]
    [InlineData(" 42 ", false, 0)]
    public void IntegerTryParse_HonorsNumberStylesNone(string input, bool success, int expected)
    {
        StringSegment digits = CreateSegment(input);

        int.TryParse(digits, NumberStyles.None, provider: null, out int intResult).ShouldBe(success);
        intResult.ShouldBe(expected);

        long.TryParse(digits, NumberStyles.None, provider: null, out long longResult).ShouldBe(success);
        longResult.ShouldBe(expected);

        uint.TryParse(digits, NumberStyles.None, provider: null, out uint uintResult).ShouldBe(success);
        uintResult.ShouldBe((uint)expected);
    }

    [Fact]
    public void NumericTryParse_RejectsInvalidStyles()
    {
        StringSegment segment = CreateSegment("42");
        const NumberStyles invalidStyle = NumberStyles.AllowHexSpecifier | NumberStyles.AllowLeadingSign;

        Should.Throw<ArgumentException>(
            () => int.TryParse(segment, invalidStyle, NumberFormatInfo.InvariantInfo, out _));
        Should.Throw<ArgumentException>(
            () => long.TryParse(segment, invalidStyle, NumberFormatInfo.InvariantInfo, out _));
        Should.Throw<ArgumentException>(
            () => uint.TryParse(segment, invalidStyle, NumberFormatInfo.InvariantInfo, out _));
        Should.Throw<ArgumentException>(
            () => double.TryParse(segment, invalidStyle, NumberFormatInfo.InvariantInfo, out _));
    }

    [Fact]
    public void NumericTryParse_UsesCultureInfoSigns()
    {
        CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "positive";
        culture.NumberFormat.NegativeSign = "negative";

        AssertProviderSigns(culture);
    }

    [Fact]
    public void NumericTryParse_UsesNumberFormatInfoSigns()
    {
        NumberFormatInfo numberFormat = (NumberFormatInfo)NumberFormatInfo.InvariantInfo.Clone();
        numberFormat.PositiveSign = "positive";
        numberFormat.NegativeSign = "negative";

        AssertProviderSigns(numberFormat);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4")]
    [InlineData(" 1 . +2 . 3 . 4 ")]
    [InlineData("1.2.")]
    [InlineData("1..2")]
    [InlineData("1.2.3.4.5")]
    [InlineData("-1.2")]
    [InlineData("1.-2")]
    [InlineData("2147483648.0")]
    [InlineData("1.a")]
    public void VersionTryParse_MatchesStringParsing(string text)
    {
        StringSegment segment = CreateSegment(text);

        Version.TryParse(segment, out Version? result)
            .ShouldBe(Version.TryParse(text, out Version? expected));

        result.ShouldBe(expected);
    }

    [Fact]
    public void VersionTryParse_RejectsNullSegment()
    {
        StringSegment segment = default;

        Version.TryParse(segment, out Version? result).ShouldBeFalse();
        result.ShouldBeNull();
    }

    private static void AssertProviderSigns(IFormatProvider provider)
    {
        StringSegment positive = CreateSegment("positive42");
        StringSegment negative = CreateSegment("negative42");

        int.TryParse(positive, NumberStyles.Integer, provider, out int intResult).ShouldBeTrue();
        intResult.ShouldBe(42);
        int.TryParse(negative, NumberStyles.Integer, provider, out intResult).ShouldBeTrue();
        intResult.ShouldBe(-42);

        long.TryParse(positive, NumberStyles.Integer, provider, out long longResult).ShouldBeTrue();
        longResult.ShouldBe(42);
        long.TryParse(negative, NumberStyles.Integer, provider, out longResult).ShouldBeTrue();
        longResult.ShouldBe(-42);

        uint.TryParse(positive, NumberStyles.Integer, provider, out uint uintResult).ShouldBeTrue();
        uintResult.ShouldBe(42u);
        uint.TryParse(negative, NumberStyles.Integer, provider, out uintResult).ShouldBeFalse();
        uintResult.ShouldBe(0u);

        double.TryParse(positive, FloatingPointStyles, provider, out double doubleResult).ShouldBeTrue();
        doubleResult.ShouldBe(42);
        double.TryParse(negative, FloatingPointStyles, provider, out doubleResult).ShouldBeTrue();
        doubleResult.ShouldBe(-42);
    }

    private static StringSegment CreateSegment(string text)
        => new($"prefix{text}suffix", 6, text.Length);
}
