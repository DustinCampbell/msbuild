// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Build.Text;
using Xunit;

namespace Microsoft.Build.UnitTests.Text;

public class StringSegmentComparerTests
{
    [Fact]
    public void Ordinal_Compare_Equal_Segments_Returns_Zero()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.Ordinal.Compare(first, second).Should().Be(0);
    }

    [Fact]
    public void Ordinal_Compare_Different_Segments_Returns_NonZero()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("best", 0, 4);

        StringSegmentComparer.Ordinal.Compare(first, second).Should().NotBe(0);
    }

    [Fact]
    public void Ordinal_Compare_Is_CaseSensitive()
    {
        StringSegment first = new("Test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.Ordinal.Compare(first, second).Should().NotBe(0);
    }

    [Fact]
    public void Ordinal_Equals_Same_Segments_Returns_True()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.Ordinal.Equals(first, second).Should().BeTrue();
    }

    [Fact]
    public void Ordinal_Equals_Different_Segments_Returns_False()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("best", 0, 4);

        StringSegmentComparer.Ordinal.Equals(first, second).Should().BeFalse();
    }

    [Fact]
    public void Ordinal_Equals_Is_CaseSensitive()
    {
        StringSegment first = new("Test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.Ordinal.Equals(first, second).Should().BeFalse();
    }

    [Fact]
    public void Ordinal_GetHashCode_Same_Segments_Same_Hash()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.Ordinal.GetHashCode(first).Should().Be(
            StringSegmentComparer.Ordinal.GetHashCode(second));
    }

    [Fact]
    public void OrdinalIgnoreCase_Compare_Equal_Segments_Returns_Zero()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.Compare(first, second).Should().Be(0);
    }

    [Fact]
    public void OrdinalIgnoreCase_Compare_Equal_Segments_DifferentCase_Returns_Zero()
    {
        StringSegment first = new("Test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.Compare(first, second).Should().Be(0);
    }

    [Fact]
    public void OrdinalIgnoreCase_Compare_Different_Segments_Returns_NonZero()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("best", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.Compare(first, second).Should().NotBe(0);
    }

    [Fact]
    public void OrdinalIgnoreCase_Equals_Same_Segments_Returns_True()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.Equals(first, second).Should().BeTrue();
    }

    [Fact]
    public void OrdinalIgnoreCase_Equals_Same_Segments_DifferentCase_Returns_True()
    {
        StringSegment first = new("Test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.Equals(first, second).Should().BeTrue();
    }

    [Fact]
    public void OrdinalIgnoreCase_Equals_Different_Segments_Returns_False()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("best", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.Equals(first, second).Should().BeFalse();
    }

    [Fact]
    public void OrdinalIgnoreCase_GetHashCode_Same_Segments_Same_Hash()
    {
        StringSegment first = new("test", 0, 4);
        StringSegment second = new("test", 0, 4);

        StringSegmentComparer.OrdinalIgnoreCase.GetHashCode(first).Should().Be(
            StringSegmentComparer.OrdinalIgnoreCase.GetHashCode(second));
    }
}
