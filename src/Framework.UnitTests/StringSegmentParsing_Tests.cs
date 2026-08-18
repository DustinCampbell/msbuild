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
    [InlineData(null, NumberStyles.Integer)]
    [InlineData("", NumberStyles.Integer)]
    [InlineData("0", NumberStyles.Integer)]
    [InlineData("-0", NumberStyles.Integer)]
    [InlineData("-000", NumberStyles.Integer)]
    [InlineData("+42", NumberStyles.Integer)]
    [InlineData("\t\r\n-42\f\v ", NumberStyles.Integer)]
    [InlineData("00000000004294967295", NumberStyles.Integer)]
    [InlineData("2147483647", NumberStyles.Integer)]
    [InlineData("-2147483648", NumberStyles.Integer)]
    [InlineData("2147483648", NumberStyles.Integer)]
    [InlineData("-2147483649", NumberStyles.Integer)]
    [InlineData("4294967295", NumberStyles.Integer)]
    [InlineData("4294967296", NumberStyles.Integer)]
    [InlineData("9223372036854775807", NumberStyles.Integer)]
    [InlineData("-9223372036854775808", NumberStyles.Integer)]
    [InlineData("9223372036854775808", NumberStyles.Integer)]
    [InlineData("-9223372036854775809", NumberStyles.Integer)]
    [InlineData("++1", NumberStyles.Integer)]
    [InlineData("4 2", NumberStyles.Integer)]
    [InlineData("\u00a042", NumberStyles.Integer)]
    [InlineData("42\u00a0", NumberStyles.Integer)]
    [InlineData("42\0", NumberStyles.Integer)]
    [InlineData("42\0x", NumberStyles.Integer)]
    [InlineData("42", NumberStyles.None)]
    [InlineData(" 42", NumberStyles.None)]
    [InlineData("42 ", NumberStyles.None)]
    [InlineData("+42", NumberStyles.None)]
    [InlineData(" 42", NumberStyles.AllowLeadingWhite)]
    [InlineData("42 ", NumberStyles.AllowLeadingWhite)]
    [InlineData("42 ", NumberStyles.AllowTrailingWhite)]
    [InlineData(" 42", NumberStyles.AllowTrailingWhite)]
    [InlineData("-42", NumberStyles.AllowLeadingSign)]
    [InlineData("42-", NumberStyles.AllowTrailingSign)]
    [InlineData("(42)", NumberStyles.AllowParentheses)]
    [InlineData("1,234", NumberStyles.Number)]
    [InlineData("42.0", NumberStyles.Number)]
    [InlineData("-0.0", NumberStyles.Number)]
    [InlineData("42.5", NumberStyles.Number)]
    [InlineData("1e2", NumberStyles.Float)]
    [InlineData("1e-2", NumberStyles.Float)]
    [InlineData("42", NumberStyles.Any)]
    [InlineData("$42", NumberStyles.Currency)]
    public void NumericTryParse_MatchesRuntimeStringParsing(string? text, NumberStyles style)
    {
        AssertNumericParsing(text, style, NumberFormatInfo.InvariantInfo);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("7fffffff")]
    [InlineData("80000000")]
    [InlineData("ffffffff")]
    [InlineData("ffffffffffffffff")]
    [InlineData("10000000000000000")]
    public void IntegerTryParse_MatchesRuntimeHexadecimalParsing(string text)
    {
        AssertIntegerParsing(text, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo);
    }

#if NET
    [Theory]
    [InlineData("10")]
    [InlineData("11111111111111111111111111111111")]
    [InlineData("2")]
    public void IntegerTryParse_MatchesRuntimeBinaryParsing(string text)
    {
        AssertIntegerParsing(text, NumberStyles.BinaryNumber, NumberFormatInfo.InvariantInfo);
    }
