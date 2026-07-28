// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;
using Shouldly;

namespace Microsoft.Build.UnitTests;

/// <summary>
///  Shouldly-style assertions for <see cref="StringSegment"/> so tests can assert directly on a segment
///  without materializing it via <see cref="StringSegment.ToString"/> or inspecting
///  <see cref="StringSegment.HasValue"/>.
/// </summary>
internal static class StringSegmentShouldExtensions
{
    /// <summary>
    ///  Asserts that the segment's text equals <paramref name="expected"/>. A null segment (one whose
    ///  <see cref="StringSegment.HasValue"/> is <see langword="false"/>) compares equal only to
    ///  <see langword="null"/>.
    /// </summary>
    public static void ShouldBe(this StringSegment actual, string? expected)
        => actual.Value.ShouldBe(expected);

    /// <summary>
    ///  Asserts that the segment is a null segment (its <see cref="StringSegment.HasValue"/> is
    ///  <see langword="false"/>).
    /// </summary>
    public static void ShouldBeNull(this StringSegment actual)
        => actual.HasValue.ShouldBeFalse($"Expected a null StringSegment but was \"{actual}\".");
}
