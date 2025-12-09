// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Shared.UnitTests;

public class StringSegment_Tests
{
    [Fact]
    public void Constructor_WithNullString_CreatesEmptySegment()
    {
        var segment = new StringSegment(null);

        segment.IsEmpty.ShouldBeTrue();
        segment.Length.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithEmptyString_CreatesEmptySegment()
    {
        var segment = new StringSegment(string.Empty);

        segment.IsEmpty.ShouldBeTrue();
        segment.Length.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WithString_CreatesSegment()
    {
        var segment = new StringSegment("hello");

        segment.IsEmpty.ShouldBeFalse();
        segment.Length.ShouldBe(5);
        segment.ToString().ShouldBe("hello");
    }

    [Fact]
    public void Constructor_WithOffsetAndLength_CreatesSegment()
    {
        var segment = new StringSegment("hello world", 6, 5);

        segment.Length.ShouldBe(5);
        segment.ToString().ShouldBe("world");
    }

    [Fact]
    public void Constructor_WithInvalidOffset_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("hello", -1, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("hello", 10, 3));
    }

    [Fact]
    public void Constructor_WithInvalidLength_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("hello", 0, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("hello", 0, 10));
        Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("hello", 2, 5));
    }

    [Fact]
    public void Indexer_ReturnsCorrectCharacter()
    {
        var segment = new StringSegment("hello");

        segment[0].ShouldBe('h');
        segment[4].ShouldBe('o');
    }

    [Fact]
    public void Indexer_WithInvalidIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment[-1]);
        Should.Throw<ArgumentOutOfRangeException>(() => segment[5]);
    }

    [Fact]
    public void Substring_WithStartIndex_ReturnsCorrectString()
    {
        var segment = new StringSegment("hello world");

        segment.Substring(0).ShouldBe("hello world");
        segment.Substring(6).ShouldBe("world");
        segment.Substring(11).ShouldBe(string.Empty);
    }

    [Fact]
    public void Substring_WithStartIndexAndLength_ReturnsCorrectString()
    {
        var segment = new StringSegment("hello world");

        segment.Substring(0, 5).ShouldBe("hello");
        segment.Substring(6, 5).ShouldBe("world");
        segment.Substring(0, 0).ShouldBe(string.Empty);
    }

    [Fact]
    public void Slice_WithStart_ReturnsCorrectSegment()
    {
        var segment = new StringSegment("hello world");

        var sliced = segment.Slice(6);
        sliced.Length.ShouldBe(5);
        sliced.ToString().ShouldBe("world");

        segment.Slice(11).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Slice_WithStartAndLength_ReturnsCorrectSegment()
    {
        var segment = new StringSegment("hello world");

        var sliced = segment.Slice(0, 5);
        sliced.Length.ShouldBe(5);
        sliced.ToString().ShouldBe("hello");

        segment.Slice(6, 5).ToString().ShouldBe("world");
        segment.Slice(0, 0).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IndexOf_Char_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world");

        segment.IndexOf('h').ShouldBe(0);
        segment.IndexOf('w').ShouldBe(6);
        segment.IndexOf('d').ShouldBe(10);
        segment.IndexOf('x').ShouldBe(-1);
    }


    [Fact]
    public void Contains_Char_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.Contains('h').ShouldBeTrue();
        segment.Contains('o').ShouldBeTrue();
        segment.Contains('w').ShouldBeTrue();
        segment.Contains('d').ShouldBeTrue();
        segment.Contains('x').ShouldBeFalse();
        segment.Contains('H').ShouldBeFalse();
    }

    [Fact]
    public void Contains_Char_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        // Ordinal comparison (case-sensitive)
        segment.Contains('h', StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains('H', StringComparison.Ordinal).ShouldBeTrue();
        segment.Contains('w', StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains('W', StringComparison.Ordinal).ShouldBeTrue();

        // OrdinalIgnoreCase comparison (case-insensitive)
        segment.Contains('h', StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains('H', StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains('w', StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains('W', StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains('x', StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Fact]
    public void Contains_String_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.Contains("hello").ShouldBeTrue();
        segment.Contains("world").ShouldBeTrue();
        segment.Contains("lo wo").ShouldBeTrue();
        segment.Contains("o").ShouldBeTrue();
        segment.Contains(string.Empty).ShouldBeTrue();
        segment.Contains("Hello").ShouldBeFalse();
        segment.Contains("xyz").ShouldBeFalse();
        segment.Contains("hello world!").ShouldBeFalse();
    }

    [Fact]
    public void Contains_String_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        // Ordinal comparison (case-sensitive)
        segment.Contains("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains("Hello", StringComparison.Ordinal).ShouldBeTrue();
        segment.Contains("WORLD", StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains("World", StringComparison.Ordinal).ShouldBeTrue();

        // OrdinalIgnoreCase comparison (case-insensitive)
        segment.Contains("hello", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("HELLO", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("world", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("WORLD", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("xyz", StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Fact]
    public void Contains_String_NullValue_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentNullException>(() => segment.Contains((string)null!));
        Should.Throw<ArgumentNullException>(() => segment.Contains(null!, StringComparison.Ordinal));
    }

    [Fact]
    public void Contains_Span_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.Contains("hello".AsSpan()).ShouldBeTrue();
        segment.Contains("world".AsSpan()).ShouldBeTrue();
        segment.Contains("lo wo".AsSpan()).ShouldBeTrue();
        segment.Contains("o".AsSpan()).ShouldBeTrue();
        segment.Contains(ReadOnlySpan<char>.Empty).ShouldBeTrue();
        segment.Contains("Hello".AsSpan()).ShouldBeFalse();
        segment.Contains("xyz".AsSpan()).ShouldBeFalse();
    }

    [Fact]
    public void Contains_Span_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        // Ordinal comparison (case-sensitive)
        segment.Contains("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains("Hello".AsSpan(), StringComparison.Ordinal).ShouldBeTrue();
        segment.Contains("WORLD".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains("World".AsSpan(), StringComparison.Ordinal).ShouldBeTrue();

        // OrdinalIgnoreCase comparison (case-insensitive)
        segment.Contains("hello".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("HELLO".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("world".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("WORLD".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Contains("xyz".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeFalse();
    }

    [Fact]
    public void Contains_EmptySegment_ReturnsFalseForNonEmpty()
    {
        var empty = StringSegment.Empty;

        empty.Contains('a').ShouldBeFalse();
        empty.Contains('a', StringComparison.Ordinal).ShouldBeFalse();
        empty.Contains("test").ShouldBeFalse();
        empty.Contains("test", StringComparison.Ordinal).ShouldBeFalse();
        empty.Contains("test".AsSpan()).ShouldBeFalse();
        empty.Contains("test".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Contains_EmptyString_ReturnsTrue()
    {
        var segment = new StringSegment("hello");

        segment.Contains(string.Empty).ShouldBeTrue();
        segment.Contains(string.Empty, StringComparison.Ordinal).ShouldBeTrue();
        segment.Contains(ReadOnlySpan<char>.Empty).ShouldBeTrue();
        segment.Contains(ReadOnlySpan<char>.Empty, StringComparison.Ordinal).ShouldBeTrue();

        // Empty segment also contains empty string
        StringSegment.Empty.Contains(string.Empty).ShouldBeTrue();
        StringSegment.Empty.Contains(string.Empty, StringComparison.Ordinal).ShouldBeTrue();
        StringSegment.Empty.Contains(ReadOnlySpan<char>.Empty).ShouldBeTrue();
        StringSegment.Empty.Contains(ReadOnlySpan<char>.Empty, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Contains_AtBeginning_ReturnsTrue()
    {
        var segment = new StringSegment("hello world");

        segment.Contains('h').ShouldBeTrue();
        segment.Contains("hello").ShouldBeTrue();
        segment.Contains("hello", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Contains_AtEnd_ReturnsTrue()
    {
        var segment = new StringSegment("hello world");

        segment.Contains('d').ShouldBeTrue();
        segment.Contains("world").ShouldBeTrue();
        segment.Contains("world", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Contains_InMiddle_ReturnsTrue()
    {
        var segment = new StringSegment("hello world");

        segment.Contains('o').ShouldBeTrue();
        segment.Contains("lo wo").ShouldBeTrue();
        segment.Contains("lo wo", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void Contains_MultipleOccurrences_ReturnsTrue()
    {
        var segment = new StringSegment("banana");

        segment.Contains('a').ShouldBeTrue();
        segment.Contains("an").ShouldBeTrue();
        segment.Contains("na").ShouldBeTrue();
    }

    [Fact]
    public void Contains_SingleCharacterString_WorksCorrectly()
    {
        var segment = new StringSegment("hello");

        segment.Contains("h").ShouldBeTrue();
        segment.Contains("e").ShouldBeTrue();
        segment.Contains("o").ShouldBeTrue();
        segment.Contains("x").ShouldBeFalse();
    }

    [Fact]
    public void Contains_LongerThanSegment_ReturnsFalse()
    {
        var segment = new StringSegment("hi");

        segment.Contains("hello").ShouldBeFalse();
        segment.Contains("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.Contains("hello".AsSpan()).ShouldBeFalse();
        segment.Contains("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Contains_EntireSegment_ReturnsTrue()
    {
        var segment = new StringSegment("hello");

        segment.Contains("hello").ShouldBeTrue();
        segment.Contains("hello", StringComparison.Ordinal).ShouldBeTrue();
        segment.Contains("HELLO", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void IndexOf_Char_WithStartIndex_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.IndexOf('h', 0).ShouldBe(0);
        segment.IndexOf('h', 1).ShouldBe(12);
        segment.IndexOf('o', 5).ShouldBe(7);
        segment.IndexOf('x', 0).ShouldBe(-1);
        segment.IndexOf('h', 17).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_Char_WithStartIndex_InvalidStartIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf('h', -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf('h', 6));
    }

    [Fact]
    public void IndexOf_Char_WithComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World");

        segment.IndexOf('h', StringComparison.Ordinal).ShouldBe(-1);
        segment.IndexOf('h', StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.IndexOf('H', StringComparison.Ordinal).ShouldBe(0);
        segment.IndexOf('w', StringComparison.OrdinalIgnoreCase).ShouldBe(6);
    }

    [Fact]
    public void IndexOf_Char_WithStartIndexAndCount_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.IndexOf('h', 0, 5).ShouldBe(0);
        segment.IndexOf('h', 0, 11).ShouldBe(0);
        segment.IndexOf('h', 1, 11).ShouldBe(-1);
        segment.IndexOf('h', 12, 5).ShouldBe(12);
        segment.IndexOf('o', 4, 4).ShouldBe(4);
        segment.IndexOf('o', 1, 3).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_Char_WithStartIndexAndCount_InvalidRange_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf('h', -1, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf('h', 0, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf('h', 0, 6));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf('h', 3, 3));
    }

    [Fact]
    public void IndexOf_String_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world");

        segment.IndexOf("hello").ShouldBe(0);
        segment.IndexOf("world").ShouldBe(6);
        segment.IndexOf("lo wo").ShouldBe(3);
        segment.IndexOf("xyz").ShouldBe(-1);
        segment.IndexOf("").ShouldBe(0);
    }

    [Fact]
    public void IndexOf_String_WithStartIndex_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.IndexOf("hello", 0).ShouldBe(0);
        segment.IndexOf("hello", 1).ShouldBe(12);
        segment.IndexOf("world", 0).ShouldBe(6);
        segment.IndexOf("world", 7).ShouldBe(-1);
        segment.IndexOf("", 10).ShouldBe(10);
        segment.IndexOf("hello", 17).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_String_WithStartIndex_InvalidStartIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 6));
    }

    [Fact]
    public void IndexOf_String_WithComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World");

        segment.IndexOf("hello", StringComparison.Ordinal).ShouldBe(-1);
        segment.IndexOf("hello", StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.IndexOf("WORLD", StringComparison.OrdinalIgnoreCase).ShouldBe(6);
        segment.IndexOf("WORLD", StringComparison.Ordinal).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_String_WithStartIndexAndCount_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.IndexOf("hello", 0, 5).ShouldBe(0);
        segment.IndexOf("hello", 0, 17).ShouldBe(0);
        segment.IndexOf("hello", 1, 16).ShouldBe(12);
        segment.IndexOf("world", 0, 11).ShouldBe(6);
        segment.IndexOf("world", 0, 10).ShouldBe(-1);
        segment.IndexOf("", 5, 0).ShouldBe(5);
        segment.IndexOf("x", 5, 0).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_String_WithStartIndexAndCount_InvalidRange_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", -1, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 0, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 0, 6));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 3, 3));
    }

    [Fact]
    public void IndexOf_String_WithStartIndexAndComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World Hello");

        segment.IndexOf("hello", 0, StringComparison.Ordinal).ShouldBe(-1);
        segment.IndexOf("hello", 0, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.IndexOf("hello", 1, StringComparison.OrdinalIgnoreCase).ShouldBe(12);
        segment.IndexOf("WORLD", 0, StringComparison.OrdinalIgnoreCase).ShouldBe(6);
        segment.IndexOf("", 10, StringComparison.Ordinal).ShouldBe(10);
    }

    [Fact]
    public void IndexOf_String_WithStartIndexAndComparison_InvalidStartIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", -1, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 6, StringComparison.Ordinal));
    }

    [Fact]
    public void IndexOf_String_WithStartIndexCountAndComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World Hello");

        segment.IndexOf("hello", 0, 5, StringComparison.Ordinal).ShouldBe(-1);
        segment.IndexOf("hello", 0, 5, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.IndexOf("hello", 1, 16, StringComparison.OrdinalIgnoreCase).ShouldBe(12);
        segment.IndexOf("WORLD", 0, 11, StringComparison.OrdinalIgnoreCase).ShouldBe(6);
        segment.IndexOf("WORLD", 0, 10, StringComparison.OrdinalIgnoreCase).ShouldBe(-1);
        segment.IndexOf("", 5, 0, StringComparison.Ordinal).ShouldBe(5);
    }

    [Fact]
    public void IndexOf_String_WithStartIndexCountAndComparison_InvalidRange_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", -1, 3, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 0, -1, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 0, 6, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.IndexOf("world", 3, 3, StringComparison.Ordinal));
    }

    [Fact]
    public void IndexOf_EmptySegment_ReturnsNegativeOne()
    {
        var empty = StringSegment.Empty;

        empty.IndexOf('a').ShouldBe(-1);
        empty.IndexOf('a', 0).ShouldBe(-1);
        empty.IndexOf('a', StringComparison.Ordinal).ShouldBe(-1);
        empty.IndexOf('a', 0, 0).ShouldBe(-1);
        empty.IndexOf("test").ShouldBe(-1);
        empty.IndexOf("test", 0).ShouldBe(-1);
        empty.IndexOf("test", StringComparison.Ordinal).ShouldBe(-1);
        empty.IndexOf("test", 0, 0).ShouldBe(-1);
        empty.IndexOf("test", 0, StringComparison.Ordinal).ShouldBe(-1);
        empty.IndexOf("test", 0, 0, StringComparison.Ordinal).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_EmptyString_ReturnsStartIndex()
    {
        var segment = new StringSegment("hello");

        segment.IndexOf("").ShouldBe(0);
        segment.IndexOf("", 0).ShouldBe(0);
        segment.IndexOf("", 3).ShouldBe(3);
        segment.IndexOf("", 5).ShouldBe(5);
        segment.IndexOf("", StringComparison.Ordinal).ShouldBe(0);
        segment.IndexOf("", 2, StringComparison.Ordinal).ShouldBe(2);
    }

    [Fact]
    public void IndexOf_EdgeCases_WorkCorrectly()
    {
        var segment = new StringSegment("aaa");

        // Multiple occurrences
        segment.IndexOf('a').ShouldBe(0);
        segment.IndexOf('a', 1).ShouldBe(1);
        segment.IndexOf('a', 2).ShouldBe(2);
        segment.IndexOf('a', 3).ShouldBe(-1);

        // Character at the end
        segment = new StringSegment("hello!");
        segment.IndexOf('!').ShouldBe(5);
        segment.IndexOf('!', 5).ShouldBe(5);
        segment.IndexOf('!', 6).ShouldBe(-1);
    }

    [Fact]
    public void IndexOf_NullString_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentNullException>(() => segment.IndexOf(null!));
        Should.Throw<ArgumentNullException>(() => segment.IndexOf(null!, 0));
        Should.Throw<ArgumentNullException>(() => segment.IndexOf(null!, StringComparison.Ordinal));
        Should.Throw<ArgumentNullException>(() => segment.IndexOf(null!, 0, 5));
        Should.Throw<ArgumentNullException>(() => segment.IndexOf(null!, 0, StringComparison.Ordinal));
        Should.Throw<ArgumentNullException>(() => segment.IndexOf(null!, 0, 5, StringComparison.Ordinal));
    }

    [Fact]
    public void LastIndexOf_Char_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world");

        segment.LastIndexOf('h').ShouldBe(0);
        segment.LastIndexOf('o').ShouldBe(7); // Last 'o' in "world"
        segment.LastIndexOf('l').ShouldBe(9); // Last 'l' in "world"
        segment.LastIndexOf('d').ShouldBe(10);
        segment.LastIndexOf('x').ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_Char_WithStartIndex_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.LastIndexOf('h', 16).ShouldBe(12); // Last 'h'
        segment.LastIndexOf('h', 11).ShouldBe(0);  // First 'h'
        segment.LastIndexOf('h', 0).ShouldBe(0);
        segment.LastIndexOf('o', 10).ShouldBe(7);  // 'o' in "world"
        segment.LastIndexOf('x', 10).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_Char_WithStartIndex_InvalidStartIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf('h', -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf('h', 5));
    }

    [Fact]
    public void LastIndexOf_Char_WithComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World");

        segment.LastIndexOf('h', StringComparison.Ordinal).ShouldBe(-1);
        segment.LastIndexOf('h', StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.LastIndexOf('H', StringComparison.Ordinal).ShouldBe(0);
        segment.LastIndexOf('O', StringComparison.OrdinalIgnoreCase).ShouldBe(7); // Last 'o'/'O'
    }

    [Fact]
    public void LastIndexOf_Char_WithStartIndexAndCount_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.LastIndexOf('h', 16, 17).ShouldBe(12); // Search entire string
        segment.LastIndexOf('h', 11, 12).ShouldBe(0);  // Search first part
        segment.LastIndexOf('h', 16, 5).ShouldBe(12);  // Search last 5 chars
        segment.LastIndexOf('o', 10, 8).ShouldBe(7);   // 'o' in "world"
        segment.LastIndexOf('h', 5, 5).ShouldBe(-1);   // Not in range
    }

    [Fact]
    public void LastIndexOf_Char_WithStartIndexAndCount_InvalidRange_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf('h', -1, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf('h', 5, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf('h', 2, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf('h', 2, 4));
    }

    [Fact]
    public void LastIndexOf_String_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.LastIndexOf("hello").ShouldBe(12);
        segment.LastIndexOf("world").ShouldBe(6);
        segment.LastIndexOf("o").ShouldBe(16); // Last 'o'
        segment.LastIndexOf("xyz").ShouldBe(-1);
        segment.LastIndexOf("").ShouldBe(17); // Empty string at end
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndex_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.LastIndexOf("hello", 16).ShouldBe(12);
        segment.LastIndexOf("hello", 11).ShouldBe(0);
        segment.LastIndexOf("hello", 4).ShouldBe(0);
        segment.LastIndexOf("world", 10).ShouldBe(6);
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndex_InvalidStartIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 5));
    }

    [Fact]
    public void LastIndexOf_String_WithComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World Hello");

        segment.LastIndexOf("hello", StringComparison.Ordinal).ShouldBe(-1);
        segment.LastIndexOf("hello", StringComparison.OrdinalIgnoreCase).ShouldBe(12);
        segment.LastIndexOf("WORLD", StringComparison.OrdinalIgnoreCase).ShouldBe(6);
        segment.LastIndexOf("WORLD", StringComparison.Ordinal).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndexAndCount_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.LastIndexOf("hello", 16, 17).ShouldBe(12);
        segment.LastIndexOf("hello", 11, 12).ShouldBe(0);
        segment.LastIndexOf("hello", 16, 5).ShouldBe(12);
        segment.LastIndexOf("world", 10, 11).ShouldBe(6);
        segment.LastIndexOf("hello", 10, 6).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndexAndCount_InvalidRange_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", -1, 3));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 5, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 2, -1));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 2, 4));
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndexAndComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World Hello");

        segment.LastIndexOf("hello", 16, StringComparison.Ordinal).ShouldBe(-1);
        segment.LastIndexOf("hello", 16, StringComparison.OrdinalIgnoreCase).ShouldBe(12);
        segment.LastIndexOf("hello", 11, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.LastIndexOf("WORLD", 10, StringComparison.OrdinalIgnoreCase).ShouldBe(6);
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndexAndComparison_InvalidStartIndex_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", -1, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 5, StringComparison.Ordinal));
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndexCountAndComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World Hello");

        segment.LastIndexOf("hello", 16, 17, StringComparison.Ordinal).ShouldBe(-1);
        segment.LastIndexOf("hello", 16, 17, StringComparison.OrdinalIgnoreCase).ShouldBe(12);
        segment.LastIndexOf("hello", 11, 12, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
        segment.LastIndexOf("WORLD", 10, 11, StringComparison.OrdinalIgnoreCase).ShouldBe(6);
    }

    [Fact]
    public void LastIndexOf_String_WithStartIndexCountAndComparison_InvalidRange_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", -1, 3, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 5, 1, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 2, -1, StringComparison.Ordinal));
        Should.Throw<ArgumentOutOfRangeException>(() => segment.LastIndexOf("world", 2, 4, StringComparison.Ordinal));
    }

    [Fact]
    public void LastIndexOf_EmptySegment_ReturnsNegativeOne()
    {
        var empty = StringSegment.Empty;

        empty.LastIndexOf('a').ShouldBe(-1);
        empty.LastIndexOf("test").ShouldBe(-1);
        empty.LastIndexOf("test", StringComparison.Ordinal).ShouldBe(-1);
        empty.LastIndexOf("test".AsSpan()).ShouldBe(-1);
        empty.LastIndexOf("test".AsSpan(), StringComparison.Ordinal).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_EmptyString_ReturnsEndPosition()
    {
        var segment = new StringSegment("hello");

        segment.LastIndexOf("").ShouldBe(5); // End of string
        segment.LastIndexOf("", StringComparison.Ordinal).ShouldBe(5);
        segment.LastIndexOf([]).ShouldBe(5);
        segment.LastIndexOf([], StringComparison.Ordinal).ShouldBe(5);
    }

    [Fact]
    public void LastIndexOf_NullString_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentNullException>(() => segment.LastIndexOf(null!));
        Should.Throw<ArgumentNullException>(() => segment.LastIndexOf(null!, 0));
        Should.Throw<ArgumentNullException>(() => segment.LastIndexOf(null!, StringComparison.Ordinal));
        Should.Throw<ArgumentNullException>(() => segment.LastIndexOf(null!, 0, 5));
        Should.Throw<ArgumentNullException>(() => segment.LastIndexOf(null!, 0, StringComparison.Ordinal));
        Should.Throw<ArgumentNullException>(() => segment.LastIndexOf(null!, 0, 5, StringComparison.Ordinal));
    }

    [Fact]
    public void LastIndexOf_MultipleOccurrences_ReturnsLastOne()
    {
        var segment = new StringSegment("banana");

        segment.LastIndexOf('a').ShouldBe(5);
        segment.LastIndexOf("an").ShouldBe(3);
        segment.LastIndexOf("na").ShouldBe(4);
    }

    [Fact]
    public void LastIndexOf_Span_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("hello world hello");

        segment.LastIndexOf("hello".AsSpan()).ShouldBe(12);
        segment.LastIndexOf("world".AsSpan()).ShouldBe(6);
        segment.LastIndexOf("o".AsSpan()).ShouldBe(16);
        segment.LastIndexOf("xyz".AsSpan()).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_Span_WithComparison_ReturnsCorrectIndex()
    {
        var segment = new StringSegment("Hello World Hello");

        segment.LastIndexOf("hello".AsSpan(), StringComparison.Ordinal).ShouldBe(-1);
        segment.LastIndexOf("hello".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBe(12);
        segment.LastIndexOf("WORLD".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBe(6);
    }

    [Fact]
    public void StartsWith_String_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.StartsWith("hello").ShouldBeTrue();
        segment.StartsWith("world").ShouldBeFalse();
        segment.StartsWith(string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_String_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        segment.StartsWith("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.StartsWith("hello", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith("HELLO", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith("Hello", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_String_NullValue_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentNullException>(() => segment.StartsWith((string)null!));
        Should.Throw<ArgumentNullException>(() => segment.StartsWith(null!, StringComparison.Ordinal));
    }

    [Fact]
    public void StartsWith_Span_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.StartsWith("hello".AsSpan()).ShouldBeTrue();
        segment.StartsWith("world".AsSpan()).ShouldBeFalse();
        segment.StartsWith(ReadOnlySpan<char>.Empty).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_Span_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        segment.StartsWith("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
        segment.StartsWith("hello".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith("HELLO".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith("Hello".AsSpan(), StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_Char_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.StartsWith('h').ShouldBeTrue();
        segment.StartsWith('w').ShouldBeFalse();
        segment.StartsWith('H').ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_EmptySegment_ReturnsFalse()
    {
        var empty = StringSegment.Empty;

        empty.StartsWith('h').ShouldBeFalse();
        empty.StartsWith("hello").ShouldBeFalse();
        empty.StartsWith("hello", StringComparison.Ordinal).ShouldBeFalse();
        empty.StartsWith("hello".AsSpan()).ShouldBeFalse();
        empty.StartsWith("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_EmptyString_ReturnsTrue()
    {
        var segment = new StringSegment("hello");

        segment.StartsWith(string.Empty).ShouldBeTrue();
        segment.StartsWith(string.Empty, StringComparison.Ordinal).ShouldBeTrue();
        segment.StartsWith(ReadOnlySpan<char>.Empty).ShouldBeTrue();
        segment.StartsWith(ReadOnlySpan<char>.Empty, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_String_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.EndsWith("world").ShouldBeTrue();
        segment.EndsWith("hello").ShouldBeFalse();
        segment.EndsWith(string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_String_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        segment.EndsWith("world", StringComparison.Ordinal).ShouldBeFalse();
        segment.EndsWith("world", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith("WORLD", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith("World", StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_String_NullValue_Throws()
    {
        var segment = new StringSegment("hello");

        Should.Throw<ArgumentNullException>(() => segment.EndsWith((string)null!));
        Should.Throw<ArgumentNullException>(() => segment.EndsWith(null!, StringComparison.Ordinal));
    }

    [Fact]
    public void EndsWith_Span_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.EndsWith("world".AsSpan()).ShouldBeTrue();
        segment.EndsWith("hello".AsSpan()).ShouldBeFalse();
        segment.EndsWith(ReadOnlySpan<char>.Empty).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_Span_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello World");

        segment.EndsWith("world".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
        segment.EndsWith("world".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith("WORLD".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith("World".AsSpan(), StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_Char_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello world");

        segment.EndsWith('d').ShouldBeTrue();
        segment.EndsWith('h').ShouldBeFalse();
        segment.EndsWith('D').ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_EmptySegment_ReturnsFalse()
    {
        var empty = StringSegment.Empty;

        empty.EndsWith('d').ShouldBeFalse();
        empty.EndsWith("world").ShouldBeFalse();
        empty.EndsWith("world", StringComparison.Ordinal).ShouldBeFalse();
        empty.EndsWith("world".AsSpan()).ShouldBeFalse();
        empty.EndsWith("world".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_EmptyString_ReturnsTrue()
    {
        var segment = new StringSegment("hello");

        segment.EndsWith(string.Empty).ShouldBeTrue();
        segment.EndsWith(string.Empty, StringComparison.Ordinal).ShouldBeTrue();
        segment.EndsWith(ReadOnlySpan<char>.Empty).ShouldBeTrue();
        segment.EndsWith(ReadOnlySpan<char>.Empty, StringComparison.Ordinal).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_LongerThanSegment_ReturnsFalse()
    {
        var segment = new StringSegment("hi");

        segment.StartsWith("hello").ShouldBeFalse();
        segment.StartsWith("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.StartsWith("hello".AsSpan()).ShouldBeFalse();
        segment.StartsWith("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_LongerThanSegment_ReturnsFalse()
    {
        var segment = new StringSegment("hi");

        segment.EndsWith("hello").ShouldBeFalse();
        segment.EndsWith("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.EndsWith("hello".AsSpan()).ShouldBeFalse();
        segment.EndsWith("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void Equals_String_ReturnsCorrectResult()
    {
        var segment = new StringSegment("hello");

        segment.Equals("hello").ShouldBeTrue();
        segment.Equals("world").ShouldBeFalse();
        segment.Equals(null).ShouldBeFalse();

        new StringSegment(null).Equals(null).ShouldBeTrue();
    }

    [Fact]
    public void Equals_StringSegment_ReturnsCorrectResult()
    {
        var segment1 = new StringSegment("hello");
        var segment2 = new StringSegment("hello");
        var segment3 = new StringSegment("world");

        segment1.Equals(segment2).ShouldBeTrue();
        segment1.Equals(segment3).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithComparison_ReturnsCorrectResult()
    {
        var segment = new StringSegment("Hello");

        segment.Equals("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.Equals("hello", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.Equals("HELLO", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperators_WorkCorrectly()
    {
        var segment1 = new StringSegment("hello");
        var segment2 = new StringSegment("hello");
        var segment3 = new StringSegment("world");

        (segment1 == segment2).ShouldBeTrue();
        (segment1 != segment3).ShouldBeTrue();
        (segment1 == "hello").ShouldBeTrue();
        (segment1 != "world").ShouldBeTrue();
        ("hello" == segment1).ShouldBeTrue();
        ("world" != segment1).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_ReturnsSameValueForEqualSegments()
    {
        var segment1 = new StringSegment("hello");
        var segment2 = new StringSegment("hello");
        var segment3 = new StringSegment("hello world", 0, 5);

        segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
        segment1.GetHashCode().ShouldBe(segment3.GetHashCode());
    }

    [Fact]
    public void GetHashCode_EmptySegment_ReturnsZero()
    {
        var segment = new StringSegment(string.Empty);

        segment.GetHashCode().ShouldBe(0);
    }

    [Fact]
    public void Trim_RemovesWhitespace()
    {
        new StringSegment("  hello  ").Trim().ToString().ShouldBe("hello");
        new StringSegment("\t\nhello\r\n").Trim().ToString().ShouldBe("hello");
        new StringSegment("hello").Trim().ToString().ShouldBe("hello");
        new StringSegment("   ").Trim().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Trim_WithChar_RemovesSpecifiedChar()
    {
        new StringSegment("xxhelloxx").Trim('x').ToString().ShouldBe("hello");
        new StringSegment("hello").Trim('x').ToString().ShouldBe("hello");
        new StringSegment("xxx").Trim('x').IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Trim_WithChars_RemovesSpecifiedChars()
    {
        new StringSegment("xy hello yx").Trim("xy ".AsSpan()).ToString().ShouldBe("hello");
        new StringSegment("###hello###").Trim("#@".AsSpan()).ToString().ShouldBe("hello");
    }

    [Fact]
    public void TrimStart_RemovesWhitespace()
    {
        new StringSegment("  hello  ").TrimStart().ToString().ShouldBe("hello  ");
        new StringSegment("\t\nhello").TrimStart().ToString().ShouldBe("hello");
        new StringSegment("hello").TrimStart().ToString().ShouldBe("hello");
        new StringSegment("   ").TrimStart().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimStart_WithChar_RemovesSpecifiedChar()
    {
        new StringSegment("xxhelloxx").TrimStart('x').ToString().ShouldBe("helloxx");
        new StringSegment("hello").TrimStart('x').ToString().ShouldBe("hello");
        new StringSegment("xxx").TrimStart('x').IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimStart_WithChars_RemovesSpecifiedChars()
    {
        new StringSegment("xy hello yx").TrimStart("xy ".AsSpan()).ToString().ShouldBe("hello yx");
        new StringSegment("###hello###").TrimStart("#@".AsSpan()).ToString().ShouldBe("hello###");
    }

    [Fact]
    public void TrimEnd_RemovesWhitespace()
    {
        new StringSegment("  hello  ").TrimEnd().ToString().ShouldBe("  hello");
        new StringSegment("hello\r\n").TrimEnd().ToString().ShouldBe("hello");
        new StringSegment("hello").TrimEnd().ToString().ShouldBe("hello");
        new StringSegment("   ").TrimEnd().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimEnd_WithChar_RemovesSpecifiedChar()
    {
        new StringSegment("xxhelloxx").TrimEnd('x').ToString().ShouldBe("xxhello");
        new StringSegment("hello").TrimEnd('x').ToString().ShouldBe("hello");
        new StringSegment("xxx").TrimEnd('x').IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimEnd_WithChars_RemovesSpecifiedChars()
    {
        new StringSegment("xy hello yx").TrimEnd("xy ".AsSpan()).ToString().ShouldBe("xy hello");
        new StringSegment("###hello###").TrimEnd("#@".AsSpan()).ToString().ShouldBe("###hello");
    }

    [Fact]
    public void ImplicitConversion_FromString_Works()
    {
        StringSegment segment = "hello";

        segment.Length.ShouldBe(5);
        segment.ToString().ShouldBe("hello");
    }

    [Fact]
    public void ImplicitConversion_ToMemory_Works()
    {
        var segment = new StringSegment("hello");
        ReadOnlyMemory<char> memory = segment;

        memory.Length.ShouldBe(5);
        memory.ToString().ShouldBe("hello");
    }

    [Fact]
    public void ImplicitConversion_ToSpan_Works()
    {
        var segment = new StringSegment("hello");
        ReadOnlySpan<char> span = segment;

        span.Length.ShouldBe(5);
        span.ToString().ShouldBe("hello");
    }

    [Fact]
    public void Memory_Property_ReturnsCorrectMemory()
    {
        var segment = new StringSegment("hello world", 6, 5);

        segment.Memory.Length.ShouldBe(5);
        segment.Memory.ToString().ShouldBe("world");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("a", false)]
    [InlineData("hello", false)]
    public void IsEmpty_ReturnsCorrectValue(string input, bool expectedEmpty)
    {
        var segment = new StringSegment(input);

        segment.IsEmpty.ShouldBe(expectedEmpty);
    }

    [Fact]
    public void ChainedOperations_WorkCorrectly()
    {
        var segment = new StringSegment("  hello world  ");

        var trimmed = segment.Trim();
        var sliced = trimmed.Slice(6);
        var result = sliced.Substring(0, 5);

        result.ShouldBe("world");
    }

    [Fact]
    public void SegmentFromSegment_WorksCorrectly()
    {
        var original = new StringSegment("hello world");
        var sliced = original.Slice(6);
        var furtherSliced = sliced.Slice(2);

        furtherSliced.ToString().ShouldBe("rld");
    }

    [Fact]
    public void ToString_OnEmptySegment_ReturnsEmptyString()
    {
        new StringSegment(string.Empty).ToString().ShouldBe(string.Empty);
        new StringSegment(null).ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Equals_Object_WorksCorrectly()
    {
        var segment = new StringSegment("hello");

        segment.Equals((object)"hello").ShouldBeTrue();
        segment.Equals((object)"world").ShouldBeFalse();
        segment.Equals((object)new StringSegment("hello")).ShouldBeTrue();
        segment.Equals((object)new StringSegment("world")).ShouldBeFalse();
        segment.Equals((object)123).ShouldBeFalse();
        segment.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void Empty_ReturnsEmptySegment()
    {
        var empty = StringSegment.Empty;

        empty.IsEmpty.ShouldBeTrue();
        empty.Length.ShouldBe(0);
        empty.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Empty_EqualsDefaultSegment()
    {
        var empty = StringSegment.Empty;
        StringSegment defaultSegment = default;

        empty.Equals(defaultSegment).ShouldBeTrue();
        (empty == defaultSegment).ShouldBeTrue();
    }

    [Fact]
    public void Empty_EqualsEmptyString()
    {
        var empty = StringSegment.Empty;

        empty.Equals(string.Empty).ShouldBeTrue();
        (empty == string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void Empty_EqualsNullString()
    {
        var empty = StringSegment.Empty;
        string? nullString = null;

        empty.Equals(nullString).ShouldBeTrue();
        (empty == nullString).ShouldBeTrue();
    }

    [Fact]
    public void Empty_HasZeroHashCode()
    {
        var empty = StringSegment.Empty;

        empty.GetHashCode().ShouldBe(0);
    }

    [Fact]
    public void Empty_OperationsReturnEmpty()
    {
        var empty = StringSegment.Empty;

        empty.Slice(0).IsEmpty.ShouldBeTrue();
        empty.Trim().IsEmpty.ShouldBeTrue();
        empty.TrimStart().IsEmpty.ShouldBeTrue();
        empty.TrimEnd().IsEmpty.ShouldBeTrue();
        empty.Substring(0).ShouldBe(string.Empty);
    }

    [Fact]
    public void Empty_IndexOfReturnsNegativeOne()
    {
        var empty = StringSegment.Empty;

        empty.IndexOf('a').ShouldBe(-1);
        empty.IndexOf("test").ShouldBe(-1);
    }

    [Fact]
    public void Empty_StartsWithAndEndsWithEmptyString()
    {
        var empty = StringSegment.Empty;

        empty.StartsWith(string.Empty).ShouldBeTrue();
        empty.EndsWith(string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void Empty_DoesNotStartOrEndWithNonEmptyString()
    {
        var empty = StringSegment.Empty;

        empty.StartsWith("test").ShouldBeFalse();
        empty.EndsWith("test").ShouldBeFalse();
    }
}
