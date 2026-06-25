// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Text;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Framework.UnitTests;

public class StringSegmentRange_Tests
{
    [Fact]
    public void DefaultRangeRepresentsEmptySegment()
    {
        const string buffer = "value";
        StringSegmentRange range = default;

        range.Offset.ShouldBe(0);
        range.Length.ShouldBe(0);
        range.IsNull.ShouldBeFalse();
        range.IsEmpty.ShouldBeTrue();

        StringSegment segment = range.ToSegment(buffer);
        segment.HasValue.ShouldBeTrue();
        segment.IsEmpty.ShouldBeTrue();
        segment.Buffer.ShouldBeSameAs(buffer);
        segment.Offset.ShouldBe(0);

        StringSegmentRange converted = StringSegment.Empty;
        converted.ShouldBe(range);
    }

    [Fact]
    public void NullRangeRepresentsNullSegment()
    {
        StringSegmentRange range = StringSegmentRange.Null;

        range.Offset.ShouldBe(-1);
        range.Length.ShouldBe(0);
        range.IsNull.ShouldBeTrue();
        range.IsEmpty.ShouldBeFalse();
        range.ShouldBe(new StringSegmentRange(-1, 0));
        range.ToSegment("value").HasValue.ShouldBeFalse();

        StringSegment nullSegment = default;
        StringSegmentRange converted = nullSegment;
        converted.ShouldBe(range);
    }

    [Fact]
    public void EmptyRangeCanHaveNonzeroOffset()
    {
        const string buffer = "prefixsuffix";
        StringSegmentRange range = new(6, 0);

        range.IsNull.ShouldBeFalse();
        range.IsEmpty.ShouldBeTrue();

        StringSegment segment = range.ToSegment(buffer);
        segment.HasValue.ShouldBeTrue();
        segment.IsEmpty.ShouldBeTrue();
        segment.Buffer.ShouldBeSameAs(buffer);
        segment.Offset.ShouldBe(6);

        StringSegmentRange converted = segment;
        converted.ShouldBe(range);
    }

    [Fact]
    public void StringSegmentRoundTripsThroughRange()
    {
        const string buffer = "prefix-value-suffix";
        StringSegment original = new(buffer, offset: 7, length: 5);

        StringSegmentRange range = original;
        StringSegment roundTripped = range.ToSegment(buffer);

        range.Offset.ShouldBe(original.Offset);
        range.Length.ShouldBe(original.Length);
        range.IsNull.ShouldBeFalse();
        range.IsEmpty.ShouldBeFalse();
        roundTripped.Buffer.ShouldBeSameAs(buffer);
        roundTripped.Offset.ShouldBe(original.Offset);
        roundTripped.Length.ShouldBe(original.Length);
        roundTripped.Value.ShouldBe(original.Value);
    }

    [Fact]
    public void RangeCanBeAppliedToDifferentBuffer()
    {
        StringSegment original = new("prefix-value", offset: 7, length: 5);
        StringSegmentRange range = original;
        const string buffer = "another-text!";

        StringSegment segment = range.ToSegment(buffer);

        segment.Buffer.ShouldBeSameAs(buffer);
        segment.Offset.ShouldBe(7);
        segment.Length.ShouldBe(5);
        segment.Value.ShouldBe("-text");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(7, 5)]
    public void NullBufferProducesNullSegment(int offset, int length)
    {
        StringSegmentRange range = new(offset, length);

        range.ToSegment(null).HasValue.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-2, 0)]
    [InlineData(-1, 1)]
    [InlineData(-1, -1)]
    [InlineData(0, -1)]
    [InlineData(int.MinValue, 0)]
    public void InvalidRangeThrows(int offset, int length)
    {
        Should.Throw<InternalErrorException>(() => new StringSegmentRange(offset, length));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(1, 2)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void RangeOutsideBufferThrows(int offset, int length)
    {
        StringSegmentRange range = new(offset, length);

        Should.Throw<InternalErrorException>(() => range.ToSegment("ab"));
    }
}
