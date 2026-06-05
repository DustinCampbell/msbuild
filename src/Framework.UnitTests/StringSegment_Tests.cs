// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace Microsoft.Build.Text.UnitTests;

public class StringSegmentTests
{
    [Fact]
    public void DefaultConstructor_CreatesEmptySegment()
    {
        StringSegment segment = new();
        segment.Length.ShouldBe(0);
        segment.IsEmpty.ShouldBeTrue();
        segment.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_WithString_InitializesCorrectly()
    {
        string value = "Hello";
        StringSegment segment = new(value);
        segment.Length.ShouldBe(value.Length);
        segment.IsEmpty.ShouldBeFalse();
        segment.ToString().ShouldBe(value);
    }

    [Fact]
    public void Constructor_WithNullString_ReturnsEmpty()
    {
        StringSegment segment = new(null!, 0, 0);
        segment.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithNegativeStart_ThrowsException()
        => Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("test", -1, 1));

    [Fact]
    public void Constructor_WithNegativeLength_ThrowsException()
        => Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("test", 0, -1));

    [Fact]
    public void Constructor_WithStartAndLengthExceedingBounds_ThrowsException()
        => Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("test", 3, 2)).Message.ShouldContain("Start and length exceed the bounds of the string");

    [Fact]
    public void Constructor_WithStartAndLength_InitializesCorrectly()
    {
        string value = "Hello World";
        StringSegment segment = new(value, 6, 5);
        segment.Length.ShouldBe(5);
        segment.IsEmpty.ShouldBeFalse();
        segment.ToString().ShouldBe("World");
    }

    [Fact]
    public void Constructor_WithStart_InitializesCorrectly()
    {
        string value = "Hello World";
        StringSegment segment = new(value, 6);
        segment.Length.ShouldBe(5);
        segment.IsEmpty.ShouldBeFalse();
        segment.ToString().ShouldBe("World");
    }

    [Fact]
    public void Constructor_NegativeStartIndex_Throws()
        => Should.Throw<ArgumentOutOfRangeException>(() => _ = new StringSegment("Hello", -1, 2));

    [Fact]
    public void Constructor_NegativeLength_Throws()
        => Should.Throw<ArgumentOutOfRangeException>(() => _ = new StringSegment("Hello", 0, -1));

    [Fact]
    public void Indexer_ReturnsCorrectCharacter()
    {
        StringSegment segment = new("Hello", 1, 3);
        segment[0].ShouldBe('e');
        segment[1].ShouldBe('l');
        segment[2].ShouldBe('l');
    }

    [Fact]
    public void Indexer_WithInvalidIndex_ThrowsException()
    {
        StringSegment segment = new("Hello");
        Should.Throw<IndexOutOfRangeException>(() => _ = segment[5]);
    }

    [Fact]
    public void RangeIndexer_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment subSegment = segment[6..11];
        subSegment.ToString().ShouldBe("World");
    }

    [Fact]
    public void Slice_WithStart_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment sliced = segment[6..];
        sliced.ToString().ShouldBe("World");
    }

    [Fact]
    public void Slice_WithStartOutOfRange_ThrowsException()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => _ = segment[6..]);
    }

    [Fact]
    public void Slice_WithStartAndLength_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment sliced = segment[..5];
        sliced.ToString().ShouldBe("Hello");
    }

    [Fact]
    public void Slice_WithStartAndLengthOutOfRange_ThrowsException()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => segment.Slice(2, 4));
    }

    [Fact]
    public void TrySplit_WithDelimiterPresent_SplitsCorrectly()
    {
        StringSegment segment = new("Hello,World");
        bool result = segment.TrySplit(',', out StringSegment left, out StringSegment right);

        result.ShouldBeTrue();
        left.ToString().ShouldBe("Hello");
        right.ToString().ShouldBe("World");
    }

    [Fact]
    public void TrySplit_WithDelimiterNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("HelloWorld");
        bool result = segment.TrySplit(',', out StringSegment left, out StringSegment right);

        result.ShouldBeTrue();
        left.ToString().ShouldBe("HelloWorld");
        right.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrySplit_WithEmptySegment_ReturnsFalse()
    {
        StringSegment segment = new();
        bool result = segment.TrySplit(',', out StringSegment left, out StringSegment right);

        result.ShouldBeFalse();
        left.IsEmpty.ShouldBeTrue();
        right.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrySplitAny_WithSameDelimiters_SplitsCorrectly()
    {
        StringSegment segment = new("Hello,World");
        bool result = segment.TrySplitAny(',', ',', out StringSegment left, out StringSegment right);

        result.ShouldBeTrue();
        left.ToString().ShouldBe("Hello");
        right.ToString().ShouldBe("World");
    }

    [Fact]
    public void TrySplitAny_WithDifferentDelimiters_SplitsCorrectly()
    {
        StringSegment segment = new("Hello;World,Test");
        bool result = segment.TrySplitAny(',', ';', out StringSegment left, out StringSegment right);

        result.ShouldBeTrue();
        left.ToString().ShouldBe("Hello");
        right.ToString().ShouldBe("World,Test");
    }

    [Fact]
    public void Contains_WithCharPresent_ReturnsTrue()
    {
        StringSegment segment = new("Hello");
        segment.Contains('e').ShouldBeTrue();
    }

    [Fact]
    public void Contains_WithCharNotPresent_ReturnsFalse()
    {
        StringSegment segment = new("Hello");
        segment.Contains('z').ShouldBeFalse();
    }

    [Fact]
    public void IndexOf_WithCharPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello", 1, 3);
        segment.IndexOf('l').ShouldBe(1);
    }

    [Fact]
    public void IndexOf_WithCharNotPresent_ReturnsMinusOne()
    {
        StringSegment segment = new("Hello");
        segment.IndexOf('z').ShouldBe(-1);
    }

    [Fact]
    public void IndexOfAny_WithCharsPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello");
        segment.IndexOfAny('e', 'o').ShouldBe(1);
    }

    [Fact]
    public void Replace_ReplacesAllOccurrences()
    {
        StringSegment segment = new("Hello");
        StringSegment replaced = segment.Replace('l', 'x');
        replaced.ToString().ShouldBe("Hexxo");
    }

    [Fact]
    public void Replace_WithNoMatches_ReturnsSameSegment()
    {
        StringSegment segment = new("Hello");
        StringSegment replaced = segment.Replace('z', 'x');
        replaced.ShouldBe(segment);
    }

    [Fact]
    public void Replace_WithNoMatches_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello");
        StringSegment replaced = segment.Replace('z', 'x');
        replaced.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSpan()
    {
        StringSegment segment = new("Hello", 1, 3);
        ReadOnlySpan<char> span = segment.AsSpan();
        span.ToString().ShouldBe("ell");
    }

    [Fact]
    public void AsSpan_WithStart_ReturnsCorrectSpan()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment.AsSpan(1);
        span.ToString().ShouldBe("ello");
    }

    [Fact]
    public void AsSpan_WithStartAndLength_ReturnsCorrectSpan()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment.AsSpan(1, 3);
        span.ToString().ShouldBe("ell");
    }

    [Fact]
    public void ImplicitConversion_ToReadOnlySpan_Succeeds()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment;
        span.ToString().ShouldBe("Hello");
    }

    [Fact]
    public void ExplicitConversion_ToString_Succeeds()
    {
        StringSegment segment = new("Hello");
        string value = (string)segment;
        value.ShouldBe("Hello");
    }

    [Fact]
    public void ImplicitConversion_FromString_Succeeds()
    {
        string value = "Hello";
        StringSegment segment = value;
        segment.ToString().ShouldBe("Hello");
    }

    [Fact]
    public void Equals_WithIdenticalSegments_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("Hello");
        segment1.Equals(segment2).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithDifferentSegments_ReturnsFalse()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("World");
        segment1.Equals(segment2).ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithIgnoreCase_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("hello");
        segment1.Equals(segment2, true).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithReadOnlySpan_ReturnsTrue()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = "Hello".AsSpan();
        segment.Equals(span).ShouldBeTrue();
    }

    [Fact]
    public void EqualsOperator_WithIdenticalSegments_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("Hello");
        (segment1 == segment2).ShouldBeTrue();
    }

    [Fact]
    public void NotEqualsOperator_WithDifferentSegments_ReturnsTrue()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("World");
        (segment1 != segment2).ShouldBeTrue();
    }

    [Fact]
    public void GetHashCode_ReturnsSameValueForEqualSegments()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("Hello");
        segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
    }

    [Fact]
    public void ToString_WithFullSegment_ReturnsOriginalString()
    {
        string original = "Hello";
        StringSegment segment = new(original);
        segment.ToString().ShouldBeSameAs(original);
    }

    [Fact]
    public void ToString_WithPartialSegment_ReturnsSubstring()
    {
        StringSegment segment = new("Hello World", 6, 5);
        segment.ToString().ShouldBe("World");
    }

    [Fact]
    public void IndexOfAny_WithReadOnlySpan_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");

        // Empty span
        segment.IndexOfAny([]).ShouldBe(-1);

        // Single char span
        segment.IndexOfAny("e".AsSpan()).ShouldBe(1);

        // Two char span
        segment.IndexOfAny("lo".AsSpan()).ShouldBe(2);

        // Multiple chars span
        segment.IndexOfAny("xyzW".AsSpan()).ShouldBe(6);

        // No match
        segment.IndexOfAny("xyz".AsSpan()).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOf_WithCharPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");
        segment.LastIndexOf('l').ShouldBe(9);

        // Character at the beginning
        segment.LastIndexOf('H').ShouldBe(0);

        // Character at the end
        segment.LastIndexOf('d').ShouldBe(10);

        // Character not present
        segment.LastIndexOf('z').ShouldBe(-1);

        // Segment with multiple occurrences
        StringSegment multipleOccurrences = new("Hello", 1, 3);  // "ell"
        multipleOccurrences.LastIndexOf('l').ShouldBe(2);
    }

    [Fact]
    public void Equals_WithDifferentComparisonTypes_WorksCorrectly()
    {
        StringSegment lower = new("hello world");
        StringSegment upper = new("HELLO WORLD");
        StringSegment mixed = new("Hello World");

        // Ordinal comparison (case sensitive)
        lower.Equals(upper, StringComparison.Ordinal).ShouldBeFalse();
        lower.Equals(lower, StringComparison.Ordinal).ShouldBeTrue();

        // OrdinalIgnoreCase comparison
        lower.Equals(upper, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();

        // CurrentCulture comparison
        mixed.Equals(upper, StringComparison.CurrentCulture).ShouldBeFalse();
        mixed.Equals(upper, StringComparison.CurrentCultureIgnoreCase).ShouldBeTrue();

        // InvariantCulture comparison
        mixed.Equals(upper, StringComparison.InvariantCulture).ShouldBeFalse();
        mixed.Equals(upper, StringComparison.InvariantCultureIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void Equals_WithString_HandlesEdgeCases()
    {
        StringSegment empty = new();
        StringSegment segment = new("Hello World");
        StringSegment partial = new("Hello World", 6, 5);  // "World"

        // Null string
        empty.Equals((string?)null).ShouldBeFalse();
        segment.Equals((string?)null).ShouldBeFalse();

        // Empty string
        empty.Equals(string.Empty).ShouldBeTrue();
        segment.Equals(string.Empty).ShouldBeFalse();

        // Full match
        segment.Equals("Hello World").ShouldBeTrue();

        // Partial match
        partial.Equals("World").ShouldBeTrue();
        partial.Equals("Hello").ShouldBeFalse();

        // Different length
        segment.Equals("Hello").ShouldBeFalse();
    }

    [Fact]
    public void Equals_WithObject_HandlesVariousTypes()
    {
        StringSegment segment = new("Hello");

        // Same value as string
        object stringObj = "Hello";
        segment.Equals(stringObj).ShouldBeTrue();

        // Same value as StringSegment
        object segmentObj = new StringSegment("Hello");
        segment.Equals(segmentObj).ShouldBeTrue();

        // Different value as string
        object differentStringObj = "World";
        segment.Equals(differentStringObj).ShouldBeFalse();

        // Different value as StringSegment
        object differentSegmentObj = new StringSegment("World");
        segment.Equals(differentSegmentObj).ShouldBeFalse();

        // Null object
        segment.Equals((object?)null).ShouldBeFalse();

        // Different type
        object intObj = 42;
        segment.Equals(intObj).ShouldBeFalse();
    }

    [Fact]
    public void LastIndexOf_WithSegmentSlice_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World Hello");
        StringSegment sliced = segment[6..11];  // "World"

        sliced.LastIndexOf('o').ShouldBe(1);
        sliced.LastIndexOf('l').ShouldBe(3);
        sliced.LastIndexOf('W').ShouldBe(0);
        sliced.LastIndexOf('H').ShouldBe(-1);
    }

    [Fact]
    public void EmptySegment_MethodBehaviors()
    {
        StringSegment empty = new();

        // Index operations
        empty.IndexOf('a').ShouldBe(-1);
        empty.IndexOfAny('a', 'b').ShouldBe(-1);
        empty.IndexOfAny("abc".AsSpan()).ShouldBe(-1);
        empty.LastIndexOf('a').ShouldBe(-1);

        // AsSpan
        empty.AsSpan().Length.ShouldBe(0);

        // Replace
        StringSegment replaced = empty.Replace('a', 'b');
        replaced.IsEmpty.ShouldBeTrue();

        // Equals
        empty.Equals(string.Empty).ShouldBeTrue();
        empty.Equals(new StringSegment()).ShouldBeTrue();
        empty.Equals("a").ShouldBeFalse();
    }

    [Fact]
    public void EmptySegment_Indexer_ThrowsException()
    {
        StringSegment empty = new();
        Should.Throw<IndexOutOfRangeException>(() => _ = empty[0]);
    }

    [Fact]
    public void EmptySegment_RangeIndexer_ReturnsEmptySegment()
    {
        StringSegment empty = new();
        StringSegment result = empty[0..0];
        result.IsEmpty.ShouldBeTrue();
        result.Length.ShouldBe(0);
    }

    [Fact]
    public void EmptySegment_SliceWithStart_ReturnsEmptySegmentForZero()
    {
        StringSegment empty = new();
        StringSegment result = empty[..];
        result.IsEmpty.ShouldBeTrue();
        result.Length.ShouldBe(0);
    }

    [Fact]
    public void EmptySegment_SliceWithStart_ThrowsForNonZero()
    {
        StringSegment empty = new();
        Should.Throw<InternalErrorException>(() => _ = empty[1..]);
    }

    [Fact]
    public void EmptySegment_SliceWithStartAndLength_ReturnsEmptySegmentForZeros()
    {
        StringSegment empty = new();
        StringSegment result = empty[..0];
        result.IsEmpty.ShouldBeTrue();
        result.Length.ShouldBe(0);
    }

    [Fact]
    public void EmptySegment_SliceWithStartAndLength_ThrowsForNonZeroLength()
    {
        StringSegment empty = new();
        Should.Throw<InternalErrorException>(() => _ = empty[..1]);
    }

    [Fact]
    public void EmptySegment_TrySplitAny_ReturnsFalse()
    {
        StringSegment empty = new();
        bool result = empty.TrySplitAny(',', ';', out StringSegment left, out StringSegment right);
        result.ShouldBeFalse();
        left.IsEmpty.ShouldBeTrue();
        right.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void EmptySegment_Contains_ReturnsFalse()
    {
        StringSegment empty = new();
        empty.Contains('a').ShouldBeFalse();
    }

    [Fact]
    public void EmptySegment_EqualsOperator_WorksCorrectly()
    {
        StringSegment empty1 = new();
        StringSegment empty2 = new();
        StringSegment nonEmpty = new("test");

        (empty1 == empty2).ShouldBeTrue();
        (empty1 != nonEmpty).ShouldBeTrue();
        (empty1 == string.Empty).ShouldBeTrue();
    }

    [Fact]
    public void EmptySegment_Conversions_WorkCorrectly()
    {
        StringSegment empty = new();

        string str = (string)empty;
        str.ShouldBe(string.Empty);

        ReadOnlySpan<char> span = empty;
        span.IsEmpty.ShouldBeTrue();
        span.Length.ShouldBe(0);
    }

    [Fact]
    public void StartsWith_WithString_ReturnsTrueForPrefix()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith("Hello").ShouldBeTrue();
        segment.StartsWith("H").ShouldBeTrue();
        segment.StartsWith("Hello World").ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_WithString_ReturnsFalseForNonPrefix()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith("World").ShouldBeFalse();
        segment.StartsWith("hello").ShouldBeFalse(); // Case sensitive by default
        segment.StartsWith("Hello World!").ShouldBeFalse(); // Longer than segment
    }

    [Fact]
    public void StartsWith_WithString_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith("hello", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith("hello", StringComparison.Ordinal).ShouldBeFalse();
        segment.StartsWith("HELLO", StringComparison.InvariantCultureIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_WithStringSegment_ReturnsTrueForPrefix()
    {
        StringSegment segment = new("Hello World");
        StringSegment prefix1 = new("Hello");
        StringSegment prefix2 = new("Hello World", 0, 5); // "Hello"

        segment.StartsWith(prefix1).ShouldBeTrue();
        segment.StartsWith(prefix2).ShouldBeTrue();
        segment.StartsWith(segment).ShouldBeTrue(); // Same segment
    }

    [Fact]
    public void StartsWith_WithStringSegment_ReturnsFalseForNonPrefix()
    {
        StringSegment segment = new("Hello World");
        StringSegment nonPrefix1 = new("World");
        StringSegment nonPrefix2 = new("Hello World", 6, 5); // "World"
        StringSegment nonPrefix3 = new("hello"); // Case sensitive by default

        segment.StartsWith(nonPrefix1).ShouldBeFalse();
        segment.StartsWith(nonPrefix2).ShouldBeFalse();
        segment.StartsWith(nonPrefix3).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_WithStringSegment_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        StringSegment lowerPrefix = new("hello");

        segment.StartsWith(lowerPrefix, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith(lowerPrefix, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_WithReadOnlySpan_ReturnsTrueForPrefix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> prefix1 = "Hello".AsSpan();
        ReadOnlySpan<char> prefix2 = "H".AsSpan();

        segment.StartsWith(prefix1).ShouldBeTrue();
        segment.StartsWith(prefix2).ShouldBeTrue();
        segment.StartsWith(segment.AsSpan()).ShouldBeTrue(); // Full span
    }

    [Fact]
    public void StartsWith_WithReadOnlySpan_ReturnsFalseForNonPrefix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> nonPrefix1 = "World".AsSpan();
        ReadOnlySpan<char> nonPrefix2 = "hello".AsSpan(); // Case sensitive by default

        segment.StartsWith(nonPrefix1).ShouldBeFalse();
        segment.StartsWith(nonPrefix2).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_WithReadOnlySpan_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> lowerPrefix = "hello".AsSpan();

        segment.StartsWith(lowerPrefix, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.StartsWith(lowerPrefix, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_WithEmptyValues_ReturnsExpectedResults()
    {
        string hello = "Hello";
        hello.StartsWith("", StringComparison.Ordinal).ShouldBeTrue();
        Should.Throw<ArgumentNullException>(() => hello.StartsWith(null!, StringComparison.Ordinal));

        "".StartsWith("", StringComparison.Ordinal).ShouldBeTrue();

        StringSegment segment = new("Hello");
        StringSegment empty = new();

        // Empty values should never match as a prefix
        segment.StartsWith(string.Empty).ShouldBeTrue();
        segment.StartsWith(empty).ShouldBeTrue();
        segment.StartsWith("".AsSpan()).ShouldBeTrue();

        // Empty segment behavior
        empty.StartsWith(string.Empty).ShouldBeTrue();
        empty.StartsWith(empty).ShouldBeTrue();
        empty.StartsWith("".AsSpan()).ShouldBeTrue();
        empty.StartsWith("Hello").ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_WithPartialSegment_WorksCorrectly()
    {
        StringSegment segment = new("Hello World", 6, 5); // "World"

        segment.StartsWith("Wo").ShouldBeTrue();
        segment.StartsWith("World").ShouldBeTrue();
        segment.StartsWith("Hello").ShouldBeFalse();
        segment.StartsWith("Worlds").ShouldBeFalse();
    }

    [Fact]
    public void Trim_RemovesLeadingAndTrailingWhitespace()
    {
        StringSegment segment = new("  Hello World  ");
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void TrimStart_RemovesLeadingWhitespace()
    {
        StringSegment segment = new("  Hello World  ");
        StringSegment trimmed = segment.TrimStart();
        trimmed.ToString().ShouldBe("Hello World  ");
    }

    [Fact]
    public void TrimEnd_RemovesTrailingWhitespace()
    {
        StringSegment segment = new("  Hello World  ");
        StringSegment trimmed = segment.TrimEnd();
        trimmed.ToString().ShouldBe("  Hello World");
    }

    [Fact]
    public void Trim_WithNoWhitespace_ReturnsOriginalString()
    {
        string original = "HelloWorld";
        StringSegment segment = new(original);
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().ShouldBeSameAs(original);
    }

    [Fact]
    public void Trim_WithNoWhitespace_ReturnsSameInstance()
    {
        string original = "HelloWorld";
        StringSegment segment = new(original);
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().ShouldBeSameAs(original);
    }

    [Fact]
    public void TrimStart_WithNoLeadingWhitespace_ReturnsSameInstance()
    {
        StringSegment segment = new("HelloWorld");
        StringSegment trimmed = segment.TrimStart();
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void TrimEnd_WithNoTrailingWhitespace_ReturnsSameInstance()
    {
        StringSegment segment = new("HelloWorld");
        StringSegment trimmed = segment.TrimEnd();
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void Trim_WithEmptySegment_ReturnsEmptySegment()
    {
        StringSegment segment = new();
        StringSegment trimmed = segment.Trim();
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Trim_WithOnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   \t\n\r   ");
        StringSegment trimmed = segment.Trim();
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimStart_WithOnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   \t\n\r   ");
        StringSegment trimmed = segment.TrimStart();
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimEnd_WithOnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   \t\n\r   ");
        StringSegment trimmed = segment.TrimEnd();
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Trim_WithPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("  Hello World  ", 2, 11);
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void TrimStart_WithPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("  Hello World  ", 2, 11);
        StringSegment trimmed = segment.TrimStart();
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void TrimEnd_WithPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("  Hello World  ", 1, 11);
        StringSegment trimmed = segment.TrimEnd();
        trimmed.ToString().ShouldBe(" Hello Worl");
    }

    [Fact]
    public void Trim_WithVariousWhitespaceCharacters_TrimsCorrectly()
    {
        StringSegment segment = new("\t \n\rHello World\r\n \t");
        StringSegment trimmed = segment.Trim();
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void EndsWith_WithString_ReturnsTrueForSuffix()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith("World").ShouldBeTrue();
        segment.EndsWith("d").ShouldBeTrue();
        segment.EndsWith("Hello World").ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_WithString_ReturnsFalseForNonSuffix()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith("Hello").ShouldBeFalse();
        segment.EndsWith("world").ShouldBeFalse(); // Case sensitive by default
        segment.EndsWith("!Hello World").ShouldBeFalse(); // Longer than segment
    }

    [Fact]
    public void EndsWith_WithString_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith("world", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith("world", StringComparison.Ordinal).ShouldBeFalse();
        segment.EndsWith("WORLD", StringComparison.InvariantCultureIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_WithStringSegment_ReturnsTrueForSuffix()
    {
        StringSegment segment = new("Hello World");
        StringSegment suffix1 = new("World");
        StringSegment suffix2 = new("Hello World", 6, 5); // "World"

        segment.EndsWith(suffix1).ShouldBeTrue();
        segment.EndsWith(suffix2).ShouldBeTrue();
        segment.EndsWith(segment).ShouldBeTrue(); // Same segment
    }

    [Fact]
    public void EndsWith_WithStringSegment_ReturnsFalseForNonSuffix()
    {
        StringSegment segment = new("Hello World");
        StringSegment nonSuffix1 = new("Hello");
        StringSegment nonSuffix2 = new("Hello World", 0, 5); // "Hello"
        StringSegment nonSuffix3 = new("world"); // Case sensitive by default

        segment.EndsWith(nonSuffix1).ShouldBeFalse();
        segment.EndsWith(nonSuffix2).ShouldBeFalse();
        segment.EndsWith(nonSuffix3).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_WithStringSegment_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        StringSegment lowerSuffix = new("world");

        segment.EndsWith(lowerSuffix, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith(lowerSuffix, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_WithReadOnlySpan_ReturnsTrueForSuffix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> suffix1 = "World".AsSpan();
        ReadOnlySpan<char> suffix2 = "d".AsSpan();

        segment.EndsWith(suffix1).ShouldBeTrue();
        segment.EndsWith(suffix2).ShouldBeTrue();
        segment.EndsWith(segment.AsSpan()).ShouldBeTrue(); // Full span
    }

    [Fact]
    public void EndsWith_WithReadOnlySpan_ReturnsFalseForNonSuffix()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> nonSuffix1 = "Hello".AsSpan();
        ReadOnlySpan<char> nonSuffix2 = "world".AsSpan(); // Case sensitive by default

        segment.EndsWith(nonSuffix1).ShouldBeFalse();
        segment.EndsWith(nonSuffix2).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_WithReadOnlySpan_HandlesCaseComparison()
    {
        StringSegment segment = new("Hello World");
        ReadOnlySpan<char> lowerSuffix = "world".AsSpan();

        segment.EndsWith(lowerSuffix, StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
        segment.EndsWith(lowerSuffix, StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_WithEmptyValues_ReturnsExpectedResults()
    {
        StringSegment segment = new("Hello");
        StringSegment empty = new();

        // Empty values should always match as a suffix
        segment.EndsWith(string.Empty).ShouldBeTrue();
        segment.EndsWith(empty).ShouldBeTrue();
        segment.EndsWith("".AsSpan()).ShouldBeTrue();

        // Empty segment behavior
        empty.EndsWith(string.Empty).ShouldBeTrue();
        empty.EndsWith(empty).ShouldBeTrue();
        empty.EndsWith("".AsSpan()).ShouldBeTrue();
        empty.EndsWith("Hello").ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_WithPartialSegment_WorksCorrectly()
    {
        StringSegment segment = new("Hello World", 0, 5); // "Hello"

        segment.EndsWith("lo").ShouldBeTrue();
        segment.EndsWith("Hello").ShouldBeTrue();
        segment.EndsWith("World").ShouldBeFalse();
        segment.EndsWith("xHello").ShouldBeFalse();
    }

    [Fact]
    public void LastIndexOfAny_WithCharsPresent_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");
        segment.LastIndexOfAny('l', 'o').ShouldBe(9); // 'l' at index 9 is the last occurrence

        // Character at the beginning
        segment.LastIndexOfAny('H', 'x').ShouldBe(0);

        // Character at the end
        segment.LastIndexOfAny('x', 'd').ShouldBe(10);

        // Multiple occurrences of both characters
        segment.LastIndexOfAny('l', 'o').ShouldBe(9); // 'l' at index 9 comes after 'o' at index 7

        // Same character provided twice
        segment.LastIndexOfAny('l', 'l').ShouldBe(9);
    }

    [Fact]
    public void LastIndexOfAny_WithCharsNotPresent_ReturnsMinusOne()
    {
        StringSegment segment = new("Hello World");
        segment.LastIndexOfAny('z', 'y').ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithEmptySegment_ReturnsMinusOne()
    {
        StringSegment empty = new();
        empty.LastIndexOfAny('a', 'b').ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithPartialSegment_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World", 3, 5); // "lo Wo"
        segment.LastIndexOfAny('l', 'o').ShouldBe(4);  // 'o' at index 4 relative to the segment
        segment.LastIndexOfAny('H', 'e').ShouldBe(-1); // Not in this segment
    }

    [Fact]
    public void LastIndexOfAny_WithReadOnlySpan_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World");

        // Empty span
        segment.LastIndexOfAny([]).ShouldBe(-1);

        // Single char span
        segment.LastIndexOfAny("l".AsSpan()).ShouldBe(9);

        // Two char span
        segment.LastIndexOfAny("lo".AsSpan()).ShouldBe(9);

        // Multiple chars span
        segment.LastIndexOfAny("xyzWdol".AsSpan()).ShouldBe(10); // 'd' at index 10

        // No match
        segment.LastIndexOfAny("xyz".AsSpan()).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithReadOnlySpan_AndEmptySegment_ReturnsMinusOne()
    {
        StringSegment empty = new();
        empty.LastIndexOfAny("abc".AsSpan()).ShouldBe(-1);
    }

    [Fact]
    public void LastIndexOfAny_WithReadOnlySpan_AndPartialSegment_ReturnsCorrectIndex()
    {
        StringSegment segment = new("Hello World Hello", 6, 5); // "World"

        segment.LastIndexOfAny("dlr".AsSpan()).ShouldBe(4);  // 'd' at index 4 relative to the segment
        segment.LastIndexOfAny("o".AsSpan()).ShouldBe(1);
        segment.LastIndexOfAny("W".AsSpan()).ShouldBe(0);
        segment.LastIndexOfAny("HZ".AsSpan()).ShouldBe(-1);
    }

    [Fact]
    public void Trim_WithSpecificChar_RemovesLeadingAndTrailingChar()
    {
        StringSegment segment = new("###Hello World###");
        StringSegment trimmed = segment.Trim('#');
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void Trim_WithSpecificChar_WhenNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.Trim('#');
        trimmed.ShouldBe(segment);
    }

    [Fact]
    public void Trim_WithSpecificChar_WhenNotPresent_ReturnsSameInstance()
    {
        string original = "Hello World";
        StringSegment segment = new(original);
        StringSegment trimmed = segment.Trim('#');
        trimmed.ToString().ShouldBeSameAs(original);
    }

    [Fact]
    public void Trim_WithSpecificChar_WhenOnlyTrimChar_ReturnsEmptySegment()
    {
        StringSegment segment = new("######");
        StringSegment trimmed = segment.Trim('#');
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_RemovesLeadingAndTrailingChars()
    {
        StringSegment segment = new("###Hello*World***");
        StringSegment trimmed = segment.Trim('#', '*');
        trimmed.ToString().ShouldBe("Hello*World");
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_WhenOnlyTrimChars_ReturnsEmptySegment()
    {
        StringSegment segment = new("##**##**##");
        StringSegment trimmed = segment.Trim('#', '*');
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.Trim('#', '*');
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void TrimStart_WithSpecificChar_RemovesLeadingChar()
    {
        StringSegment segment = new("###Hello World###");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.ToString().ShouldBe("Hello World###");
    }

    [Fact]
    public void TrimStart_WithSpecificChar_WhenNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.ShouldBe(segment);
    }

    [Fact]
    public void TrimStart_WithSpecificChar_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void TrimStart_WithSpecificChar_WhenOnlyTrimChar_ReturnsEmptySegment()
    {
        StringSegment segment = new("######");
        StringSegment trimmed = segment.TrimStart('#');
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimStart_WithTwoSpecificChars_RemovesLeadingChars()
    {
        StringSegment segment = new("##**Hello World");
        StringSegment trimmed = segment.TrimStart('#', '*');
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void TrimStart_WithTwoSpecificChars_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimStart('#', '*');
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_RemovesTrailingChar()
    {
        StringSegment segment = new("###Hello World###");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.ToString().ShouldBe("###Hello World");
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_WhenNotPresent_ReturnsOriginalSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.ShouldBe(segment);
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void TrimEnd_WithSpecificChar_WhenOnlyTrimChar_ReturnsEmptySegment()
    {
        StringSegment segment = new("######");
        StringSegment trimmed = segment.TrimEnd('#');
        trimmed.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimEnd_WithTwoSpecificChars_RemovesTrailingChars()
    {
        StringSegment segment = new("Hello World##**");
        StringSegment trimmed = segment.TrimEnd('#', '*');
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void TrimEnd_WithTwoSpecificChars_WhenNotPresent_ReturnsSameInstance()
    {
        StringSegment segment = new("Hello World");
        StringSegment trimmed = segment.TrimEnd('#', '*');
        trimmed.ToString().ShouldBeSameAs((string)segment);
    }

    [Fact]
    public void Trim_WithSpecificChar_OnPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("###Hello World###", 3, 11); // "Hello World"
        StringSegment trimmed = segment.Trim('#');
        trimmed.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void Trim_WithTwoSpecificChars_OnPartialSegment_TrimsCorrectly()
    {
        StringSegment segment = new("###Hello*World###", 3, 11); // "Hello*World"
        StringSegment trimmed = segment.Trim('*', '#');
        trimmed.ToString().ShouldBe("Hello*World");
    }

    [Fact]
    public unsafe void Pinning_NonEmptySegment_ProvidesValidPointer()
    {
        string original = "Hello World";
        StringSegment segment = new(original);

        fixed (char* pSegment = segment)
        {
            // Pointer should not be null
            ((nint)pSegment).ShouldNotBe(0);

            // Should be able to read characters through the pointer
            for (int i = 0; i < segment.Length; i++)
            {
                pSegment[i].ShouldBe(original[i]);
            }
        }
    }

    [Fact]
    public unsafe void Pinning_PartialSegment_ProvidesValidPointerToSubstring()
    {
        string original = "Hello World";
        StringSegment segment = new(original, 6, 5); // "World"

        fixed (char* pSegment = segment)
        {
            // Pointer should not be null
            (pSegment is null).ShouldBeFalse();

            // Should be able to read characters through the pointer
            for (int i = 0; i < segment.Length; i++)
            {
                pSegment[i].ShouldBe(original[i + 6]);
            }

            // The pointer should point to the correct position in the original string
            fixed (char* pOriginal = original)
            {
                // The segment pointer should be offset from the original string
                nint offset = (nint)(pSegment - pOriginal);
                offset.ShouldBe((nint)6);
            }
        }
    }

    [Fact]
    public unsafe void Pinning_EmptySegment_ReturnsNullPointer()
    {
        StringSegment empty = new();

        fixed (char* pEmpty = empty)
        {
            // Empty segment should return a null pointer
            (pEmpty is null).ShouldBeTrue();
        }
    }

    [Fact]
    public unsafe void Pinning_SegmentWithEmptyString_ReturnsNonNullPointer()
    {
        // Empty string is different from a null string - it's a valid but zero-length buffer
        string emptyString = string.Empty;
        StringSegment segment = new(emptyString);

        fixed (char* pSegment = segment)
        {
            // Empty string segment should return a null pointer (to avoid empty string's buffer)
            (pSegment is null).ShouldBeTrue();
        }
    }

    [Fact]
    public unsafe void Pinning_SegmentAfterSlicing_ProvidesCorrectPointer()
    {
        string original = "Hello World";
        StringSegment segment = new(original);
        StringSegment sliced = segment.Slice(6, 5); // "World"

        fixed (char* pSliced = sliced)
        {
            fixed (char* pOriginal = original)
            {
                // The sliced pointer should be offset from the original string
                nint offset = (nint)(pSliced - pOriginal);
                offset.ShouldBe((nint)6);

                // Verify content
                for (int i = 0; i < sliced.Length; i++)
                {
                    pSliced[i].ShouldBe(original[i + 6]);
                }
            }
        }
    }

    [Fact]
    public void GetHashCode_IsConsistentForSameContent()
    {
        // Multiple calls should return the same hash code
        StringSegment segment = new("Test String");
        int hash1 = segment.GetHashCode();
        int hash2 = segment.GetHashCode();

        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void CompareTo_StringSegment_Ordinal_WorksCorrectly()
    {
        // Test segments with different positions
        StringSegment segment1 = new("Hello World", 0, 5);  // "Hello"
        StringSegment segment2 = new("Hello World", 6, 5);  // "World"
        StringSegment segment3 = new("Hello World", 0, 11); // "Hello World"
        StringSegment segment4 = new("Hello", 0, 5);        // "Hello"
        StringSegment segment5 = new("hello", 0, 5);        // "hello"
        StringSegment empty = new();

        // Same content from different sources
        segment1.CompareTo(segment4).ShouldBe(
            string.Compare("Hello", "Hello", StringComparison.Ordinal));

        // Different content
        segment1.CompareTo(segment2).ShouldBe(
            string.Compare("Hello", "World", StringComparison.Ordinal));
        segment2.CompareTo(segment1).ShouldBe(
            string.Compare("World", "Hello", StringComparison.Ordinal));

        // Comparing with whole string
        segment1.CompareTo(segment3).ShouldBe(
            string.Compare("Hello", "Hello World", StringComparison.Ordinal));
        segment3.CompareTo(segment1).ShouldBe(
            string.Compare("Hello World", "Hello", StringComparison.Ordinal));

        // Case sensitivity (ordinal is case-sensitive)
        segment1.CompareTo(segment5).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.Ordinal));
        segment5.CompareTo(segment1).ShouldBe(
            string.Compare("hello", "Hello", StringComparison.Ordinal));

        // Empty segment
        empty.CompareTo(segment1).ShouldBe(
            string.Compare("", "Hello", StringComparison.Ordinal));
        segment1.CompareTo(empty).ShouldBe(
            string.Compare("Hello", "", StringComparison.Ordinal));
        empty.CompareTo(empty).ShouldBe(
            string.Compare("", "", StringComparison.Ordinal));
    }

    [Fact]
    public void CompareTo_StringSegment_WithComparison_WorksCorrectly()
    {
        StringSegment segment1 = new("Hello World", 0, 5);  // "Hello"
        StringSegment segment2 = new("hello world", 0, 5);  // "hello"

        // Ordinal (case-sensitive)
        segment1.CompareTo(segment2, StringComparison.Ordinal).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.Ordinal));

        // OrdinalIgnoreCase
        segment1.CompareTo(segment2, StringComparison.OrdinalIgnoreCase).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.OrdinalIgnoreCase));

        // CurrentCulture
        segment1.CompareTo(segment2, StringComparison.CurrentCulture).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.CurrentCulture));

        // CurrentCultureIgnoreCase
        segment1.CompareTo(segment2, StringComparison.CurrentCultureIgnoreCase).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.CurrentCultureIgnoreCase));

        // InvariantCulture
        segment1.CompareTo(segment2, StringComparison.InvariantCulture).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.InvariantCulture));

        // InvariantCultureIgnoreCase
        segment1.CompareTo(segment2, StringComparison.InvariantCultureIgnoreCase).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.InvariantCultureIgnoreCase));
    }

    [Fact]
    public void CompareTo_String_Ordinal_WorksCorrectly()
    {
        // Test segments with different positions
        StringSegment fullSegment = new("Hello World");
        StringSegment startSegment = new("Hello World", 0, 5);   // "Hello"
        StringSegment middleSegment = new("Hello World", 3, 5);  // "lo Wo"
        StringSegment endSegment = new("Hello World", 6, 5);     // "World"
        StringSegment empty = new();

        // Full segment comparison
        fullSegment.CompareTo("Hello World").ShouldBe(
            string.Compare("Hello World", "Hello World", StringComparison.Ordinal));
        fullSegment.CompareTo("Hello").ShouldBe(
            string.Compare("Hello World", "Hello", StringComparison.Ordinal));
        fullSegment.CompareTo("Zebra").ShouldBe(
            string.Compare("Hello World", "Zebra", StringComparison.Ordinal));

        // Start segment comparison
        startSegment.CompareTo("Hello").ShouldBe(
            string.Compare("Hello", "Hello", StringComparison.Ordinal));
        startSegment.CompareTo("Help").ShouldBe(
            string.Compare("Hello", "Help", StringComparison.Ordinal));
        startSegment.CompareTo("Hel").ShouldBe(
            string.Compare("Hello", "Hel", StringComparison.Ordinal));

        // Middle segment comparison
        middleSegment.CompareTo("lo Wo").ShouldBe(
            string.Compare("lo Wo", "lo Wo", StringComparison.Ordinal));

        middleSegment.CompareTo("lo").ShouldBe(
            string.Compare("lo Wo", "lo", StringComparison.Ordinal));
        middleSegment.CompareTo("lo Wp").ShouldBe(
            string.Compare("lo Wo", "lo Wp", StringComparison.Ordinal));

        // End segment comparison
        endSegment.CompareTo("World").ShouldBe(
            string.Compare("World", "World", StringComparison.Ordinal));
        endSegment.CompareTo("Worle").ShouldBe(
            string.Compare("World", "Worle", StringComparison.Ordinal));
        endSegment.CompareTo("Worl").ShouldBe(
            string.Compare("World", "Worl", StringComparison.Ordinal));

        // Empty segment
        empty.CompareTo("").ShouldBe(
            string.Compare("", "", StringComparison.Ordinal));
        empty.CompareTo("Hello").ShouldBe(
            string.Compare("", "Hello", StringComparison.Ordinal));
    }


    [Fact]
    public void CompareTo_String_WithComparison_WorksCorrectly()
    {
        StringSegment segment = new("Hello World", 0, 5);  // "Hello"

        // Ordinal
        segment.CompareTo("hello", StringComparison.Ordinal).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.Ordinal));
        segment.CompareTo("Hello", StringComparison.Ordinal).ShouldBe(
            string.Compare("Hello", "Hello", StringComparison.Ordinal));

        // OrdinalIgnoreCase
        segment.CompareTo("hello", StringComparison.OrdinalIgnoreCase).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.OrdinalIgnoreCase));
        segment.CompareTo("help", StringComparison.OrdinalIgnoreCase).ShouldBe(
            string.Compare("Hello", "help", StringComparison.OrdinalIgnoreCase));

        // CurrentCulture
        int expected = string.Compare("Hello", "hello", StringComparison.CurrentCulture);
        segment.CompareTo("hello", StringComparison.CurrentCulture).ShouldBe(expected);
        segment.CompareTo("Hello", StringComparison.CurrentCulture).ShouldBe(
            string.Compare("Hello", "Hello", StringComparison.CurrentCulture));

        // CurrentCultureIgnoreCase
        segment.CompareTo("hello", StringComparison.CurrentCultureIgnoreCase).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.CurrentCultureIgnoreCase));

        // InvariantCulture
        segment.CompareTo("hello", StringComparison.InvariantCulture).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.InvariantCulture));
        segment.CompareTo("Hello", StringComparison.InvariantCulture).ShouldBe(
            string.Compare("Hello", "Hello", StringComparison.InvariantCulture));

        // InvariantCultureIgnoreCase
        segment.CompareTo("hello", StringComparison.InvariantCultureIgnoreCase).ShouldBe(
            string.Compare("Hello", "hello", StringComparison.InvariantCultureIgnoreCase));

        // Different lengths
        segment.CompareTo("Hell", StringComparison.Ordinal).ShouldBe(
            string.Compare("Hello", "Hell", StringComparison.Ordinal));
        segment.CompareTo("Helloz", StringComparison.Ordinal).ShouldBe(
            string.Compare("Hello", "Helloz", StringComparison.Ordinal));
    }

    [Fact]
    public void ComparisonOperators_WithStringSegments_WorkCorrectly()
    {
        StringSegment segment1 = new("Hello");
        StringSegment segment2 = new("World");
        StringSegment segment3 = new("Hello");
        StringSegment empty = new();

        // Equality operators
        (segment1 == segment3).ShouldBeTrue();
        (segment1 != segment2).ShouldBeTrue();

        // Less than
        (segment1 < segment2).ShouldBeTrue();
        (segment2 < segment1).ShouldBeFalse();

        // Less than or equal
        (segment1 <= segment3).ShouldBeTrue();
        (segment1 <= segment2).ShouldBeTrue();
        (segment2 <= segment1).ShouldBeFalse();

        // Greater than
        (segment2 > segment1).ShouldBeTrue();
        (segment1 > segment2).ShouldBeFalse();

        // Greater than or equal
        (segment1 >= segment3).ShouldBeTrue();
        (segment2 >= segment1).ShouldBeTrue();
        (segment1 >= segment2).ShouldBeFalse();

        // Empty comparisons
        (empty < segment1).ShouldBeTrue();
        (segment1 > empty).ShouldBeTrue();
#pragma warning disable CS1718 // Comparison made to same variable
        (empty == empty).ShouldBeTrue();
#pragma warning restore CS1718 // Comparison made to same variable

        // Verify against string comparisons
        (segment1 < segment2).ShouldBe(string.Compare("Hello", "World", StringComparison.Ordinal) < 0);
        (segment2 > segment1).ShouldBe(string.Compare("World", "Hello", StringComparison.Ordinal) > 0);
    }

    [Fact]
    public void ComparisonOperators_WithStrings_WorkCorrectly()
    {
        StringSegment segment = new("Hello");

        // Less than
        (segment < "World").ShouldBeTrue();
        (segment < "Hello").ShouldBeFalse();

        // Less than or equal
        (segment <= "Hello").ShouldBeTrue();
        (segment <= "World").ShouldBeTrue();
        (segment <= "Hel").ShouldBeFalse();

        // Greater than
        (segment > "Hel").ShouldBeTrue();
        (segment > "Hello").ShouldBeFalse();

        // Greater than or equal
        (segment >= "Hello").ShouldBeTrue();
        (segment >= "Hel").ShouldBeTrue();
        (segment >= "World").ShouldBeFalse();

        // Verify against string comparisons
        (segment < "World").ShouldBe(string.Compare("Hello", "World", StringComparison.Ordinal) < 0);
        (segment > "Hel").ShouldBe(string.Compare("Hello", "Hel", StringComparison.Ordinal) > 0);
    }

    [Fact]
    public void CompareTo_WithDifferentPositions_MatchesStringCompare()
    {
        string original = "This is a test string for comparison tests";

        // Test segments at different positions
        TestSegmentCompare(original, 0, 4);     // "This"
        TestSegmentCompare(original, 5, 2);     // "is"
        TestSegmentCompare(original, 10, 4);    // "test"
        TestSegmentCompare(original, 22, 9);    // "for compa"
        TestSegmentCompare(original, 32, 5);    // "rison"

        // Local helper function
        static void TestSegmentCompare(string source, int start, int length)
        {
            StringSegment segment = new(source, start, length);
            string substring = source.Substring(start, length);

            // Compare against multiple targets
            string[] targets =
            [
                "a",
                "z",
                substring,
                substring.ToLowerInvariant(),
                substring.ToUpperInvariant(),
                substring + "x"
            ];

            foreach (string target in targets)
            {
                // Test ordinal comparison
                segment.CompareTo(target).ShouldBe(
                    string.Compare(substring, target, StringComparison.Ordinal),
                    $"Segment '{segment}' compared to '{target}' should match string comparison");

                // Test comparison with explicit comparison type
                segment.CompareTo(target, StringComparison.OrdinalIgnoreCase).ShouldBe(
                    string.Compare(substring, target, StringComparison.OrdinalIgnoreCase),
                    $"Segment '{segment}' compared to '{target}' with OrdinalIgnoreCase should match string comparison");
            }
        }
    }

    [Fact]
    public void CompareTo_WithSpecialCases_WorksCorrectly()
    {
        // Unicode characters
        StringSegment unicodeSegment = new("こんにちは世界"); // "Hello World" in Japanese
        StringSegment partialUnicode = new("こんにちは世界", 0, 5); // "こんにちは" (Hello)

        unicodeSegment.CompareTo("こんにちは世界").ShouldBe(
            string.Compare("こんにちは世界", "こんにちは世界", StringComparison.Ordinal),
            "Full Unicode segment comparison should match string.Compare");

        partialUnicode.CompareTo("こんにちは").ShouldBe(
            string.Compare("こんにちは", "こんにちは", StringComparison.Ordinal),
            "Partial Unicode segment comparison should match string.Compare");

        unicodeSegment.CompareTo("こんにちは").ShouldBe(
            string.Compare("こんにちは世界", "こんにちは", StringComparison.Ordinal),
            "Unicode segment compared to shorter string should match string.Compare");

        // Same prefix but different lengths
        StringSegment abcSegment = new("abcdef", 0, 3); // "abc"

        abcSegment.CompareTo("abc").ShouldBe(
            string.Compare("abc", "abc", StringComparison.Ordinal),
            "Equal length comparison should match string.Compare");

        abcSegment.CompareTo("ab").ShouldBe(
            string.Compare("abc", "ab", StringComparison.Ordinal),
            "Comparison with shorter string should match string.Compare");

        abcSegment.CompareTo("abcd").ShouldBe(
            string.Compare("abc", "abcd", StringComparison.Ordinal),
            "Comparison with longer string should match string.Compare");

        // Empty segment
        StringSegment empty = new();

        empty.CompareTo("").ShouldBe(
            string.Compare(string.Empty, "", StringComparison.Ordinal),
            "Empty segment compared to empty string should match string.Compare");

        empty.CompareTo("a").ShouldBe(
            string.Compare(string.Empty, "a", StringComparison.Ordinal),
            "Empty segment compared to non-empty string should match string.Compare");

        empty.CompareTo(empty).ShouldBe(
            string.Compare(string.Empty, string.Empty, StringComparison.Ordinal),
            "Empty segment compared to itself should match string.Compare");
    }

    [Fact]
    public void CompareTo_StringComparison_InvalidValue_ThrowsArgumentOutOfRangeException()
    {
        StringSegment segment = new("test");
        string other = "test";

        // Cast to an invalid StringComparison value
        StringComparison invalidComparison = (StringComparison)99;

        // Should throw for invalid comparison value
        Should.Throw<InternalErrorException>(() => segment.CompareTo(other, invalidComparison)).Message.ShouldContain("Unsupported comparison type");
    }

    [Fact]
    public void CompareTo_WithMixedAsciiAndNonAscii_WorksCorrectly()
    {
        // Strings with ASCII and non-ASCII
        StringSegment ascii = new("abcdef");
        StringSegment nonAscii = new("abcд");  // Cyrillic д (U+0434)
        StringSegment mixed = new("abc\u00E9f"); // é (U+00E9)
        StringSegment mixedSame = new("abc\u00E9f"); // same as above

        // Compare ASCII vs mixed
        ascii.CompareTo(mixed).ShouldBe(
            string.Compare("abcdef", "abc\u00E9f", StringComparison.Ordinal),
            "ASCII and mixed comparison should match string.Compare");

        // Compare mixed vs same mixed
        mixed.CompareTo(mixedSame).ShouldBe(0, "Identical mixed strings should compare as equal");

        // Compare mixed vs non-ASCII
        mixed.CompareTo(nonAscii).ShouldBe(
            string.Compare("abc\u00E9f", "abcд", StringComparison.Ordinal),
            "Mixed and non-ASCII comparison should match string.Compare");

        // Comparison with strings differing in ASCII portion
        StringSegment mixedAsciiDiff = new("abd\u00E9f");
        mixed.CompareTo(mixedAsciiDiff).ShouldBe(
            string.Compare("abc\u00E9f", "abd\u00E9f", StringComparison.Ordinal),
            "Strings differing in ASCII portion should compare correctly");

        // Comparison with strings differing in non-ASCII portion
        StringSegment mixedNonAsciiDiff = new("abc\u00EAf"); // ê (U+00EA) instead of é
        mixed.CompareTo(mixedNonAsciiDiff).ShouldBe(
            string.Compare("abc\u00E9f", "abc\u00EAf", StringComparison.Ordinal),
            "Strings differing in non-ASCII portion should compare correctly");
    }

    [Fact]
    public void CompareTo_AsciiToNonAsciiTransition_WorksCorrectly()
    {
        // Test strings that differ at the transition from ASCII to non-ASCII
        StringSegment ascii1 = new("abcde");
        StringSegment ascii2 = new("abcdf");
        StringSegment transitionA = new("abcd\u00E9"); // é (U+00E9)
        StringSegment transitionB = new("abcd\u00EA"); // ê (U+00EA)

        // ASCII comparison before the transition point
        ascii1.CompareTo(ascii2).ShouldBe(
            string.Compare("abcde", "abcdf", StringComparison.Ordinal),
            "ASCII strings should compare correctly");

        // Comparison at the transition point (ASCII to non-ASCII)
        ascii1.CompareTo(transitionA).ShouldBe(
            string.Compare("abcde", "abcd\u00E9", StringComparison.Ordinal),
            "ASCII to non-ASCII transition should compare correctly");

        ascii2.CompareTo(transitionA).ShouldBe(
            string.Compare("abcdf", "abcd\u00E9", StringComparison.Ordinal),
            "ASCII to non-ASCII transition should compare correctly");

        // Comparison between different non-ASCII transitions
        transitionA.CompareTo(transitionB).ShouldBe(
            string.Compare("abcd\u00E9", "abcd\u00EA", StringComparison.Ordinal),
            "Different non-ASCII transitions should compare correctly");

        // Test with a partial segment
        StringSegment partialTransition = new("xxabcd\u00E9yy", 2, 5); // "abcd\u00E9"
        partialTransition.CompareTo(transitionA).ShouldBe(0, "Partial segment with transition should compare correctly");
    }

    [Fact]
    public void CompareTo_OrdinalIgnoreCase_WithMixedCharacters_WorksCorrectly()
    {
        // Case differences with ASCII and non-ASCII characters
        StringSegment lower = new("abcdef");
        StringSegment upper = new("ABCDEF");
        StringSegment mixedCase = new("aBcDeF");

        StringSegment lowerWithNonAscii = new("abcdé"); // é (U+00E9)
        StringSegment upperWithNonAscii = new("ABCDé"); // é (U+00E9)
        StringSegment upperNonAsciiCase = new("abcdÉ"); // É (U+00C9)

        // ASCII case-insensitive comparison
        lower.CompareTo(upper, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "ASCII case-insensitive comparison should work correctly");

        lower.CompareTo(mixedCase, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "Mixed case ASCII should compare as equal with case-insensitive comparison");

        // Non-ASCII case-insensitive comparison
        lowerWithNonAscii.CompareTo(upperWithNonAscii, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "Case-insensitive comparison with non-ASCII should work correctly for ASCII part");

        lowerWithNonAscii.CompareTo(upperNonAsciiCase, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "Case-insensitive comparison with non-ASCII should work correctly for non-ASCII part");

        // Mixed ASCII/non-ASCII with case differences
        StringSegment complexLower = new("abc\u00E9\u00F1xyz"); // abcéñxyz
        StringSegment complexUpper = new("ABC\u00C9\u00D1XYZ"); // ABCÉÑxyz

        complexLower.CompareTo(complexUpper, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "Complex mixed case comparison should work correctly");

        // Different strings that should not be equal even with case-insensitive comparison
        StringSegment differAtAscii = new("abd\u00E9\u00F1xyz"); // abdéñxyz
        complexLower.CompareTo(differAtAscii, StringComparison.OrdinalIgnoreCase).ShouldNotBe(0,
            "Different strings should not be equal with case-insensitive comparison");

        StringSegment differAtNonAscii = new("abc\u00EA\u00F1xyz"); // abcêñxyz
        complexLower.CompareTo(differAtNonAscii, StringComparison.OrdinalIgnoreCase).ShouldNotBe(0,
            "Strings differing at non-ASCII should not be equal with case-insensitive comparison");
    }

    [Fact]
    public void CompareTo_HalfAsciiOptimization_WorksCorrectly()
    {
        // Test the half-ASCII optimization in OrdinalIgnoreCase comparison
        // The implementation first compares ASCII characters with a fast path,
        // then falls back to culture-aware comparison for the rest

        // Strings that are identical in their ASCII part but differ after
        StringSegment asciiSameNonAsciiDiff1 = new("abc\u00E9xyz");  // abcéxyz
        StringSegment asciiSameNonAsciiDiff2 = new("abc\u00EAxyz");  // abcêxyz

        // Should compare correctly with ordinal
        asciiSameNonAsciiDiff1.CompareTo(asciiSameNonAsciiDiff2, StringComparison.Ordinal)
            .ShouldBe(
                string.Compare("abc\u00E9xyz", "abc\u00EAxyz", StringComparison.Ordinal),
                "Strings with same ASCII part should compare correctly with Ordinal");

        // Should compare correctly with ordinal ignore case
        asciiSameNonAsciiDiff1.CompareTo(asciiSameNonAsciiDiff2, StringComparison.OrdinalIgnoreCase)
            .ShouldBe(
                string.Compare("abc\u00E9xyz", "abc\u00EAxyz", StringComparison.OrdinalIgnoreCase),
                "Strings with same ASCII part should compare correctly with OrdinalIgnoreCase");

        // Test with case differences in ASCII part and differences in non-ASCII part
        StringSegment mixedCase1 = new("aBc\u00E9xyz");  // aBcéxyz
        StringSegment mixedCase2 = new("AbC\u00EAxyz");  // AbCêxyz

        mixedCase1.CompareTo(mixedCase2, StringComparison.OrdinalIgnoreCase)
            .ShouldBe(
                string.Compare("aBc\u00E9xyz", "AbC\u00EAxyz", StringComparison.OrdinalIgnoreCase),
                "Strings with case differences in ASCII and differences in non-ASCII should compare correctly");
    }

    [Fact]
    public void CompareTo_WithAsciiPrefixNonAsciiSuffix_OrdinalIgnoreCase()
    {
        // This tests the specific case where string comparison switches from the
        // ASCII-optimized path to the culture-aware path in OrdinalIgnoreCase

        // Create strings with identical ASCII prefix but different non-ASCII suffix
        StringSegment segment1 = new("hello\u00E9"); // helloé
        StringSegment segment2 = new("hello\u00EA"); // helloê
        StringSegment segment3 = new("HELLO\u00E9"); // HELLOé

        // Ordinal comparison should detect the difference
        segment1.CompareTo(segment2, StringComparison.Ordinal).ShouldBe(
            string.Compare("hello\u00E9", "hello\u00EA", StringComparison.Ordinal),
            "Ordinal comparison should detect difference in non-ASCII suffix");

        // OrdinalIgnoreCase should ignore case in ASCII part but detect difference in non-ASCII
        segment1.CompareTo(segment3, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "OrdinalIgnoreCase should consider different case ASCII with same non-ASCII as equal");

        segment1.CompareTo(segment2, StringComparison.OrdinalIgnoreCase).ShouldBe(
            string.Compare("hello\u00E9", "hello\u00EA", StringComparison.OrdinalIgnoreCase),
            "OrdinalIgnoreCase should detect difference in non-ASCII suffix");

        // Test with characters that need surrogate pairs (outside BMP)
        StringSegment surrogate1 = new("hello\U0001F600"); // hello😀 (GRINNING FACE)
        StringSegment surrogate2 = new("hello\U0001F601"); // hello😁 (GRINNING FACE WITH SMILING EYES)
        StringSegment surrogate3 = new("HELLO\U0001F600"); // HELLO😀

        surrogate1.CompareTo(surrogate2, StringComparison.Ordinal).ShouldBe(
            string.Compare("hello\U0001F600", "hello\U0001F601", StringComparison.Ordinal),
            "Ordinal comparison should detect difference in surrogate pairs");

        surrogate1.CompareTo(surrogate3, StringComparison.OrdinalIgnoreCase).ShouldBe(0, "OrdinalIgnoreCase should consider different case ASCII with same surrogate pairs as equal");
    }

    [Fact]
    public void ImplicitConversion_ToReadOnlyMemory_WorksCorrectly()
    {
        // Full segment
        string original = "Hello World";
        StringSegment segment = new(original);
        ReadOnlyMemory<char> memory = segment;

        memory.Span.ToString().ShouldBe(original);
        memory.Length.ShouldBe(original.Length);

        // Partial segment
        StringSegment partialSegment = new(original, 6, 5); // "World"
        ReadOnlyMemory<char> partialMemory = partialSegment;

        partialMemory.Span.ToString().ShouldBe("World");
        partialMemory.Length.ShouldBe(5);

        // Empty segment
        StringSegment emptySegment = new();
        ReadOnlyMemory<char> emptyMemory = emptySegment;

        emptyMemory.Length.ShouldBe(0);
        emptyMemory.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryFormat_WithSufficientBuffer_ReturnsTrue()
    {
        StringSegment segment = new("Hello World");
        Span<char> destination = new char[segment.Length];

        bool result = ((ISpanFormattable)segment).TryFormat(
            destination,
            out int charsWritten,
            [],
            null);

        result.ShouldBeTrue();
        charsWritten.ShouldBe(segment.Length);
        destination.ToString().ShouldBe("Hello World");
    }

    [Fact]
    public void TryFormat_WithInsufficientBuffer_ReturnsFalse()
    {
        StringSegment segment = new("Hello World");
        Span<char> destination = new char[segment.Length - 1]; // One character too small

        bool result = ((ISpanFormattable)segment).TryFormat(
            destination,
            out int charsWritten,
            [],
            null);

        result.ShouldBeFalse();
        charsWritten.ShouldBe(0);
    }

    [Fact]
    public void TryFormat_WithFormat_IgnoresFormat()
    {
        StringSegment segment = new("123");
        Span<char> destination = new char[segment.Length];

        // Format should be ignored, as specified in the implementation
        bool result = ((ISpanFormattable)segment).TryFormat(
            destination,
            out int charsWritten,
            "N2".AsSpan(), // Number format that would add commas if used
            CultureInfo.InvariantCulture);

        charsWritten.ShouldBe(segment.Length);
        result.ShouldBeTrue();
        destination.ToString().ShouldBe("123"); // Not "123.00"
    }

    [Fact]
    public void Equals_WithReadOnlySpan_IgnoresCase()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> lower = "hello".AsSpan();

        // StringSegment.Equals(ReadOnlySpan<char>) doesn't have a case-insensitive option
        // This test demonstrates that it's case-sensitive
        segment.Equals(lower).ShouldBeFalse();

        // For comparison, string.Equals() with OrdinalIgnoreCase would return true
        string.Equals(segment.ToString(), lower.ToString(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public unsafe void Pinning_WithDifferentSegmentCreationPaths_WorksConsistently()
    {
        // Test pinning with segments created via different paths
        string original = "Test String";

        // Direct constructor
        StringSegment segment1 = new(original);

        // Slice
        StringSegment segment2 = segment1[..original.Length];

        // Range indexer
        StringSegment segment3 = segment1[0..original.Length];

        // Implicit conversion
        StringSegment segment4 = original;

        fixed (char* p1 = segment1)
        fixed (char* p2 = segment2)
        fixed (char* p3 = segment3)
        fixed (char* p4 = segment4)
        fixed (char* po = original)
        {
            // All should point to the same memory location
            ((nint)p1).ShouldBe((nint)po);
            ((nint)p2).ShouldBe((nint)po);
            ((nint)p3).ShouldBe((nint)po);
            ((nint)p4).ShouldBe((nint)po);
        }
    }

    [Fact]
    public void IFormattable_ToString_ReturnsSameAsToString()
    {
        StringSegment segment = new("Hello World");
        IFormattable formattable = segment;

        // Should ignore format string and provider
        string result = formattable.ToString("N2", CultureInfo.InvariantCulture);

        result.ShouldBe(segment.ToString());
        result.ShouldBe("Hello World");
    }

    [Fact]
    public void CompareTo_WithNullString_ReturnsPositiveValue()
    {
        string hello = "Hello";
#pragma warning disable CA1310 // Specify StringComparison for correctness
        hello.CompareTo(null).ShouldBe(1);
#pragma warning restore CA1310

        // StringSegment with content should return positive value when compared to null
        StringSegment segment = new("Hello");
        segment.CompareTo(null).ShouldBe(1);

#pragma warning disable CA1310 // Specify StringComparison for correctness
        "".CompareTo(null).ShouldBe(1);
#pragma warning restore CA1310

        // Empty StringSegment should return 1 when compared to null (special case)
        StringSegment empty = new();
        empty.CompareTo(null).ShouldBe(1);
    }

    [Fact]
    public void ImplicitConversion_ToReadOnlyMemory_WithNullValue_ReturnsEmptyMemory()
    {
        StringSegment segment = new(null!);
        ReadOnlyMemory<char> memory = segment;

        memory.IsEmpty.ShouldBeTrue();
        memory.Length.ShouldBe(0);
    }

    // ---- Branch-coverage tests for the lower-coverage methods ----

#pragma warning disable IDE0057 // Use range operator - the whole point of these tests is to exercise the Slice methods directly.

    [Fact]
    public void Slice_DirectMethodCall_ReturnsCorrectSegment()
    {
        StringSegment segment = new("Hello World");
        StringSegment sliced = segment.Slice(6);
        sliced.ToString().ShouldBe("World");
    }

    [Fact]
    public void Slice_DirectMethodCall_StartZero_ReturnsSameContent()
    {
        StringSegment segment = new("Hello");
        StringSegment sliced = segment.Slice(0);
        sliced.ToString().ShouldBe("Hello");
    }

    [Fact]
    public void Slice_DirectMethodCall_StartEqualsLength_ReturnsEmpty()
    {
        StringSegment segment = new("Hello");
        StringSegment sliced = segment.Slice(5);
        sliced.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Slice_DirectMethodCall_StartOutOfRange_Throws()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => segment.Slice(6));
    }

    [Fact]
    public void Slice_DirectMethodCall_NegativeStart_Throws()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => segment.Slice(-1));
    }

    [Fact]
    public void Slice_DirectMethodCall_NullValue_ReturnsEmpty()
    {
        StringSegment segment = default;
        StringSegment sliced = segment.Slice(0);
        sliced.IsEmpty.ShouldBeTrue();
    }

#pragma warning restore IDE0057

    [Fact]
    public void ToString_OutSegment_PopulatesSegment()
    {
        StringSegment original = new("Hello World", 6, 5);
        string s = original.ToString(out StringSegment newSegment);
        s.ShouldBe("World");
        newSegment.ToString().ShouldBe("World");

        // Resulting segment should reference the new string and start at 0.
        newSegment.AsSpan().ToString().ShouldBe("World");
    }

    [Fact]
    public void Equals_ReadOnlySpan_DifferentLength_ReturnsFalse()
    {
        StringSegment segment = new("Hello");
        segment.Equals("Hi".AsSpan()).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ReadOnlySpan_SameContent_ReturnsTrue()
    {
        StringSegment segment = new("Hello");
        segment.Equals("Hello".AsSpan()).ShouldBeTrue();
    }

    [Fact]
    public void AsSpan_StartLength_ZeroLength_ReturnsEmpty()
    {
        StringSegment segment = new("Hello");
        ReadOnlySpan<char> span = segment.AsSpan(2, 0);
        span.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void AsSpan_StartLength_FullSegment_Works()
    {
        StringSegment segment = new("Hello");
        segment.AsSpan(0, 5).ToString().ShouldBe("Hello");
    }

    [Fact]
    public void AsSpan_Start_FromMiddle_Works()
    {
        StringSegment segment = new("Hello");
        segment.AsSpan(2).ToString().ShouldBe("llo");
    }

    [Fact]
    public void AsSpan_Start_End_ReturnsEmpty()
    {
        StringSegment segment = new("Hello");
        segment.AsSpan(5).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void IndexOfAny_TwoChars_FirstMatch()
    {
        StringSegment segment = new("Hello");
        segment.IndexOfAny('e', 'o').ShouldBe(1);
    }

    [Fact]
    public void IndexOfAny_TwoChars_NoMatch_ReturnsMinusOne()
    {
        StringSegment segment = new("Hello");
        segment.IndexOfAny('x', 'y').ShouldBe(-1);
    }

    [Fact]
    public void TrimStart_NoLeadingWhitespace_ReturnsSelf()
    {
        StringSegment segment = new("Hello");
        segment.TrimStart().ToString().ShouldBe("Hello");
    }

    [Fact]
    public void TrimStart_OnlyWhitespace_ReturnsEmpty()
    {
        StringSegment segment = new("   ");
        segment.TrimStart().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimStart_Char_Match_TrimsLeading()
    {
        StringSegment segment = new("xxxHello");
        segment.TrimStart('x').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void TrimStart_Char_NoMatch_ReturnsSelf()
    {
        StringSegment segment = new("Hello");
        segment.TrimStart('x').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void TrimEnd_Char_Match_TrimsTrailing()
    {
        StringSegment segment = new("Helloxxx");
        segment.TrimEnd('x').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void TrimEnd_Char_NoMatch_ReturnsSelf()
    {
        StringSegment segment = new("Hello");
        segment.TrimEnd('x').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void Trim_Char_BothEnds_Trims()
    {
        StringSegment segment = new("xxxHelloxxx");
        segment.Trim('x').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void Trim_TwoChars_BothEnds_Trims()
    {
        StringSegment segment = new("xy Helloxx");
        segment.Trim('x', 'y').ToString().ShouldBe(" Hello");
    }

    [Fact]
    public void TrimStart_TwoChars_TrimsLeading()
    {
        StringSegment segment = new("xyxHello");
        segment.TrimStart('x', 'y').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void TrimEnd_TwoChars_TrimsTrailing()
    {
        StringSegment segment = new("Helloxyx");
        segment.TrimEnd('x', 'y').ToString().ShouldBe("Hello");
    }

    [Fact]
    public void StartsWith_Span_OrdinalIgnoreCase_True()
    {
        StringSegment segment = new("Hello");
        segment.StartsWith("HE".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void StartsWith_Span_Ordinal_DifferentCase_False()
    {
        StringSegment segment = new("Hello");
        segment.StartsWith("HE".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void EndsWith_Span_OrdinalIgnoreCase_True()
    {
        StringSegment segment = new("Hello");
        segment.EndsWith("LO".AsSpan(), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_Span_Ordinal_DifferentCase_False()
    {
        StringSegment segment = new("Hello");
        segment.EndsWith("LO".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_StringSegment_DifferentCase_OrdinalIgnoreCase_True()
    {
        StringSegment segment = new("Hello World");
        segment.StartsWith(new StringSegment("HELLO"), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void EndsWith_StringSegment_OrdinalIgnoreCase_True()
    {
        StringSegment segment = new("Hello World");
        segment.EndsWith(new StringSegment("WORLD"), StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_StartIndexLength_ZeroLength_OnEmptyString_Succeeds()
    {
        StringSegment segment = new(string.Empty, 0, 0);
        segment.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_StartIndexLength_LengthExceedsBounds_Throws()
        => Should.Throw<ArgumentOutOfRangeException>(() => new StringSegment("Hello", 2, 10));

    [Fact]
    public void CompareTo_StringSegment_Ordinal_LessThan()
    {
        StringSegment a = new("apple");
        StringSegment b = new("banana");
        a.CompareTo(b, StringComparison.Ordinal).ShouldBeLessThan(0);
    }

    [Fact]
    public void CompareTo_StringSegment_OrdinalIgnoreCase_Equal()
    {
        StringSegment a = new("Apple");
        StringSegment b = new("APPLE");
        a.CompareTo(b, StringComparison.OrdinalIgnoreCase).ShouldBe(0);
    }

    [Fact]
    public void CompareTo_StringSegment_InvalidComparison_Throws()
    {
        StringSegment a = new("a");
        StringSegment b = new("b");
        Should.Throw<InternalErrorException>(() => a.CompareTo(b, (StringComparison)42));
    }

    [Fact]
    public void AsSpan_Start_OutOfRange_Throws()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => _ = segment.AsSpan(6));
    }

    [Fact]
    public void AsSpan_StartLength_OutOfRange_Throws()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => _ = segment.AsSpan(0, 6));
    }

    [Fact]
    public void AsSpan_StartLength_NegativeStart_Throws()
    {
        StringSegment segment = new("Hello");
        Should.Throw<InternalErrorException>(() => _ = segment.AsSpan(-1, 1));
    }

    [Fact]
    public void IndexOfAny_TwoChars_NotFound_ReturnsMinusOne()
    {
        StringSegment segment = new("aaaa");
        segment.IndexOfAny('x', 'y').ShouldBe(-1);
    }

    [Fact]
    public void EndsWith_Span_LongerThanSegment_False()
    {
        StringSegment segment = new("ab");
        segment.EndsWith("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void StartsWith_Span_LongerThanSegment_False()
    {
        StringSegment segment = new("ab");
        segment.StartsWith("hello".AsSpan(), StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void TrimStart_Default_OnlyWhitespace()
    {
        StringSegment segment = new("  \t\n  ");
        segment.TrimStart().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimEnd_Default_OnlyWhitespace()
    {
        StringSegment segment = new("  \t\n  ");
        segment.TrimEnd().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TrimEnd_Default_NoTrailingWhitespace_ReturnsSelf()
    {
        StringSegment segment = new("Hello");
        segment.TrimEnd().ToString().ShouldBe("Hello");
    }
}
