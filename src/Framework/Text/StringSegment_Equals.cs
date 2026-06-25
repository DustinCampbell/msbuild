// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NET
using System.Globalization;
#endif

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Indicates whether this segment is equal to the specified object using an ordinal comparison.
    /// </summary>
    /// <param name="obj">The object to compare with this segment.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="obj"/> is a <see cref="StringSegment"/> equal to this
    ///  segment; otherwise, <see langword="false"/>. A boxed <see cref="string"/> is never considered equal.
    /// </returns>
    public override bool Equals(object? obj)
        => obj is StringSegment segment && Equals(segment);

    /// <summary>
    ///  Indicates whether this segment is equal to <paramref name="other"/> using an ordinal comparison.
    /// </summary>
    /// <param name="other">The segment to compare with this segment.</param>
    /// <returns>
    ///  <see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(StringSegment other)
        => Equals(other, StringComparison.Ordinal);

    /// <summary>
    ///  Indicates whether this segment is equal to <paramref name="other"/> using the specified comparison.
    ///  Two null segments are considered equal, and a null segment is never equal to a non-null segment.
    /// </summary>
    /// <param name="other">The segment to compare with this segment.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(StringSegment other, StringComparison comparisonType)
    {
        if (HasValue != other.HasValue)
        {
            return false;
        }

        if (!HasValue)
        {
            // Both segments are null.
            return true;
        }

        if (ReferenceEquals(Buffer, other.Buffer) && Offset == other.Offset && Length == other.Length)
        {
            // Both segments view the same range of the same buffer, so they are equal under any comparison.
            return true;
        }

#if NET
        return AsSpan().Equals(other.AsSpan(), comparisonType);
#else
        return EqualsCore(Buffer, Offset, Length, other.Buffer!, other.Offset, other.Length, comparisonType);
#endif
    }

    /// <summary>
    ///  Indicates whether this segment is equal to <paramref name="other"/> using an ordinal comparison. A
    ///  null segment is equal to a <see langword="null"/> string, and an empty segment is equal to the empty
    ///  string.
    /// </summary>
    /// <param name="other">The string to compare with this segment.</param>
    /// <returns>
    ///  <see langword="true"/> if this segment and <paramref name="other"/> are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? other)
        => Equals(other, StringComparison.Ordinal);

    /// <summary>
    ///  Indicates whether this segment is equal to <paramref name="other"/> using the specified comparison. A
    ///  null segment is equal to a <see langword="null"/> string, and an empty segment is equal to the empty
    ///  string.
    /// </summary>
    /// <param name="other">The string to compare with this segment.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if this segment and <paramref name="other"/> are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? other, StringComparison comparisonType)
    {
        if (HasValue != (other != null))
        {
            return false;
        }

        if (!HasValue)
        {
            // Both this segment and the other string are null.
            return true;
        }

        if (ReferenceEquals(Buffer, other) && Offset == 0 && Length == other.Length)
        {
            // This segment covers the entirety of the same string instance, so the two are equal under any comparison.
            return true;
        }

#if NET
        return AsSpan().Equals(other.AsSpan(), comparisonType);
#else
        return EqualsCore(Buffer, Offset, Length, other!, 0, other!.Length, comparisonType);
#endif
    }

    /// <summary>
    ///  Indicates whether two <see cref="StringSegment"/> instances are equal using an ordinal comparison.
    ///  Two null segments are considered equal, and a null segment is never equal to a non-null segment.
    /// </summary>
    /// <param name="a">The first segment to compare.</param>
    /// <param name="b">The second segment to compare.</param>
    /// <returns>
    ///  <see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Equals(StringSegment a, StringSegment b)
        => a.Equals(b, StringComparison.Ordinal);

    /// <summary>
    ///  Indicates whether two <see cref="StringSegment"/> instances are equal using the specified comparison.
    ///  Two null segments are considered equal, and a null segment is never equal to a non-null segment.
    /// </summary>
    /// <param name="a">The first segment to compare.</param>
    /// <param name="b">The second segment to compare.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool Equals(StringSegment a, StringSegment b, StringComparison comparisonType)
        => a.Equals(b, comparisonType);

#if !NET
    /// <summary>
    ///  Determines whether two string regions are equal using the given <see cref="StringComparison"/>.
    /// </summary>
    /// <remarks>
    ///  The ordinal cases use a direct character comparison with an early-out rather than
    ///  <see cref="CompareCore"/>. On .NET Framework <see cref="CompareCore"/> routes ordinal comparisons
    ///  through <see cref="CompareInfo.Compare(string, int, int, string, int, int, CompareOptions)"/>, which
    ///  is a globalization call roughly 2-3x slower than an equality check and computes an ordering the
    ///  caller does not need. Culture-sensitive comparisons still defer to <see cref="CompareCore"/> because
    ///  regions of differing length can be linguistically equal.
    /// </remarks>
    private static bool EqualsCore(string buffer1, int offset1, int length1, string buffer2, int offset2, int length2, StringComparison comparisonType)
        => comparisonType switch
        {
            StringComparison.Ordinal => EqualsOrdinal(buffer1, offset1, length1, buffer2, offset2, length2),
            StringComparison.OrdinalIgnoreCase => EqualsOrdinalIgnoreCase(buffer1, offset1, length1, buffer2, offset2, length2),
            _ => CompareCore(buffer1, offset1, length1, buffer2, offset2, length2, comparisonType) == 0,
        };

    /// <summary>
    ///  Determines whether two string regions are equal using ordinal comparison.
    /// </summary>
    private static bool EqualsOrdinal(string buffer1, int offset1, int length1, string buffer2, int offset2, int length2)
    {
        // Regions of different lengths can never be ordinally equal.
        if (length1 != length2)
        {
            return false;
        }

        for (int i = 0; i < length1; i++)
        {
            if (buffer1[offset1 + i] != buffer2[offset2 + i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Determines whether two string regions are equal using ordinal ignore case comparison.
    /// </summary>
    /// <param name="buffer1"></param>
    /// <param name="offset1"></param>
    /// <param name="length1"></param>
    /// <param name="buffer2"></param>
    /// <param name="offset2"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    private static bool EqualsOrdinalIgnoreCase(string buffer1, int offset1, int length1, string buffer2, int offset2, int length2)
    {
        // Regions of different lengths can never be ordinally equal.
        if (length1 != length2)
        {
            return false;
        }

        TextInfo textInfo = s_invariantTextInfo;

        for (int i = 0; i < length1; i++)
        {
            char c1 = buffer1[offset1 + i];
            char c2 = buffer2[offset2 + i];
            if (c1 != c2 && textInfo.ToUpper(c1) != textInfo.ToUpper(c2))
            {
                return false;
            }
        }

        return true;
    }
#endif
}
