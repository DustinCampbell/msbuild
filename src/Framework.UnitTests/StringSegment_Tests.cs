// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Buffers;
#endif
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class StringSegment_Tests
{
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

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("a", false)]
    [InlineData("hello", false)]
    public void IsNullOrEmpty_Works(string? value, bool expected)
    {
        StringSegment.IsNullOrEmpty(value).ShouldBe(expected);
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
    [InlineData("abc", "ABC", StringComparison.OrdinalIgnoreCase, true)]
    [InlineData("abc", "ABC", StringComparison.Ordinal, false)]
    [InlineData("abc", "abc", StringComparison.Ordinal, true)]
    public void Equals_WithComparison(string left, string right, StringComparison comparison, bool expected)
    {
        ((StringSegment)left).Equals((StringSegment)right, comparison).ShouldBe(expected);
        StringSegment.Equals(left, right, comparison).ShouldBe(expected);
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

#if NET
    [Fact]
    public void IndexOfAny_SearchValues()
    {
        HelloWorld.IndexOfAny(SearchValues.Create("ow")).ShouldBe(4);
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
        HelloWorld.LastIndexOfAny(SearchValues.Create("ol")).ShouldBe(9);
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
        StringSegment.Join(',', "only").ShouldBe("only");
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
        StringSegment.Join("--", "only").ShouldBe("only");
        StringSegment.Join(string.Empty, "only").ShouldBe("only");
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
    public void ToString_ReturnsValueOrEmpty()
    {
        HelloWorld.ToString().ShouldBe("hello world");
        ((StringSegment)default).ToString().ShouldBe(string.Empty);
        StringSegment.Empty.ToString().ShouldBe(string.Empty);
    }
}