#endif

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("-0.0")]
    [InlineData("-0e0")]
    [InlineData("-0e-500")]
    [InlineData("1")]
    [InlineData("-42")]
    [InlineData("+1.25E3")]
    [InlineData("1,234.5")]
    [InlineData(" NaN ")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("2147483647")]
    [InlineData("-2147483648")]
    [InlineData("2147483648")]
    [InlineData("9007199254740991")]
    [InlineData("9007199254740992")]
    [InlineData("9007199254740993")]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    [InlineData("9223372036854775808")]
    [InlineData("1e500")]
    [InlineData("1e-500")]
    [InlineData("1.2.3")]
    [InlineData("--1")]
    public void DoubleTryParse_PreservesRuntimeRoundingAndSpecialValues(string? text)
    {
        AssertDoubleParsing(text, FloatingPointStyles, NumberFormatInfo.InvariantInfo);
    }

    [Theory]
    [InlineData("positive42")]
    [InlineData("negative42")]
    [InlineData("negative0")]
    [InlineData("positive1e2")]
    [InlineData("negative1,234.5")]
    public void NumericTryParse_UsesProviderSigns(string text)
    {
        CultureInfo culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "positive";
        culture.NumberFormat.NegativeSign = "negative";

        AssertNumericParsing(text, FloatingPointStyles, culture);
        AssertNumericParsing(text, FloatingPointStyles, culture.NumberFormat);
    }

    [Theory]
    [InlineData("1_234")]
    [InlineData("1_234,0")]
    [InlineData("1,5")]
    [InlineData("-0,0")]
    public void NumericTryParse_UsesProviderSeparators(string text)
    {
        NumberFormatInfo numberFormat = new()
        {
            NumberGroupSeparator = "_",
            NumberDecimalSeparator = ","
        };

        AssertNumericParsing(text, FloatingPointStyles, numberFormat);
    }

    [Theory]
    [InlineData("42 ", "42 ", "-")]
    [InlineData("42 1", "42 ", "-")]
    [InlineData("42 ", "+", "42 ")]
    [InlineData("x 42", "x\u00a0", "x ")]
    [InlineData("x 42", "x\u202f", "x ")]
    [InlineData("\0+42", "\0+", "-")]
    [InlineData("\0-42", "+", "\0-")]
    [InlineData("negative42", "negative", "negative")]
    public void NumericTryParse_PreservesRuntimeSignMatching(string text, string positiveSign, string negativeSign)
    {
        NumberFormatInfo numberFormat = new()
        {
            PositiveSign = positiveSign,
            NegativeSign = negativeSign
        };

        AssertNumericParsing(text, NumberStyles.Integer, numberFormat);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("4242")]
    [InlineData("42.0")]
    public void NumericTryParse_PreservesRuntimeCurrencyTokenMatching(string text)
    {
        NumberFormatInfo numberFormat = new() { CurrencySymbol = "42" };

        AssertNumericParsing(text, NumberStyles.Currency, numberFormat);
    }

    [Theory]
    [InlineData("42", NumberStyles.Integer)]
    [InlineData("1e2", NumberStyles.Float)]
    [InlineData("invalid", NumberStyles.Integer)]
    public void NumericTryParse_ResolvesCustomProviderOnce(string text, NumberStyles style)
    {
        StringSegment segment = CreateSegment(text);
        NumberFormatInfo numberFormat = NumberFormatInfo.InvariantInfo;
        CountingFormatProvider provider = new(numberFormat);

        int.TryParse(segment, style, provider, out int intResult)
            .ShouldBe(int.TryParse(text, style, numberFormat, out int expectedInt));
        intResult.ShouldBe(expectedInt);
        provider.CallCount.ShouldBe(1);

        long.TryParse(segment, style, provider, out long longResult)
            .ShouldBe(long.TryParse(text, style, numberFormat, out long expectedLong));
        longResult.ShouldBe(expectedLong);
        provider.CallCount.ShouldBe(2);

        uint.TryParse(segment, style, provider, out uint uintResult)
            .ShouldBe(uint.TryParse(text, style, numberFormat, out uint expectedUInt));
        uintResult.ShouldBe(expectedUInt);
        provider.CallCount.ShouldBe(3);

        double.TryParse(segment, style, provider, out double doubleResult)
            .ShouldBe(double.TryParse(text, style, numberFormat, out double expectedDouble));
        BitConverter.DoubleToInt64Bits(doubleResult).ShouldBe(BitConverter.DoubleToInt64Bits(expectedDouble));
        provider.CallCount.ShouldBe(4);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("-42")]
    [InlineData("1,234.5")]
    public void NumericTryParse_UsesCurrentCultureForNullProvider(string text)
    {
        AssertNumericParsing(text, FloatingPointStyles, provider: null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("42")]
    public void NumericTryParse_RejectsInvalidStyles(string? text)
    {
        StringSegment segment = CreateSegment(text);
        NumberStyles[] invalidStyles =
        [
            NumberStyles.AllowHexSpecifier | NumberStyles.AllowLeadingSign,
            (NumberStyles)(-1),
            (NumberStyles)0x40000000
        ];
        CountingFormatProvider provider = new(NumberFormatInfo.InvariantInfo);

        foreach (NumberStyles style in invalidStyles)
        {
            Should.Throw<ArgumentException>(() => int.TryParse(segment, style, provider, out _));
            Should.Throw<ArgumentException>(() => long.TryParse(segment, style, provider, out _));
            Should.Throw<ArgumentException>(() => uint.TryParse(segment, style, provider, out _));
            Should.Throw<ArgumentException>(() => double.TryParse(segment, style, provider, out _));
        }

        Should.Throw<ArgumentException>(() => double.TryParse(segment, NumberStyles.HexNumber, provider, out _));
#if NET
        Should.Throw<ArgumentException>(() => double.TryParse(segment, NumberStyles.BinaryNumber, provider, out _));
#endif
        provider.CallCount.ShouldBe(0);
    }

    private static void AssertNumericParsing(string? text, NumberStyles style, IFormatProvider? provider)
    {
        AssertIntegerParsing(text, style, provider);
        AssertDoubleParsing(text, style, provider);
    }

    private static void AssertIntegerParsing(string? text, NumberStyles style, IFormatProvider? provider)
    {
        StringSegment segment = CreateSegment(text);

        int.TryParse(segment, style, provider, out int intResult)
            .ShouldBe(int.TryParse(text, style, provider, out int expectedInt));
        intResult.ShouldBe(expectedInt);

        long.TryParse(segment, style, provider, out long longResult)
            .ShouldBe(long.TryParse(text, style, provider, out long expectedLong));
        longResult.ShouldBe(expectedLong);

        uint.TryParse(segment, style, provider, out uint uintResult)
            .ShouldBe(uint.TryParse(text, style, provider, out uint expectedUInt));
        uintResult.ShouldBe(expectedUInt);
    }

    private static void AssertDoubleParsing(string? text, NumberStyles style, IFormatProvider? provider)
    {
        StringSegment segment = CreateSegment(text);

        double.TryParse(segment, style, provider, out double result)
            .ShouldBe(double.TryParse(text, style, provider, out double expected));
        BitConverter.DoubleToInt64Bits(result).ShouldBe(BitConverter.DoubleToInt64Bits(expected));
    }

    private static StringSegment CreateSegment(string? text)
        => text is null ? default : new($"prefix{text}suffix", 6, text.Length);

    private sealed class CountingFormatProvider(NumberFormatInfo numberFormat) : IFormatProvider
    {
        public int CallCount { get; private set; }

        public object? GetFormat(Type? formatType)
        {
            CallCount++;
            return formatType == typeof(NumberFormatInfo) ? numberFormat : null;
        }
    }
}
