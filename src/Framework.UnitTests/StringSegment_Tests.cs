// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
    public void AsSpan_Overloads_RejectRangesOutsideSegment()
    {
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsSpan(-1); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsSpan(HelloWorld.Length + 1); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsSpan(-1, 0); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsSpan(0, -1); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsSpan(HelloWorld.Length, 1); });
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
    public void AsMemory_Overloads_RejectRangesOutsideSegment()
    {
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsMemory(-1); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsMemory(HelloWorld.Length + 1); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsMemory(-1, 0); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsMemory(0, -1); });
        Should.Throw<InternalErrorException>(() => { _ = HelloWorld.AsMemory(HelloWorld.Length, 1); });
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
    public void Slice_Overloads_RejectRangesOutsideSegment()
    {
        Should.Throw<InternalErrorException>(() => HelloWorld.Slice(-1));
        Should.Throw<InternalErrorException>(() => HelloWorld.Slice(HelloWorld.Length + 1));
        Should.Throw<InternalErrorException>(() => HelloWorld.Slice(-1, 0));
        Should.Throw<InternalErrorException>(() => HelloWorld.Slice(0, -1));
        Should.Throw<InternalErrorException>(() => HelloWorld.Slice(HelloWorld.Length, 1));
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
