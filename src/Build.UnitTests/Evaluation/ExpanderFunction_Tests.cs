// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Engine.UnitTests.Evaluation;

public class ExpanderFunction_Tests
{
    /* Tests for FunctionArguments.TryGetInt32 */

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenNull()
    {
        CreateFunctionArguments(null).TryGetInt32(0, out int actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenDouble()
    {
        const double value = 10.0;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenLong()
    {
        const long value = 10;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenInt()
    {
        const int value = 10;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenString()
    {
        const string value = "10";
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenDoubleWithIntMinValue()
    {
        const int expected = int.MinValue;
        const double value = expected;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenDoubleWithIntMaxValue()
    {
        const int expected = int.MaxValue;
        const double value = expected;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenDoubleWithLessThanIntMinValue()
    {
        const double value = int.MinValue - 1.0;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenDoubleWithGreaterThanIntMaxValue()
    {
        const double value = int.MaxValue + 1.0;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt32GivenLongWithGreaterThanIntMaxValue()
    {
        const long value = int.MaxValue + 1L;
        CreateFunctionArguments(value).TryGetInt32(0, out int actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    /* Tests for FunctionArguments.TryGetInt64 */

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenNull()
    {
        CreateFunctionArguments(null).TryGetInt64(0, out long actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenDouble()
    {
        const double value = 10.0;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenLong()
    {
        const long value = 10;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenInt()
    {
        const int value = 10;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenString()
    {
        const string value = "10";
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeTrue();
        actual.ShouldBe(10);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithLongMinValue()
    {
        const long expected = long.MinValue;
        const double value = expected;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithLongMaxValueShouldNotThrow()
    {
        // An OverflowException should not be thrown.
        // Convert.ToInt64(double) has a defect and will throw an OverflowException
        // for values >= (long.MaxValue - 511) and <= long.MaxValue.
        _ = Should.NotThrow(() => CreateFunctionArguments((double)long.MaxValue).TryGetInt64(0, out _));
    }

    [WindowsFullFrameworkOnlyFact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithLongMaxValueFramework()
    {
        const long longMaxValue = long.MaxValue;
        bool result = CreateFunctionArguments((double)longMaxValue).TryGetInt64(0, out long actual);

        // Because of loss of precision, long.MaxValue will not 'round trip' from long to double to long.
        result.ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [DotNetOnlyFact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithLongMaxValueDotNet()
    {
        const long longMaxValue = long.MaxValue;
        bool result = CreateFunctionArguments((double)longMaxValue).TryGetInt64(0, out long actual);

        // Testing on macOS 12 on Apple Silicon M1 Pro produces different result.
        result.ShouldBeTrue();
        actual.ShouldBe(longMaxValue);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithVeryLargeLongValue()
    {
        // Because of loss of precision, veryLargeLong will not 'round trip' but within TryGetInt64
        // the double to long conversion will pass the tolerance test. Return will be true and veryLargeLong != expected.
        const long veryLargeLong = long.MaxValue - 512;
        const double value = veryLargeLong;
        const long expected = 9223372036854774784L;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeTrue();
        actual.ShouldBe(expected);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithLessThanLongMinValue()
    {
        const double value = -92233720368547758081D;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void FunctionArgumentsTryGetInt64GivenDoubleWithGreaterThanLongMaxValue()
    {
        const double value = (double)long.MaxValue + long.MaxValue;
        CreateFunctionArguments(value).TryGetInt64(0, out long actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    /* Tests for FunctionArguments.TryGetDouble */

    [Fact]
    public void FunctionArgumentsTryGetDoubleGivenNull()
    {
        CreateFunctionArguments(null).TryGetDouble(0, out double actual).ShouldBeFalse();
        actual.ShouldBe(0);
    }

    [Fact]
    public void FunctionArgumentsTryGetDoubleGivenDouble()
    {
        const double value = 10.0;
        CreateFunctionArguments(value).TryGetDouble(0, out double actual).ShouldBeTrue();
        actual.ShouldBe(10.0);
    }

    [Fact]
    public void FunctionArgumentsTryGetDoubleGivenLong()
    {
        const long value = 10;
        CreateFunctionArguments(value).TryGetDouble(0, out double actual).ShouldBeTrue();
        actual.ShouldBe(10.0);
    }

    [Fact]
    public void FunctionArgumentsTryGetDoubleGivenInt()
    {
        const int value = 10;
        CreateFunctionArguments(value).TryGetDouble(0, out double actual).ShouldBeTrue();
        actual.ShouldBe(10.0);
    }

    [Fact]
    public void FunctionArgumentsTryGetDoubleGivenString()
    {
        const string value = "10";
        CreateFunctionArguments(value).TryGetDouble(0, out double actual).ShouldBeTrue();
        actual.ShouldBe(10.0);
    }

    [Fact]
    public void FunctionArgumentsTryGetDoubleGivenStringAndLocale()
    {
        const string value = "1,2";

        Thread currentThread = Thread.CurrentThread;
        CultureInfo originalCulture = currentThread.CurrentCulture;

        try
        {
            // English South Africa locale uses ',' as decimal separator.
            // The invariant culture should be used and "1,2" should be 12.0 not 1.2.
            var cultureEnglishSouthAfrica = CultureInfo.CreateSpecificCulture("en-ZA");
            currentThread.CurrentCulture = cultureEnglishSouthAfrica;
            CreateFunctionArguments(value).TryGetDouble(0, out double actual).ShouldBeTrue();
            actual.ShouldBe(12.0);
        }
        finally
        {
            // Restore CultureInfo.
            currentThread.CurrentCulture = originalCulture;
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.DefaultThreadCurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void FunctionArgumentsParseLiteralSegment()
    {
        const string expression = "prefix123suffix";
        var arguments = new FunctionArguments(
            [expression.AsSegment(6, 3)],
            typeof(Math),
            nameof(Math.Max));

        arguments.TryGetInt32(0, out int value).ShouldBeTrue();
        value.ShouldBe(123);
        arguments.MaterializeAll().ShouldBe(["123"]);
    }

    [Fact]
    public void FunctionArgumentsParseEscapedLiteralSegment()
    {
        var arguments = new FunctionArguments(
            ["%34%32"],
            typeof(Math),
            nameof(Math.Max));

        arguments.TryGetInt32(0, out int value).ShouldBeTrue();
        value.ShouldBe(42);
    }

    [Theory]
    [InlineData(" -42 ")]
    [InlineData("%2d42")]
    public void FunctionArgumentsParseSignedIntegerSegment(string expression)
    {
        var arguments = new FunctionArguments(
            [expression.AsSegment()],
            typeof(Math),
            nameof(Math.Min));

        arguments.TryGetInt32(0, out int int32Value).ShouldBeTrue();
        int32Value.ShouldBe(-42);
        arguments.TryGetInt64(0, out long int64Value).ShouldBeTrue();
        int64Value.ShouldBe(-42);
        arguments.TryGetDouble(0, out double doubleValue).ShouldBeTrue();
        doubleValue.ShouldBe(-42);
    }

    [Fact]
    public void FunctionArgumentsPreserveExpandedTypedValue()
    {
        var arguments = new FunctionArguments(
            ["$(Value)"],
            typeof(Math),
            nameof(Math.Max));

        arguments.SetExpandedValue(0, 42L);

        arguments.TryGetInt32(0, out int value).ShouldBeTrue();
        value.ShouldBe(42);
        arguments.GetObject(0).ShouldBe(42L);
    }

    [Theory]
    [InlineData("x0y", 1, 1, '0')]
    [InlineData("x%30y", 1, 3, '0')]
    public void FunctionArgumentsParseCharSegment(string expression, int offset, int length, char expected)
    {
        var arguments = new FunctionArguments(
            [expression.AsSegment(offset, length)],
            typeof(string),
            nameof(string.PadLeft));

        arguments.TryGetChar(0, out char value).ShouldBeTrue();
        value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("CurrentCulture", StringComparison.CurrentCulture)]
    [InlineData("CurrentCultureIgnoreCase", StringComparison.CurrentCultureIgnoreCase)]
    [InlineData("InvariantCulture", StringComparison.InvariantCulture)]
    [InlineData("InvariantCultureIgnoreCase", StringComparison.InvariantCultureIgnoreCase)]
    [InlineData("Ordinal", StringComparison.Ordinal)]
    [InlineData("OrdinalIgnoreCase", StringComparison.OrdinalIgnoreCase)]
    [InlineData("StringComparison.OrdinalIgnoreCase", StringComparison.OrdinalIgnoreCase)]
    [InlineData("System.StringComparison.CurrentCulture", StringComparison.CurrentCulture)]
    [InlineData(" InvariantCultureIgnoreCase ", StringComparison.InvariantCultureIgnoreCase)]
    public void FunctionArgumentsParseStringComparisonSegment(string expression, StringComparison expected)
    {
        var arguments = new FunctionArguments(
            [$"prefix{expression}suffix".AsSegment(6, expression.Length)],
            typeof(string),
            nameof(string.Equals));

        arguments.TryGetStringComparison(0, out StringComparison value).ShouldBeTrue();
        value.ShouldBe(expected);
    }

    [Fact]
    public void FunctionArgumentsParseVersionSegment()
    {
        const string expression = "prefix1.2.3suffix";
        var arguments = new FunctionArguments(
            [expression.AsSegment(6, 5)],
            typeof(Version),
            nameof(Version.Parse));

        arguments.TryGetVersion(0, out Version? value).ShouldBeTrue();
        value.ShouldBe(new Version(1, 2, 3));
    }

    [Fact]
    public void FunctionArgumentsMaterializeObjectSegment()
    {
        const string expression = "prefixvaluesuffix";
        var arguments = new FunctionArguments(
            [expression.AsSegment(6, 5)],
            typeof(string),
            nameof(string.Concat));

        arguments.GetObject(0).ShouldBe("value");
    }

    [Fact]
    public void FunctionArgumentsPreserveNullAndEmptyArguments()
    {
        var arguments = new FunctionArguments(
            [default, StringSegment.Empty],
            typeof(string),
            nameof(string.Concat));

        arguments.GetObject(0).ShouldBeNull();
        arguments.GetObject(1).ShouldBe(string.Empty);
    }

    [Fact]
    public void FunctionArgumentsCacheMoreThanTwoExpandedValues()
    {
        var arguments = new FunctionArguments(
            ["$(A)", "$(B)", "$(C)"],
            typeof(string),
            nameof(string.Concat));

        arguments.SetExpandedValue(0, "first");
        arguments.SetExpandedValue(1, null);
        arguments.SetExpandedValue(2, "third");

        object[] values = arguments.MaterializeAll();
        values[0].ShouldBe("first");
        values[1].ShouldBeNull();
        values[2].ShouldBe("third");
    }

    private static FunctionArguments CreateFunctionArguments(object? value)
    {
        var arguments = new FunctionArguments(
            ["$(Value)"],
            typeof(Math),
            nameof(Math.Max));

        arguments.SetExpandedValue(0, value);
        return arguments;
    }
}
