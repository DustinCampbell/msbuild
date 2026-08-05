// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Collections;

public class MSBuildNameIgnoreCaseComparer_Tests
{
    private static readonly MSBuildNameIgnoreCaseComparer s_comparer = MSBuildNameIgnoreCaseComparer.Default;

    [Theory]
    [InlineData("FOO", "foo", true)]
    [InlineData("FOOA", "FOOB", false)]
    [InlineData("AFOO", "BFOO", false)]
    [InlineData("FOO", "FOOB", false)]
    [InlineData("FOOB", "FOO", false)]
    [InlineData("a", "b", false)]
    [InlineData("", "", true)]
    [InlineData("x", null, false)]
    [InlineData(null, "x", false)]
    [InlineData(null, null, true)]
    public void StringEqualityIsOrdinalIgnoreCase(string? left, string? right, bool expected)
        => s_comparer.Equals(left, right).ShouldBe(expected);

    [Theory]
    [InlineData("foo", "xxFOOyy", 2, 3, true)]
    [InlineData("foo", "xxBARyy", 2, 3, false)]
    [InlineData("foo", "xxFOyy", 2, 2, false)]
    public void StringAndSegmentEqualityIsOrdinalIgnoreCase(
        string value,
        string buffer,
        int offset,
        int length,
        bool expected)
        => s_comparer.Equals(value, new StringSegment(buffer, offset, length)).ShouldBe(expected);

    [Theory]
    [InlineData("xFOOfooy", 1, 3, "xFOOfooy", 4, 3, true)]
    [InlineData("xFOOy", 1, 3, "xxBAR", 2, 3, false)]
    [InlineData("xFOOy", 1, 3, "xFOO", 1, 2, false)]
    public void SegmentEqualityIsOrdinalIgnoreCase(
        string leftBuffer,
        int leftOffset,
        int leftLength,
        string rightBuffer,
        int rightOffset,
        int rightLength,
        bool expected)
    {
        StringSegment left = new(leftBuffer, leftOffset, leftLength);
        StringSegment right = new(rightBuffer, rightOffset, rightLength);

        s_comparer.Equals(left, right).ShouldBe(expected);
    }

    [Fact]
    public void NullAndEmptySegmentsAreDistinct()
    {
        s_comparer.Equals(default(StringSegment), default).ShouldBeTrue();
        s_comparer.Equals(default(StringSegment), StringSegment.Empty).ShouldBeFalse();
        s_comparer.Equals(null, default(StringSegment)).ShouldBeTrue();
        s_comparer.Equals(string.Empty, StringSegment.Empty).ShouldBeTrue();
    }

    [Fact]
    public void EqualSegmentsHaveEqualHashCodes()
    {
        StringSegment lower = new("xxfooyy", 2, 3);
        StringSegment upper = new("zzFOOww", 2, 3);

        s_comparer.Equals(lower, upper).ShouldBeTrue();
        s_comparer.GetHashCode(lower).ShouldBe(s_comparer.GetHashCode(upper));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void ConstrainedEqualityRejectsInvalidBounds(int start, int length)
        => Should.Throw<InternalErrorException>(() => s_comparer.Equals("x", "y", start, length));

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 2)]
    public void ConstrainedHashCodeRejectsInvalidBounds(int start, int length)
        => Should.Throw<InternalErrorException>(() => s_comparer.GetHashCode("x", start, length));

    [Theory]
    [InlineData("bbb", "abbbaaa", 1, 3, true)]
    [InlineData("A", "babbbb", 1, 1, true)]
    [InlineData("b", "aabaa", 2, 1, true)]
    [InlineData("a", "ab", 0, 1, true)]
    [InlineData("aab", "aabaa", 0, 3, true)]
    [InlineData("bbc", "abbbaaa", 1, 3, false)]
    [InlineData("bb", "abbbaaa", 1, 3, false)]
    public void ConstrainedEqualityComparesRequestedRegion(
        string value,
        string buffer,
        int start,
        int length,
        bool expected)
        => s_comparer.Equals(value, buffer, start, length).ShouldBe(expected);

    [Fact]
    public void NullStringHashCodeIsZero()
        => s_comparer.GetHashCode(null!).ShouldBe(0);

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("abcde")]
    [InlineData("abcdef")]
    [InlineData("abcdefg")]
    [InlineData("abcdefgh")]
    public void HashCodeIsOrdinalIgnoreCase(string value)
        => s_comparer.GetHashCode(value).ShouldBe(s_comparer.GetHashCode(value.ToUpperInvariant()));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void RegionAndSegmentHashCodesMatchStandaloneString(int length)
    {
        const string value = "aBcDeFgH";
        const string buffer = "xxaBcDeFgHyy";

        int expected = s_comparer.GetHashCode(value.Substring(0, length));

        s_comparer.GetHashCode(buffer, 2, length).ShouldBe(expected);
        s_comparer.GetHashCode(new StringSegment(buffer, 2, length)).ShouldBe(expected);
    }
}
