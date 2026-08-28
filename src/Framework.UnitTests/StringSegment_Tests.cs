// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
#if NET
using System.Buffers;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class StringSegment_Tests
{
    // StringSplitOptions.TrimEntries (value 2) was introduced in .NET 5 and is absent from the .NET
    // Framework enum. Reference it by value so these tests build and run on every target.
    private const StringSplitOptions TrimEntries = (StringSplitOptions)2;

    // "hello world" embedded in a larger buffer so that Offset is non-zero. This is important for
    // verifying that search results, slices, and copies are reported relative to the segment, not the
    // underlying buffer.
    private const string HelloWorldBuffer = "[[hello world]]";

    private static StringSegment HelloWorld => new(HelloWorldBuffer, 2, 11);

    [Fact]
    public void Default_IsNullSegment()
    {
        StringSegment segment = default;

        segment.HasValue.ShouldBeFalse();
        segment.Buffer.ShouldBeNull();
        segment.Offset.ShouldBe(0);
        segment.Length.ShouldBe(0);
        segment.Value.ShouldBeNull();
        segment.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void ImplicitFromNullString_IsNullSegment()
    {
        StringSegment segment = (string?)null;

        segment.HasValue.ShouldBeFalse();
        segment.Value.ShouldBeNull();
    }

    [Fact]
    public void ImplicitFromString_CapturesWholeString()
    {
        StringSegment segment = "abc";

        segment.HasValue.ShouldBeTrue();
        segment.Buffer.ShouldBe("abc");
        segment.Offset.ShouldBe(0);
        segment.Length.ShouldBe(3);
        segment.Value.ShouldBe("abc");
        segment.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Empty_IsEmptyButNotNull()
    {
        StringSegment segment = StringSegment.Empty;

        segment.HasValue.ShouldBeTrue();
        segment.IsEmpty.ShouldBeTrue();
        segment.Length.ShouldBe(0);
        segment.Value.ShouldBe(string.Empty);
    }

    [Fact]
    public void SubSegment_ReflectsWindow()
    {
        StringSegment segment = HelloWorld;

        segment.Length.ShouldBe(11);
        segment.Offset.ShouldBe(2);
        segment.Value.ShouldBe("hello world");
        segment.AsSpan().ToString().ShouldBe("hello world");
        ReferenceEquals(segment.Buffer, HelloWorldBuffer).ShouldBeTrue();
    }

    [Theory]
    [InlineData(0, 'h')]
    [InlineData(4, 'o')]
    [InlineData(10, 'd')]
    public void Indexer_ReturnsSegmentRelativeCharacter(int index, char expected)
    {
        HelloWorld[index].ShouldBe(expected);
    }

    [Fact]
    public void Indexer_FromEnd_Works()
    {
        HelloWorld[^1].ShouldBe('d');
        HelloWorld[^11].ShouldBe('h');
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    [InlineData(100)]
    public void Indexer_OutOfRange_Throws(int index)
    {
        StringSegment segment = HelloWorld;
        Should.Throw<InternalErrorException>(() => { _ = segment[index]; });
    }

    [Fact]
    public void Constructor_InvalidOffsetOrLength_Throws()
    {
        Should.Throw<InternalErrorException>(() => new StringSegment("ab", 1, 5));
        Should.Throw<InternalErrorException>(() => new StringSegment("ab", -1, 1));
        Should.Throw<InternalErrorException>(() => new StringSegment("ab", 0, -1));
        Should.Throw<InternalErrorException>(() => new StringSegment("ab", 3, 0));
    }

    [Fact]
    public void Constructor_AcceptsRange()
    {
        const string buffer = "prefix-value";
        StringSegmentRange range = new(offset: 7, length: 5);

        StringSegment segment = new(buffer, range);

        segment.Buffer.ShouldBeSameAs(buffer);
        segment.Offset.ShouldBe(range.Offset);
        segment.Length.ShouldBe(range.Length);
        segment.Value.ShouldBe("value");
        new StringSegment(buffer, StringSegmentRange.Null).ShouldBe(default);
        new StringSegment(buffer: null, range).ShouldBe(default);
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(1, 2)]
    public void Constructor_RangeOutsideBuffer_Throws(int offset, int length)
    {
        StringSegmentRange range = new(offset, length);

        Should.Throw<InternalErrorException>(() => new StringSegment("ab", range));
    }

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
    public void Int32TryParse_ParsesInvariantIntegerSegments(string text, int expected)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);

        int.TryParse(segment, out int result).ShouldBeTrue();
        result.ShouldBe(expected);
        int.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int styledResult).ShouldBeTrue();
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
    public void Int32TryParse_RejectsInvalidInvariantIntegerSegments(string text)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);

        int.TryParse(segment, out int result).ShouldBeFalse();
        result.ShouldBe(0);
        int.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int styledResult).ShouldBeFalse();
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
    public void Int64TryParse_ParsesInvariantIntegerSegments(string text, long expected)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);

        long.TryParse(segment, out long result).ShouldBeTrue();
        result.ShouldBe(expected);
        long.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long styledResult).ShouldBeTrue();
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
    public void Int64TryParse_RejectsInvalidInvariantIntegerSegments(string text)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);

        long.TryParse(segment, out long result).ShouldBeFalse();
        result.ShouldBe(0);
        long.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out long styledResult).ShouldBeFalse();
        styledResult.ShouldBe(0);
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData(" 42 ", 42u)]
    [InlineData("00000000004294967295", uint.MaxValue)]
    [InlineData("4294967295", uint.MaxValue)]
    public void UInt32TryParse_ParsesIntegerSegments(string text, uint expected)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);

        uint.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out uint result).ShouldBeTrue();
        result.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("4 2")]
    [InlineData("4294967296")]
    public void UInt32TryParse_RejectsInvalidIntegerSegments(string text)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);

        uint.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out uint result).ShouldBeFalse();
        result.ShouldBe(0u);
    }

    [Fact]
    public void IntegerTryParse_RejectsNullSegments()
    {
        StringSegment segment = default;

        int.TryParse(segment, out int intResult).ShouldBeFalse();
        intResult.ShouldBe(0);
        int.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out intResult).ShouldBeFalse();
        intResult.ShouldBe(0);

        long.TryParse(segment, out long longResult).ShouldBeFalse();
        longResult.ShouldBe(0);
        long.TryParse(
            segment,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out longResult).ShouldBeFalse();
        longResult.ShouldBe(0);
    }

    [Fact]
    public void IntegerTryParse_FallsBackForOtherStyles()
    {
        StringSegment thousandsSegment = new("prefix1,234suffix", 6, 5);
        NumberStyles thousandsStyle = NumberStyles.Integer | NumberStyles.AllowThousands;

        int.TryParse(
            thousandsSegment,
            thousandsStyle,
            CultureInfo.InvariantCulture,
            out int intResult).ShouldBeTrue();
        intResult.ShouldBe(1234);
        long.TryParse(
            thousandsSegment,
            thousandsStyle,
            CultureInfo.InvariantCulture,
            out long longResult).ShouldBeTrue();
        longResult.ShouldBe(1234);

        StringSegment hexSegment = new("prefix7fffffffsuffix", 6, 8);
        int.TryParse(
            hexSegment,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out intResult).ShouldBeTrue();
        intResult.ShouldBe(int.MaxValue);
        long.TryParse(
            hexSegment,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out longResult).ShouldBeTrue();
        longResult.ShouldBe(int.MaxValue);

        StringSegment digitsSegment = new("prefix42suffix", 6, 2);
        int.TryParse(digitsSegment, NumberStyles.None, null, out intResult).ShouldBeTrue();
        intResult.ShouldBe(42);
        long.TryParse(digitsSegment, NumberStyles.None, null, out longResult).ShouldBeTrue();
        longResult.ShouldBe(42);

        StringSegment whitespaceSegment = new("prefix 42 suffix", 6, 4);
        int.TryParse(whitespaceSegment, NumberStyles.None, null, out intResult).ShouldBeFalse();
        intResult.ShouldBe(0);
        long.TryParse(whitespaceSegment, NumberStyles.None, null, out longResult).ShouldBeFalse();
        longResult.ShouldBe(0);
    }

    [Fact]
    public void IntegerTryParse_RejectsInvalidStyles()
    {
        StringSegment segment = new("prefix42suffix", 6, 2);
        NumberStyles invalidStyle = NumberStyles.AllowHexSpecifier | NumberStyles.AllowLeadingSign;

        Should.Throw<ArgumentException>(() =>
        {
            _ = int.TryParse(segment, invalidStyle, CultureInfo.InvariantCulture, out _);
        });
        Should.Throw<ArgumentException>(() =>
        {
            _ = long.TryParse(segment, invalidStyle, CultureInfo.InvariantCulture, out _);
        });
    }

    [Fact]
    public void IntegerTryParse_FallsBackForOtherProviders()
    {
        var numberFormat = new NumberFormatInfo { NegativeSign = "~" };
        StringSegment segment = new("prefix~42suffix", 6, 3);

        int.TryParse(segment, NumberStyles.Integer, numberFormat, out int intResult).ShouldBeTrue();
        intResult.ShouldBe(-42);
        long.TryParse(segment, NumberStyles.Integer, numberFormat, out long longResult).ShouldBeTrue();
        longResult.ShouldBe(-42);
    }

    [Fact]
    public void IntegerTryParse_AcceptsInvariantNumberFormatProvider()
    {
        StringSegment segment = new("prefix-42suffix", 6, 3);

        int.TryParse(
            segment,
            NumberStyles.Integer,
            NumberFormatInfo.InvariantInfo,
            out int intResult).ShouldBeTrue();
        intResult.ShouldBe(-42);
        long.TryParse(
            segment,
            NumberStyles.Integer,
            NumberFormatInfo.InvariantInfo,
            out long longResult).ShouldBeTrue();
        longResult.ShouldBe(-42);
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
    public void DoubleTryParse_MatchesStringOverload(string text)
    {
        const NumberStyles style = NumberStyles.Number | NumberStyles.Float;
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);
        bool expectedSuccess = double.TryParse(
            text,
            style,
            CultureInfo.InvariantCulture.NumberFormat,
            out double expected);

        bool success = double.TryParse(
            segment,
            style,
            CultureInfo.InvariantCulture.NumberFormat,
            out double result);

        success.ShouldBe(expectedSuccess);
        result.ShouldBe(expected);
        BitConverter.DoubleToInt64Bits(result).ShouldBe(BitConverter.DoubleToInt64Bits(expected));
    }

    [Fact]
    public void DoubleTryParse_RejectsNullSegment()
    {
        StringSegment segment = default;

        double.TryParse(
            segment,
            NumberStyles.Number | NumberStyles.Float,
            CultureInfo.InvariantCulture.NumberFormat,
            out double result).ShouldBeFalse();
        result.ShouldBe(0);
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
    public void VersionTryParse_MatchesStringOverload(string text)
    {
        StringSegment segment = new($"prefix{text}suffix", 6, text.Length);
        bool expectedSuccess = Version.TryParse(text, out Version? expected);

        bool success = Version.TryParse(segment, out Version? result);

        success.ShouldBe(expectedSuccess);
        result.ShouldBe(expected);
    }

    [Fact]
    public void VersionTryParse_RejectsNullSegment()
    {
        StringSegment segment = default;

        Version.TryParse(segment, out Version? result).ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("a", false)]
    [InlineData("hello", false)]
    public void IsNullOrEmpty_Works(string? value, bool expected)
    {
        StringSegment segment = value;

        segment.IsNullOrEmpty.ShouldBe(expected);
        if (!segment.IsNullOrEmpty)
        {
            segment.Buffer.Length.ShouldBe(segment.Length);
            segment.Value.Length.ShouldBe(segment.Length);
        }
    }

    [Fact]
    public void IsWhiteSpace_Works()
    {
        default(StringSegment).IsWhiteSpace().ShouldBeFalse();
        StringSegment.Empty.IsWhiteSpace().ShouldBeTrue();
        ((StringSegment)"   ").IsWhiteSpace().ShouldBeTrue();
        ((StringSegment)" \t\r\n\f\v ").IsWhiteSpace().ShouldBeTrue();
        ((StringSegment)"a").IsWhiteSpace().ShouldBeFalse();
        ((StringSegment)"  a  ").IsWhiteSpace().ShouldBeFalse();
        HelloWorld.IsWhiteSpace().ShouldBeFalse();

        // A whitespace window embedded in non-whitespace, so Offset/Length are honored.
        new StringSegment("ab   cd", 2, 3).IsWhiteSpace().ShouldBeTrue();
        new StringSegment("ab   cd", 1, 3).IsWhiteSpace().ShouldBeFalse();
    }

    [Fact]
    public void IsNullOrWhiteSpace_Works()
    {
        default(StringSegment).IsNullOrWhiteSpace().ShouldBeTrue();
        StringSegment.Empty.IsNullOrWhiteSpace().ShouldBeTrue();
        ((StringSegment)"   ").IsNullOrWhiteSpace().ShouldBeTrue();
        ((StringSegment)"a").IsNullOrWhiteSpace().ShouldBeFalse();

        StringSegment segment = "  a  ";
        segment.IsNullOrWhiteSpace().ShouldBeFalse();
        if (!segment.IsNullOrWhiteSpace())
        {
            segment.Buffer.Length.ShouldBe(5);
            segment.Value.Length.ShouldBe(5);
        }
    }

    [Fact]
    public void IsAscii_Works()
    {
        default(StringSegment).IsAscii().ShouldBeFalse();
        StringSegment.Empty.IsAscii().ShouldBeTrue();
        ((StringSegment)"hello world").IsAscii().ShouldBeTrue();
        ((StringSegment)"\0\u007f").IsAscii().ShouldBeTrue();
        ((StringSegment)"caf\u00e9").IsAscii().ShouldBeFalse();
        ((StringSegment)"\u0080").IsAscii().ShouldBeFalse();
        HelloWorld.IsAscii().ShouldBeTrue();

        // A non-ASCII character just outside an all-ASCII window must not be counted.
        new StringSegment("ab\u00e9cd", 0, 2).IsAscii().ShouldBeTrue();
        new StringSegment("ab\u00e9cd", 1, 2).IsAscii().ShouldBeFalse();
    }

    [Fact]
    public void AsSpan_Overloads_Window()
    {
        StringSegment segment = HelloWorld;

        segment.AsSpan().ToString().ShouldBe("hello world");
        segment.AsSpan(6).ToString().ShouldBe("world");
        segment.AsSpan(0, 5).ToString().ShouldBe("hello");
    }

    [Fact]
    public void AsMemory_Overloads_Window()
    {
        StringSegment segment = HelloWorld;

        segment.AsMemory().ToString().ShouldBe("hello world");
        segment.AsMemory(6).ToString().ShouldBe("world");
        segment.AsMemory(0, 5).ToString().ShouldBe("hello");
    }

    [Fact]
    public void Slice_ReWindows()
    {
        StringSegment segment = HelloWorld;

        segment.Slice(6).Value.ShouldBe("world");
        segment.Slice(0, 5).Value.ShouldBe("hello");
        ReferenceEquals(segment.Slice(6).Buffer, HelloWorldBuffer).ShouldBeTrue();
    }

    [Fact]
    public void RangeOperator_Works()
    {
        StringSegment segment = "hello";

        segment[1..3].Value.ShouldBe("el");
        segment[..2].Value.ShouldBe("he");
        segment[2..].Value.ShouldBe("llo");
        segment[^2..].Value.ShouldBe("lo");
    }

    [Fact]
    public void Enumerator_IteratesSegmentRelativeCharacters()
    {
        StringSegment segment = HelloWorld;

        List<char> chars = new();
        foreach (char c in segment)
        {
            chars.Add(c);
        }

        chars.ToArray().ShouldBe("hello world".ToCharArray());
    }

    [Fact]
    public void Enumerator_EmptySegment_YieldsNothing()
    {
        StringSegment segment = StringSegment.Empty;

        int count = 0;
        foreach (char _ in segment)
        {
            count++;
        }

        count.ShouldBe(0);
    }

    [Fact]
    public void Enumerator_NullSegment_YieldsNothing()
    {
        StringSegment segment = default;

        int count = 0;
        foreach (char _ in segment)
        {
            count++;
        }

        count.ShouldBe(0);
    }

    [Fact]
    public void Enumerator_MoveNextAndCurrent_Work()
    {
        StringSegment.Enumerator enumerator = new StringSegment("abc").GetEnumerator();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe('a');
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe('b');
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe('c');
        enumerator.MoveNext().ShouldBeFalse();
    }

    [Fact]
    public void Enumerator_Reset_RestartsIteration()
    {
        StringSegment.Enumerator enumerator = new StringSegment("ab").GetEnumerator();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.MoveNext().ShouldBeFalse();

        enumerator.Reset();

        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe('a');
    }

    [Fact]
    public void Slice_OutOfRange_Throws()
    {
        StringSegment segment = "hello";

        Should.Throw<InternalErrorException>(() => segment.Slice(6));
        Should.Throw<InternalErrorException>(() => segment.Slice(2, 5));
    }

    [Fact]
    public void Slice_NullSegment_ReturnsSelf()
    {
        StringSegment segment = default;

        segment.Slice(0).HasValue.ShouldBeFalse();
        segment.Slice(0, 0).HasValue.ShouldBeFalse();
    }

    [Fact]
    public void Equals_OrdinalByDefault()
    {
        ((StringSegment)"abc").Equals((StringSegment)"abc").ShouldBeTrue();
        ((StringSegment)"abc").Equals((StringSegment)"abd").ShouldBeFalse();
        ((StringSegment)"abc").Equals((StringSegment)"ABC").ShouldBeFalse();
    }

    [Fact]
    public void Equals_SubSegment_ComparesContent()
    {
        StringSegment fromBuffer = new("xxhello worldyy", 2, 11);

        fromBuffer.Equals((StringSegment)"hello world").ShouldBeTrue();
        fromBuffer.Equals("hello world").ShouldBeTrue();
    }

    [Theory]
    [InlineData(StringComparison.Ordinal)]
    [InlineData(StringComparison.OrdinalIgnoreCase)]
    public void Equals_SubSegmentsWithOffsets_UsesBothOffsets(StringComparison comparison)
    {
        StringSegment left = new("aaXYZbb", 2, 3);
        StringSegment right = new("11XYZ22", 2, 3);

        left.Equals(right, comparison).ShouldBeTrue();
        right.Equals(left, comparison).ShouldBeTrue();
        StringSegment.Equals(left, right, comparison).ShouldBeTrue();
    }

    [Theory]
    [InlineData("abc", "ABC", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("abc", "ABC", StringComparison.Ordinal, false)]
    [InlineData("abc", "abc", StringComparison.Ordinal, true)]
    public void Equals_WithComparison(string left, string right, StringComparison comparison, bool expected)
    {
        ((StringSegment)left).Equals((StringSegment)right, comparison).ShouldBe(expected);
        StringSegment.Equals(left, right, comparison).ShouldBe(expected);
    }

    [Fact]
    public void Equals_Span_WithComparison()
    {
        HelloWorld.Equals("hello world".AsSpan()).ShouldBeTrue();
        HelloWorld.Equals("HELLO WORLD".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.Equals("HELLO WORLD".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
        ((StringSegment)"\u00c5").Equals("A\u030a".AsSpan(), StringComparison.InvariantCulture).ShouldBeTrue();
        default(StringSegment).Equals(ReadOnlySpan<char>.Empty).ShouldBeTrue();
    }

    [Fact]
    public void Equals_NullAndEmpty_AreDistinct()
    {
        StringSegment nullSegment = default;
        StringSegment emptySegment = StringSegment.Empty;

        nullSegment.Equals(nullSegment).ShouldBeTrue();
        emptySegment.Equals(emptySegment).ShouldBeTrue();
        nullSegment.Equals(emptySegment).ShouldBeFalse();

        // A null segment equals a null string, but an empty segment does not.
        nullSegment.Equals((string?)null).ShouldBeTrue();
        emptySegment.Equals((string?)null).ShouldBeFalse();
        emptySegment.Equals(string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void Equals_Object()
    {
        object boxed = (StringSegment)"abc";

        ((StringSegment)"abc").Equals(boxed).ShouldBeTrue();
        ((StringSegment)"abc").Equals((object)"abc").ShouldBeFalse(); // a string is not a StringSegment
        ((StringSegment)"abc").Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void Operators_EqualityAndInequality()
    {
        (((StringSegment)"abc") == ((StringSegment)"abc")).ShouldBeTrue();
        (((StringSegment)"abc") != ((StringSegment)"abd")).ShouldBeTrue();
        (default(StringSegment) == default).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_EqualForEqualSegments()
    {
        StringSegment fromBuffer = new("xxhello worldyy", 2, 11);
        StringSegment direct = "hello world";

        fromBuffer.GetHashCode().ShouldBe(direct.GetHashCode());
    }

    [Fact]
    public void GetHashCode_NullSegment_IsStable()
    {
        // The concrete hash of a null/empty segment differs by target framework, but it must never throw
        // and must be consistent for equal segments.
        StringSegment first = default;
        StringSegment second = default;

        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Theory]
    [InlineData("abc", "abc", 0)]
    [InlineData("abc", "abd", -1)]
    [InlineData("abd", "abc", 1)]
    [InlineData("ab", "abc", -1)]
    public void Compare_Ordinal(string left, string right, int expectedSign)
    {
        Math.Sign(StringSegment.Compare(left, right, StringComparison.Ordinal))
            .ShouldBe(expectedSign);
    }

    [Fact]
    public void Compare_NullSortsFirst()
    {
        Math.Sign(StringSegment.Compare(default, "a", StringComparison.Ordinal)).ShouldBe(-1);
        Math.Sign(StringSegment.Compare("a", default, StringComparison.Ordinal)).ShouldBe(1);
        StringSegment.Compare(default, default, StringComparison.Ordinal).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_Overloads()
    {
        Math.Sign(((StringSegment)"abc").CompareTo((StringSegment)"abd")).ShouldBe(-1);
        Math.Sign(((StringSegment)"abc").CompareTo("abd")).ShouldBe(-1);
        ((StringSegment)"abc").CompareTo((StringSegment)"abc").ShouldBe(0);
        Math.Sign(((StringSegment)"ABC").CompareTo("abc", StringComparison.OrdinalIgnoreCase)).ShouldBe(0);
    }

    [Fact]
    public void ComparisonMethods_RejectInvalidComparison()
    {
        const StringComparison InvalidComparison = (StringComparison)(-1);

        Should.Throw<InternalErrorException>(() => HelloWorld.Equals(HelloWorld, InvalidComparison));
        Should.Throw<InternalErrorException>(() => HelloWorld.Equals(HelloWorldBuffer, InvalidComparison));
        Should.Throw<InternalErrorException>(() => StringSegment.Compare(default, default, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).IndexOf(string.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).IndexOf(StringSegment.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).LastIndexOf(string.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).LastIndexOf(StringSegment.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).LastIndexOf(ReadOnlySpan<char>.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).StartsWith(string.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).StartsWith(StringSegment.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).EndsWith(string.Empty, InvalidComparison));
        Should.Throw<InternalErrorException>(() => default(StringSegment).EndsWith(StringSegment.Empty, InvalidComparison));

        Should.Throw<ArgumentException>(() => HelloWorld.Equals("hello world".AsSpan(), InvalidComparison));
        Should.Throw<ArgumentException>(() => HelloWorld.IndexOf("world".AsSpan(), InvalidComparison));
        Should.Throw<ArgumentException>(() => HelloWorld.LastIndexOf("world".AsSpan(), InvalidComparison));
        Should.Throw<ArgumentException>(() => HelloWorld.StartsWith("hello".AsSpan(), InvalidComparison));
        Should.Throw<ArgumentException>(() => HelloWorld.EndsWith("world".AsSpan(), InvalidComparison));
    }

    [Theory]
    [InlineData('h', 0)]
    [InlineData('o', 4)]
    [InlineData('d', 10)]
    [InlineData('z', -1)]
    public void IndexOf_Char_IsSegmentRelative(char value, int expected)
    {
        HelloWorld.IndexOf(value).ShouldBe(expected);
    }

    [Fact]
    public void IndexOf_Char_WithStart()
    {
        HelloWorld.IndexOf('o', 5).ShouldBe(7);
        HelloWorld.IndexOf('o', 8).ShouldBe(-1);
    }

    [Theory]
    [InlineData("world", 6)]
    [InlineData("hello", 0)]
    [InlineData("missing", -1)]
    public void IndexOf_String(string value, int expected)
    {
        HelloWorld.IndexOf(value).ShouldBe(expected);
    }

    [Fact]
    public void IndexOf_String_IgnoreCase()
    {
        HelloWorld.IndexOf("WORLD", StringComparison.OrdinalIgnoreCase).ShouldBe(6);
    }

    [Fact]
    public void IndexOf_Span()
    {
        HelloWorld.IndexOf("world".AsSpan()).ShouldBe(6);
        default(StringSegment).IndexOf(ReadOnlySpan<char>.Empty).ShouldBe(0);
        default(StringSegment).Contains(ReadOnlySpan<char>.Empty).ShouldBeTrue();
    }

    [Fact]
    public void IndexOf_Span_WithComparisonAndRange()
    {
        HelloWorld.IndexOf("WORLD".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBe(6);
        HelloWorld.IndexOf("O".AsSpan(), 5, StringComparison.OrdinalIgnoreCase).ShouldBe(7);
        HelloWorld.IndexOf("O".AsSpan(), 3, 3, StringComparison.OrdinalIgnoreCase).ShouldBe(4);
        HelloWorld.IndexOf("WORLD".AsSpan(), 0, 5, StringComparison.OrdinalIgnoreCase).ShouldBe(-1);
    }

    [Fact]
    public void SpanSearch_CultureSensitiveComparisonHandlesDifferentUtf16Lengths()
    {
        StringSegment segment = new("[[x\u00c5y\u00c5z]]", 2, 5);
        ReadOnlySpan<char> value = "A\u030a".AsSpan();

        segment.Contains(value, StringComparison.InvariantCulture).ShouldBeTrue();
        segment.IndexOf(value, StringComparison.InvariantCulture).ShouldBe(1);
        segment.IndexOf(value, 2, 3, StringComparison.InvariantCulture).ShouldBe(3);
        segment.LastIndexOf(value, StringComparison.InvariantCulture).ShouldBe(3);
        segment.LastIndexOf(value, 2, 3, StringComparison.InvariantCulture).ShouldBe(1);
    }

    [Theory]
    [InlineData('o', 7)]
    [InlineData('l', 9)]
    [InlineData('h', 0)]
    [InlineData('z', -1)]
    public void LastIndexOf_Char(char value, int expected)
    {
        HelloWorld.LastIndexOf(value).ShouldBe(expected);
    }

    [Fact]
    public void LastIndexOf_Char_WithStartAndLength()
    {
        HelloWorld.LastIndexOf('o', 10).ShouldBe(7);
        HelloWorld.LastIndexOf('o', 6).ShouldBe(4);
        HelloWorld.LastIndexOf('o', 10, 4).ShouldBe(7);
        HelloWorld.LastIndexOf('o', 6, 2).ShouldBe(-1);
        HelloWorld.LastIndexOf('h', 0, 1).ShouldBe(0);
        HelloWorld.LastIndexOf('h', 11, 0).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_String()
    {
        HelloWorld.LastIndexOf("o").ShouldBe(7);
        HelloWorld.LastIndexOf("l").ShouldBe(9);
        HelloWorld.LastIndexOf(string.Empty).ShouldBe(11);
        StringSegment.Empty.LastIndexOf(string.Empty).ShouldBe(0);
        HelloWorld.Slice(1, 0).LastIndexOf(string.Empty).ShouldBe(0);
    }

    [Fact]
    public void LastIndexOf_String_WithStartAndLength()
    {
        HelloWorld.LastIndexOf("o", 10).ShouldBe(7);
        HelloWorld.LastIndexOf("o", 6).ShouldBe(4);
        HelloWorld.LastIndexOf("world", 10, 5).ShouldBe(6);
        HelloWorld.LastIndexOf("o", 6, 2).ShouldBe(-1);
        HelloWorld.LastIndexOf("d", 11, 1).ShouldBe(-1);
        HelloWorld.LastIndexOf("d", 11, 2).ShouldBe(10);
        HelloWorld.LastIndexOf("HELLO", 4, 5, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
    }

    [Fact]
    public void LastIndexOf_String_EmptyValue_WithStartAndLength()
    {
        HelloWorld.LastIndexOf(string.Empty, 10).ShouldBe(11);
        HelloWorld.LastIndexOf(string.Empty, 7, 3).ShouldBe(8);
        HelloWorld.LastIndexOf(string.Empty, 11, 0).ShouldBe(11);
        StringSegment.Empty.LastIndexOf(string.Empty, 0, 1).ShouldBe(0);
        HelloWorld.Slice(1, 0).LastIndexOf(string.Empty, 0, 1).ShouldBe(0);
    }

    [Fact]
    public void LastIndexOf_Span()
    {
        HelloWorld.LastIndexOf("o".AsSpan()).ShouldBe(7);
        default(StringSegment).LastIndexOf(ReadOnlySpan<char>.Empty).ShouldBe(0);
    }

    [Fact]
    public void LastIndexOf_Span_WithComparisonAndRange()
    {
        HelloWorld.LastIndexOf("O".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBe(7);
        HelloWorld.LastIndexOf("O".AsSpan(), 6, StringComparison.OrdinalIgnoreCase).ShouldBe(4);
        HelloWorld.LastIndexOf("O".AsSpan(), 10, 5, StringComparison.OrdinalIgnoreCase).ShouldBe(7);
        HelloWorld.LastIndexOf("O".AsSpan(), 6, 2, StringComparison.OrdinalIgnoreCase).ShouldBe(-1);
        HelloWorld.LastIndexOf(ReadOnlySpan<char>.Empty, 7, 3).ShouldBe(8);
    }

    [Fact]
    public void IndexOfAny_CharArray()
    {
        HelloWorld.IndexOfAny(new[] { 'w', 'r' }).ShouldBe(6);
        HelloWorld.IndexOfAny(new[] { 'z', 'q' }).ShouldBe(-1);
    }

    [Fact]
    public void IndexOfAny_TwoAndThreeChars()
    {
        HelloWorld.IndexOfAny('w', 'o').ShouldBe(4);
        HelloWorld.IndexOfAny('z', 'w', 'r').ShouldBe(6);
    }

    [Fact]
    public void IndexOfAny_Span_DispatchesBySize()
    {
        HelloWorld.IndexOfAny("o".AsSpan()).ShouldBe(4);
        HelloWorld.IndexOfAny("wo".AsSpan()).ShouldBe(4);
        HelloWorld.IndexOfAny("zwr".AsSpan()).ShouldBe(6);
        HelloWorld.IndexOfAny("zqxw".AsSpan()).ShouldBe(6);
    }

    [Fact]
    public void IndexOfAny_Span_WithStartAndLength()
    {
        HelloWorld.IndexOfAny("wo".AsSpan(), 5).ShouldBe(6);
        HelloWorld.IndexOfAny("wo".AsSpan(), 7).ShouldBe(7);
        HelloWorld.IndexOfAny("wo".AsSpan(), 5, 2).ShouldBe(6);
        HelloWorld.IndexOfAny("wo".AsSpan(), 8, 3).ShouldBe(-1);
        HelloWorld.IndexOfAny(ReadOnlySpan<char>.Empty, 5, 2).ShouldBe(-1);
    }

#if NET
    [Fact]
    public void IndexOfAny_SearchValues()
    {
        SearchValues<char> values = SearchValues.Create("ow");

        HelloWorld.IndexOfAny(values).ShouldBe(4);
        HelloWorld.IndexOfAny(values, 5).ShouldBe(6);
        HelloWorld.IndexOfAny(values, 7, 2).ShouldBe(7);
        HelloWorld.IndexOfAny(values, 8, 3).ShouldBe(-1);
        HelloWorld.IndexOfAny(SearchValues.Create("zq")).ShouldBe(-1);
    }

#endif

    [Fact]
    public void LastIndexOfAny_Variants()
    {
        HelloWorld.LastIndexOfAny(new[] { 'o', 'l' }).ShouldBe(9);
        HelloWorld.LastIndexOfAny('o', 'l').ShouldBe(9);
        HelloWorld.LastIndexOfAny('o', 'l', 'h').ShouldBe(9);
        HelloWorld.LastIndexOfAny("ol".AsSpan()).ShouldBe(9);
    }

    [Fact]
    public void LastIndexOfAny_Span_WithStartAndLength()
    {
        HelloWorld.LastIndexOfAny("ol".AsSpan(), 10).ShouldBe(9);
        HelloWorld.LastIndexOfAny("ol".AsSpan(), 6).ShouldBe(4);
        HelloWorld.LastIndexOfAny("ol".AsSpan(), 10, 4).ShouldBe(9);
        HelloWorld.LastIndexOfAny("ol".AsSpan(), 6, 2).ShouldBe(-1);
        HelloWorld.LastIndexOfAny(ReadOnlySpan<char>.Empty, 10, 4).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOfAny_CharArray_WithStartAndLength()
    {
        char[] values = ['o', 'l'];

        HelloWorld.LastIndexOfAny(values, 10).ShouldBe(9);
        HelloWorld.LastIndexOfAny(values, 6).ShouldBe(4);
        HelloWorld.LastIndexOfAny(values, 10, 4).ShouldBe(9);
        HelloWorld.LastIndexOfAny(values, 6, 2).ShouldBe(-1);
        HelloWorld.LastIndexOfAny(['h'], 0, 1).ShouldBe(0);
        HelloWorld.LastIndexOfAny(values, 11, 0).ShouldBe(-1);
    }

#if NET
    [Fact]
    public void LastIndexOfAny_SearchValues()
    {
        SearchValues<char> values = SearchValues.Create("ol");

        HelloWorld.LastIndexOfAny(values).ShouldBe(9);
        HelloWorld.LastIndexOfAny(values, 6).ShouldBe(4);
        HelloWorld.LastIndexOfAny(values, 10, 4).ShouldBe(9);
        HelloWorld.LastIndexOfAny(values, 6, 2).ShouldBe(-1);
        HelloWorld.LastIndexOfAny(SearchValues.Create("zq")).ShouldBe(-1);
    }

#endif

    [Fact]
    public void Contains_Variants()
    {
        HelloWorld.Contains('h').ShouldBeTrue();
        HelloWorld.Contains('z').ShouldBeFalse();
        HelloWorld.Contains("world").ShouldBeTrue();
        HelloWorld.Contains("WORLD", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.Contains("lo".AsSpan()).ShouldBeTrue();
        HelloWorld.Contains("WORLD".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void ContainsAny_Variants()
    {
        HelloWorld.ContainsAny(new[] { 'z', 'd' }).ShouldBeTrue();
        HelloWorld.ContainsAny('z', 'q').ShouldBeFalse();
        HelloWorld.ContainsAny('z', 'q', 'h').ShouldBeTrue();
        HelloWorld.ContainsAny("zq".AsSpan()).ShouldBeFalse();
    }

    [Fact]
    public void Trim_Whitespace()
    {
        ((StringSegment)"  hi  ").Trim().Value.ShouldBe("hi");
        ((StringSegment)"  hi  ").TrimStart().Value.ShouldBe("hi  ");
        ((StringSegment)"  hi  ").TrimEnd().Value.ShouldBe("  hi");
    }

    [Fact]
    public void Trim_SingleChar()
    {
        ((StringSegment)"xxhixx").Trim('x').Value.ShouldBe("hi");
        ((StringSegment)"xxhixx").TrimStart('x').Value.ShouldBe("hixx");
        ((StringSegment)"xxhixx").TrimEnd('x').Value.ShouldBe("xxhi");
    }

    [Fact]
    public void Trim_MultipleChars()
    {
        ((StringSegment)"xyhixy").Trim('x', 'y').Value.ShouldBe("hi");
        ((StringSegment)"xyhixy").Trim(new[] { 'x', 'y' }).Value.ShouldBe("hi");
        ((StringSegment)"xyhixy").Trim(['x', 'y']).Value.ShouldBe("hi");
    }

    [Fact]
    public void Trim_AllTrimChars_ProducesEmpty()
    {
        StringSegment result = ((StringSegment)"xxxx").Trim('x');

        result.IsEmpty.ShouldBeTrue();
        result.Length.ShouldBe(0);
    }

    [Fact]
    public void Trim_EmptySet_DoesNothing()
    {
        ((StringSegment)"  hi  ").Trim(default(ReadOnlySpan<char>)).Value.ShouldBe("  hi  ");
    }

    [Fact]
    public void Trim_ReturnsViewOverSameBuffer()
    {
        string source = string.Concat("  ", "hi", "  ");
        StringSegment trimmed = ((StringSegment)source).Trim();

        ReferenceEquals(trimmed.Buffer, source).ShouldBeTrue();
        trimmed.Offset.ShouldBe(2);
        trimmed.Length.ShouldBe(2);
        trimmed.Value.ShouldBe("hi");
    }

    [Fact]
    public void Trim_OnSubSegment()
    {
        StringSegment segment = new("[[  hi  ]]", 2, 6); // "  hi  "
        segment.Trim().Value.ShouldBe("hi");
    }

    [Fact]
    public void CopyTo_Span()
    {
        Span<char> destination = new char[11];
        HelloWorld.CopyTo(destination);
        destination.ToString().ShouldBe("hello world");
    }

    [Fact]
    public void TryCopyTo_Span()
    {
        Span<char> big = new char[11];
        HelloWorld.TryCopyTo(big).ShouldBeTrue();
        big.ToString().ShouldBe("hello world");

        Span<char> small = new char[3];
        HelloWorld.TryCopyTo(small).ShouldBeFalse();
    }

    [Fact]
    public void CopyTo_CharArray_IsSegmentRelative()
    {
        char[] destination = new char[5];
        HelloWorld.CopyTo(0, destination, 0, 5);
        new string(destination).ShouldBe("hello");

        char[] destination2 = new char[5];
        HelloWorld.CopyTo(6, destination2, 0, 5);
        new string(destination2).ShouldBe("world");
    }

    [Fact]
    public void CopyTo_CharArray_InvalidRange_Throws()
    {
        Should.Throw<InternalErrorException>(() => HelloWorld.CopyTo(0, new char[5], 0, 100));
    }

    [Fact]
    public void Join_Char_ParamsSpan()
    {
        StringSegment.Join(',', "a", "b", "c").ShouldBe("a,b,c");
    }

    [Fact]
    public void Join_String_ParamsSpan()
    {
        StringSegment.Join("--", "a", "b", "c").ShouldBe("a--b--c");
    }

    [Fact]
    public void Join_SingleValue_HasNoSeparator()
    {
        string value = "only";

        string result = StringSegment.Join(',', value);

        result.ShouldBe(value);
        ReferenceEquals(result, value).ShouldBeTrue();
    }

    [Fact]
    public void Join_Empty_ReturnsEmpty()
    {
        StringSegment.Join(',', default(ReadOnlySpan<StringSegment>)).ShouldBe(string.Empty);
    }

    [Fact]
    public void Join_UsesSegmentWindow()
    {
        StringSegment.Join(',', HelloWorld, "x").ShouldBe("hello world,x");
    }

    [Fact]
    public void Join_Enumerable_List()
    {
        List<StringSegment> values = ["a", "b", "c"];

        StringSegment.Join(',', values).ShouldBe("a,b,c");
        StringSegment.Join("--", values).ShouldBe("a--b--c");
    }

    [Fact]
    public void Join_Enumerable_ArrayFastPath()
    {
        StringSegment[] values = ["a", "b", "c"];

        StringSegment.Join(',', values).ShouldBe("a,b,c");
        StringSegment.Join(";;", values).ShouldBe("a;;b;;c");
    }

    [Fact]
    public void Join_StringSeparator_SingleChar_MatchesCharOverload()
    {
        StringSegment[] values = ["a", "b"];

        StringSegment.Join(",", values).ShouldBe("a,b");
        StringSegment.Join(",", values).ShouldBe("a,b");
    }

    [Fact]
    public void Join_EmptySeparator_Concatenates()
    {
        StringSegment.Join(string.Empty, "a", "b", "c").ShouldBe("abc");
    }

    [Fact]
    public void Join_WindowedSegment_InNonFirstPosition()
    {
        StringSegment.Join('|', "x", HelloWorld).ShouldBe("x|hello world");
        StringSegment.Join("--", "x", HelloWorld).ShouldBe("x--hello world");
    }

    [Fact]
    public void Join_EmptyFirstSegment()
    {
        StringSegment.Join(',', "", "b").ShouldBe(",b");
        StringSegment.Join("--", "", "b").ShouldBe("--b");
    }

    [Fact]
    public void Join_EmptySegmentInMiddle()
    {
        StringSegment.Join(',', "a", "", "c").ShouldBe("a,,c");
        StringSegment.Join("--", "a", "", "c").ShouldBe("a----c");
    }

    [Fact]
    public void Join_SingleValue_StringAndNoSeparator()
    {
        string value = "only";
        List<StringSegment> enumerable = [value];

        string stringSeparatorResult = StringSegment.Join("--", value);
        string noSeparatorResult = StringSegment.Join(string.Empty, value);
        string charEnumerableResult = StringSegment.Join(',', enumerable);
        string enumerableResult = StringSegment.Join("--", enumerable);
        string noSeparatorEnumerableResult = StringSegment.Join(string.Empty, enumerable);

        ReferenceEquals(stringSeparatorResult, value).ShouldBeTrue();
        ReferenceEquals(noSeparatorResult, value).ShouldBeTrue();
        ReferenceEquals(charEnumerableResult, value).ShouldBeTrue();
        ReferenceEquals(enumerableResult, value).ShouldBeTrue();
        ReferenceEquals(noSeparatorEnumerableResult, value).ShouldBeTrue();
    }

    private static List<string> Split(StringSegment segment, char separator, StringSplitOptions options = StringSplitOptions.None)
    {
        List<string> result = [];
        foreach (StringSegment piece in segment.Split(separator, options))
        {
            result.Add(piece.ValueOrEmpty);
        }

        return result;
    }

    [Fact]
    public void Split_Char_Basic()
    {
        Split("a,b,c", ',').ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Split_Char_NoSeparator_YieldsWhole()
    {
        Split("abc", ',').ShouldBe(["abc"]);
    }

    [Fact]
    public void Split_LeadingTrailingSeparators()
    {
        Split(",a,", ',').ShouldBe(["", "a", ""]);
    }

    [Fact]
    public void Split_RemoveEmptyEntries()
    {
        Split("a,,c", ',', StringSplitOptions.RemoveEmptyEntries).ShouldBe(["a", "c"]);
        Split(",a,,", ',', StringSplitOptions.RemoveEmptyEntries).ShouldBe(["a"]);
    }

    [Fact]
    public void Split_TrimEntries()
    {
        Split(" a , b ", ',', TrimEntries).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Split_TrimAndRemoveEmpty()
    {
        Split(" a ,  , b ", ',', StringSplitOptions.RemoveEmptyEntries | TrimEntries).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void Split_EmptySegment_YieldsSingleEmpty()
    {
        Split(string.Empty, ',').ShouldBe([""]);
        Split(string.Empty, ',', StringSplitOptions.RemoveEmptyEntries).ShouldBe([]);
    }

    [Fact]
    public void Split_MultipleSeparators()
    {
        List<string> result = [];
        foreach (StringSegment piece in ((StringSegment)"a;b,c").Split([';', ',']))
        {
            result.Add(piece.ValueOrEmpty);
        }

        result.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Split_EmptySeparatorSet_YieldsWhole()
    {
        List<string> result = [];
        foreach (StringSegment piece in ((StringSegment)"a,b").Split(default(ReadOnlySpan<char>)))
        {
            result.Add(piece.ValueOrEmpty);
        }

        result.ShouldBe(["a,b"]);
    }

    [Fact]
    public void Split_YieldsViewsOverOriginalBuffer()
    {
        const string source = "a,b,c";

        foreach (StringSegment piece in ((StringSegment)source).Split(','))
        {
            ReferenceEquals(piece.Buffer, source).ShouldBeTrue();
        }
    }

    [Fact]
    public void Split_OnSubSegment_IsRelative()
    {
        StringSegment segment = new("[[a,b]]", 2, 3); // "a,b"
        Split(segment, ',').ShouldBe(["a", "b"]);
    }

    [Fact]
    public void StartsWith_Variants()
    {
        HelloWorld.StartsWith('h').ShouldBeTrue();
        HelloWorld.StartsWith('w').ShouldBeFalse();
        HelloWorld.StartsWith("hello").ShouldBeTrue();
        HelloWorld.StartsWith("Hello").ShouldBeFalse();
        HelloWorld.StartsWith("Hello", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.StartsWith("hello world!").ShouldBeFalse(); // longer than the segment
        HelloWorld.StartsWith(string.Empty).ShouldBeTrue();
        HelloWorld.StartsWith("hello".AsSpan()).ShouldBeTrue();
        HelloWorld.StartsWith("Hello".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_Variants()
    {
        HelloWorld.EndsWith('d').ShouldBeTrue();
        HelloWorld.EndsWith('h').ShouldBeFalse();
        HelloWorld.EndsWith("world").ShouldBeTrue();
        HelloWorld.EndsWith("World").ShouldBeFalse();
        HelloWorld.EndsWith("World", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.EndsWith(string.Empty).ShouldBeTrue();
        HelloWorld.EndsWith("world".AsSpan()).ShouldBeTrue();
        HelloWorld.EndsWith("World".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void IndexOf_Segment()
    {
        // Whole-buffer search value: reuses the buffer with no allocation.
        HelloWorld.IndexOf((StringSegment)"world").ShouldBe(6);
        HelloWorld.IndexOf((StringSegment)"missing").ShouldBe(-1);
        HelloWorld.IndexOf(StringSegment.Empty).ShouldBe(0);
        default(StringSegment).IndexOf(StringSegment.Empty).ShouldBe(-1);
        default(StringSegment).Contains(StringSegment.Empty).ShouldBeFalse();

        // Sub-view search value exercises the substring fallback path.
        StringSegment worldSubView = new("xxworldyy", 2, 5);
        HelloWorld.IndexOf(worldSubView).ShouldBe(6);
        StringSegment upperWorldSubView = new("xxWORLDyy", 2, 5);
        HelloWorld.IndexOf(upperWorldSubView, StringComparison.OrdinalIgnoreCase).ShouldBe(6);

        // Comparison and range overloads.
        HelloWorld.IndexOf((StringSegment)"WORLD", StringComparison.OrdinalIgnoreCase).ShouldBe(6);
        HelloWorld.IndexOf((StringSegment)"o", 5).ShouldBe(7);
        HelloWorld.IndexOf((StringSegment)"o", 0, 5).ShouldBe(4);
    }

    [Fact]
    public void LastIndexOf_Segment()
    {
        HelloWorld.LastIndexOf((StringSegment)"o").ShouldBe(7);
        HelloWorld.LastIndexOf((StringSegment)"world").ShouldBe(6);
        HelloWorld.LastIndexOf(StringSegment.Empty).ShouldBe(11);
        default(StringSegment).LastIndexOf(StringSegment.Empty).ShouldBe(-1);

        StringSegment worldSubView = new("xxworldyy", 2, 5);
        HelloWorld.LastIndexOf(worldSubView).ShouldBe(6);
        StringSegment upperWorldSubView = new("xxWORLDyy", 2, 5);
        HelloWorld.LastIndexOf(upperWorldSubView, StringComparison.OrdinalIgnoreCase).ShouldBe(6);

        HelloWorld.LastIndexOf((StringSegment)"o", 6).ShouldBe(4);
        HelloWorld.LastIndexOf((StringSegment)"world", 10, 5).ShouldBe(6);
        HelloWorld.LastIndexOf((StringSegment)"HELLO", 4, 5, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
    }

    [Fact]
    public void Contains_Segment()
    {
        HelloWorld.Contains((StringSegment)"world").ShouldBeTrue();
        HelloWorld.Contains((StringSegment)"WORLD", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.Contains((StringSegment)"missing").ShouldBeFalse();

        StringSegment loSubView = new("xxloyy", 2, 2);
        HelloWorld.Contains(loSubView).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_Segment()
    {
        HelloWorld.StartsWith((StringSegment)"hello").ShouldBeTrue();
        HelloWorld.StartsWith((StringSegment)"Hello").ShouldBeFalse();
        HelloWorld.StartsWith((StringSegment)"Hello", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.StartsWith((StringSegment)"hello world!").ShouldBeFalse(); // longer than the segment
        HelloWorld.StartsWith(StringSegment.Empty).ShouldBeTrue();

        StringSegment helloSubView = new("xxhelloyy", 2, 5);
        HelloWorld.StartsWith(helloSubView).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_Segment()
    {
        HelloWorld.EndsWith((StringSegment)"world").ShouldBeTrue();
        HelloWorld.EndsWith((StringSegment)"World").ShouldBeFalse();
        HelloWorld.EndsWith((StringSegment)"World", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        HelloWorld.EndsWith(StringSegment.Empty).ShouldBeTrue();

        StringSegment worldSubView = new("xxworldyy", 2, 5);
        HelloWorld.EndsWith(worldSubView).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_OrdinalIgnoreCase_MatchesCaseVariants()
    {
        StringSegment lower = "hello world";
        StringSegment upper = new("[[HELLO WORLD]]", 2, 11);

        lower.GetHashCode(StringComparison.OrdinalIgnoreCase)
            .ShouldBe(upper.GetHashCode(StringComparison.OrdinalIgnoreCase));

        // The ordinal overload agrees with the parameterless hash.
        lower.GetHashCode(StringComparison.Ordinal).ShouldBe(lower.GetHashCode());

        // Non-ASCII case pair whose case bit is not 0x20 (Cyrillic Ie-with-grave). This verifies the fold
        // stays correct beyond the ASCII fast-path mask.
        StringSegment cyrillicUpper = "\u0400"; // Ѐ
        StringSegment cyrillicLower = "\u0450"; // ѐ
        cyrillicUpper.Equals(cyrillicLower, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        cyrillicUpper.GetHashCode(StringComparison.OrdinalIgnoreCase)
            .ShouldBe(cyrillicLower.GetHashCode(StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StringSegmentComparer_Ordinal_ComparesCaseSensitively()
    {
        StringSegmentComparer comparer = StringSegmentComparer.Ordinal;

        comparer.Equals("abc", "abc").ShouldBeTrue();
        comparer.Equals("abc", "ABC").ShouldBeFalse();
        comparer.Compare("a", "b").ShouldBeLessThan(0);
        comparer.Compare("b", "a").ShouldBeGreaterThan(0);
        comparer.Compare("a", "a").ShouldBe(0);
        comparer.GetHashCode("abc").ShouldBe(((StringSegment)"abc").GetHashCode());

        StringSegmentComparer.FromComparison(StringComparison.Ordinal).ShouldBeSameAs(comparer);
    }

    [Fact]
    public void StringSegmentComparer_OrdinalIgnoreCase_ComparesCaseInsensitively()
    {
        StringSegmentComparer comparer = StringSegmentComparer.OrdinalIgnoreCase;

        comparer.Equals("abc", "ABC").ShouldBeTrue();
        comparer.Equals("abc", "abd").ShouldBeFalse();
        comparer.GetHashCode("abc").ShouldBe(comparer.GetHashCode("ABC"));

        StringSegmentComparer.FromComparison(StringComparison.OrdinalIgnoreCase).ShouldBeSameAs(comparer);
        Should.Throw<ArgumentOutOfRangeException>(
            () => StringSegmentComparer.FromComparison(StringComparison.CurrentCulture));
    }

    [Fact]
    public void StringSegmentComparer_UsableAsDictionaryKey()
    {
        Dictionary<StringSegment, int> map = new(StringSegmentComparer.OrdinalIgnoreCase)
        {
            [(StringSegment)"Key"] = 1,
        };

        map.ContainsKey((StringSegment)"KEY").ShouldBeTrue();
        map[(StringSegment)"key"].ShouldBe(1);
    }

    [Fact]
    public void ImplicitToReadOnlySpan()
    {
        ReadOnlySpan<char> span = HelloWorld;
        span.ToString().ShouldBe("hello world");
    }

    [Fact]
    public void ImplicitToReadOnlyMemory()
    {
        ReadOnlyMemory<char> memory = HelloWorld;
        memory.ToString().ShouldBe("hello world");
    }

    [Fact]
    public void ValueOrEmpty_ReturnsValueOrEmpty()
    {
        HelloWorld.ValueOrEmpty.ShouldBe("hello world");
        default(StringSegment).ValueOrEmpty.ShouldBe(string.Empty);
        StringSegment.Empty.ValueOrEmpty.ShouldBe(string.Empty);
    }

    [Fact]
    public void ToString_ReturnsValueOrEmpty()
    {
        HelloWorld.ToString().ShouldBe("hello world");
        default(StringSegment).ToString().ShouldBe(string.Empty);
        StringSegment.Empty.ToString().ShouldBe(string.Empty);
    }
}
