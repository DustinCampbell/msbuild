// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Text;

/// <summary>
///  Compares <see cref="StringSegment"/> values for equality and order using a fixed
///  <see cref="StringComparison"/>. Only <see cref="StringComparison.Ordinal"/> and
///  <see cref="StringComparison.OrdinalIgnoreCase"/> are supported.
/// </summary>
internal sealed class StringSegmentComparer : IEqualityComparer<StringSegment>, IComparer<StringSegment>
{
    /// <summary>
    ///  Gets a comparer that performs a case-sensitive ordinal comparison.
    /// </summary>
    public static StringSegmentComparer Ordinal { get; } = new(StringComparison.Ordinal);

    /// <summary>
    ///  Gets a comparer that performs a case-insensitive ordinal comparison.
    /// </summary>
    public static StringSegmentComparer OrdinalIgnoreCase { get; } = new(StringComparison.OrdinalIgnoreCase);

    private readonly StringComparison _comparisonType;

    private StringSegmentComparer(StringComparison comparisonType)
        => _comparisonType = comparisonType;

    /// <summary>
    ///  Returns the comparer corresponding to the specified <see cref="StringComparison"/>.
    /// </summary>
    /// <param name="comparisonType">
    ///  The comparison to use. Must be <see cref="StringComparison.Ordinal"/> or
    ///  <see cref="StringComparison.OrdinalIgnoreCase"/>.
    /// </param>
    /// <returns>The matching comparer instance.</returns>
    public static StringSegmentComparer FromComparison(StringComparison comparisonType)
        => comparisonType switch
        {
            StringComparison.Ordinal => Ordinal,
            StringComparison.OrdinalIgnoreCase => OrdinalIgnoreCase,
            _ => throw new ArgumentOutOfRangeException(nameof(comparisonType)),
        };

    /// <summary>
    ///  Determines whether two <see cref="StringSegment"/> values are equal using this comparer's comparison.
    /// </summary>
    /// <param name="x">The first segment to compare.</param>
    /// <param name="y">The second segment to compare.</param>
    /// <returns>
    ///  <see langword="true"/> if the two segments are equal under this comparer's comparison; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool Equals(StringSegment x, StringSegment y)
        => x.Equals(y, _comparisonType);

    /// <summary>
    ///  Compares two <see cref="StringSegment"/> values and indicates their relative order using this
    ///  comparer's comparison.
    /// </summary>
    /// <param name="x">The first segment to compare.</param>
    /// <param name="y">The second segment to compare.</param>
    /// <returns>
    ///  A negative value if <paramref name="x"/> precedes <paramref name="y"/>, zero if they are equal, or a
    ///  positive value if <paramref name="x"/> follows <paramref name="y"/>.
    /// </returns>
    public int Compare(StringSegment x, StringSegment y)
        => StringSegment.Compare(x, y, _comparisonType);

    /// <summary>
    ///  Returns a hash code for the specified <see cref="StringSegment"/> that is consistent with this
    ///  comparer's comparison, so segments this comparer treats as equal hash identically.
    /// </summary>
    /// <param name="obj">The segment for which to compute a hash code.</param>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public int GetHashCode(StringSegment obj)
        => obj.GetHashCode(_comparisonType);
}
