// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
