// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
#if !NET
using System.Globalization;
#endif
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Text;

/// <summary>
///  A lightweight, allocation-free view over a contiguous region of a <see cref="string"/>, represented as
///  a buffer together with a start offset and a length. Provides string-like search, comparison, trimming,
///  splitting, and joining operations without copying or allocating substrings, and is tuned to perform well
///  on both .NET and .NET Framework.
/// </summary>
/// <remarks>
///  A <see cref="StringSegment"/> distinguishes between a <em>null</em> segment (one with no underlying
///  buffer, equivalent to <see langword="default"/>) and an <em>empty</em> segment (one whose buffer is
///  non-null but whose length is zero). The implicit conversion from <see cref="string"/> preserves this
///  distinction: a <see langword="null"/> string yields a null segment and the empty string yields an empty
///  segment.
/// </remarks>
[DebuggerDisplay("{Value}")]
internal readonly partial struct StringSegment :
    IEquatable<StringSegment>,
    IEquatable<string?>,
    IComparable<StringSegment>,
    IComparable<string?>
{
    /// <summary>
    ///  A <see cref="StringSegment"/> that views the empty string. This is distinct from a null segment
    ///  (<see langword="default"/>), which has no underlying buffer.
    /// </summary>
    public static readonly StringSegment Empty = string.Empty;

    /// <summary>
    ///  Indicates whether the specified segment is a null segment or has a length of zero.
    /// </summary>
    /// <param name="segment">The segment to test.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="segment"/> has no underlying buffer or its length is zero;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public static bool IsNullOrEmpty(StringSegment segment)
        => !segment.HasValue || segment.IsEmpty;

    /// <summary>
    ///  Gets the underlying string that this segment is a view over, or <see langword="null"/> if this is a
    ///  null segment.
    /// </summary>
    public string? Buffer { get; }

    /// <summary>
    ///  Gets the index within <see cref="Buffer"/> at which this segment begins.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    ///  Gets the number of characters in this segment.
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///  Initializes a new <see cref="StringSegment"/> that views the entirety of <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The string to view, or <see langword="null"/> to create a null segment.</param>
    public StringSegment(string? buffer)
    {
        Buffer = buffer;
        Offset = 0;
        Length = buffer?.Length ?? 0;
    }

    /// <summary>
    ///  Initializes a new <see cref="StringSegment"/> that views the region of <paramref name="buffer"/>
    ///  beginning at <paramref name="offset"/> and spanning <paramref name="length"/> characters.
    /// </summary>
    /// <param name="buffer">The string to view.</param>
    /// <param name="offset">The index in <paramref name="buffer"/> at which the segment begins.</param>
    /// <param name="length">The number of characters in the segment.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StringSegment(string buffer, int offset, int length)
    {
        if (buffer is null || (uint)offset > (uint)buffer.Length || (uint)length > (uint)(buffer.Length - offset))
        {
            ValidateArguments(buffer, offset, length);
        }

        Buffer = buffer;
        Offset = offset;
        Length = length;

        static void ValidateArguments(string? buffer, int offset, int length)
        {
            Assumed.NotNull(buffer);
            Assumed.PositiveOrZero(offset);
            Assumed.PositiveOrZero(length);
            Assumed.LessThanOrEqual(offset, buffer.Length);
            Assumed.LessThanOrEqual(length, buffer.Length - offset);
        }
    }

    /// <summary>
    ///  Gets the segment's characters as a newly allocated <see cref="string"/>, or <see langword="null"/>
    ///  if this is a null segment.
    /// </summary>
    /// <remarks>
    ///  Accessing this property allocates a string; prefer <see cref="AsSpan()"/> on hot paths.
    /// </remarks>
    public string? Value => HasValue ? Buffer.Substring(Offset, Length) : null;

    /// <summary>
    ///  Gets a value indicating whether this segment has an underlying buffer; that is, whether it is not a
    ///  null segment.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Buffer))]
    [MemberNotNullWhen(true, nameof(Value))]
    public bool HasValue => Buffer != null;

    /// <summary>
    ///  Gets a value indicating whether this segment has an underlying buffer but a length of zero.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Buffer))]
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsEmpty => HasValue && Length == 0;

    /// <summary>
    ///  Gets the character at the specified index within this segment.
    /// </summary>
    /// <param name="index">
    ///  The zero-based index of the character to get, relative to the start of the segment.
    /// </param>
    /// <returns>
    ///  The character at <paramref name="index"/>.
    /// </returns>
    public char this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // A single unsigned comparison catches both negative and out-of-range indices.
            if ((uint)index >= (uint)Length)
            {
                ValidateIndex(index, Length);
            }

            // A valid (in-range) index implies Length > 0, so the segment has a non-null buffer.
            return Buffer![Offset + index];

            static void ValidateIndex(int index, int length)
            {
                Assumed.PositiveOrZero(index);
                Assumed.LessThan(index, length);
            }
        }
    }

    /// <summary>
    ///  Returns a read-only span over the characters of this segment.
    /// </summary>
    /// <returns>
    ///  A <see cref="ReadOnlySpan{T}"/> covering the segment's characters.
    /// </returns>
    public ReadOnlySpan<char> AsSpan()
        => Buffer.AsSpan(Offset, Length);

    /// <summary>
    ///  Returns a read-only span over the characters of this segment beginning at the specified index.
    /// </summary>
    /// <param name="start">
    ///  The zero-based index, relative to the start of the segment, at which the span begins.
    /// </param>
    /// <returns>
    ///  A <see cref="ReadOnlySpan{T}"/> covering the segment's characters from <paramref name="start"/> to the end.
    /// </returns>
    public ReadOnlySpan<char> AsSpan(int start)
    {
        Assumed.PositiveOrZero(start);
        Assumed.LessThanOrEqual(start, Length);

        return Buffer.AsSpan(Offset + start, Length - start);
    }

    /// <summary>
    ///  Returns a read-only span over the specified range of characters within this segment.
    /// </summary>
    /// <param name="start">
    ///  The zero-based index, relative to the start of the segment, at which the span begins.
    /// </param>
    /// <param name="length">The number of characters the span covers.</param>
    /// <returns>
    ///  A <see cref="ReadOnlySpan{T}"/> covering the requested range.
    /// </returns>
    public ReadOnlySpan<char> AsSpan(int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        return Buffer.AsSpan(Offset + start, length);
    }

    /// <summary>
    ///  Returns read-only memory over the characters of this segment.
    /// </summary>
    /// <returns>
    ///  A <see cref="ReadOnlyMemory{T}"/> covering the segment's characters.
    /// </returns>
    public ReadOnlyMemory<char> AsMemory()
        => Buffer.AsMemory(Offset, Length);

    /// <summary>
    ///  Returns read-only memory over the characters of this segment beginning at the specified index.
    /// </summary>
    /// <param name="start">
    ///  The zero-based index, relative to the start of the segment, at which the memory begins.
    /// </param>
    /// <returns>
    ///  A <see cref="ReadOnlyMemory{T}"/> covering the segment's characters from <paramref name="start"/> to the end.
    /// </returns>
    public ReadOnlyMemory<char> AsMemory(int start)
    {
        Assumed.PositiveOrZero(start);
        Assumed.LessThanOrEqual(start, Length);

        return Buffer.AsMemory(Offset + start, Length - start);
    }

    /// <summary>
    ///  Returns read-only memory over the specified range of characters within this segment.
    /// </summary>
    /// <param name="start">
    ///  The zero-based index, relative to the start of the segment, at which the memory begins.
    /// </param>
    /// <param name="length">The number of characters the memory covers.</param>
    /// <returns>
    ///  A <see cref="ReadOnlyMemory{T}"/> covering the requested range.
    /// </returns>
    public ReadOnlyMemory<char> AsMemory(int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        return Buffer.AsMemory(Offset + start, length);
    }

    /// <summary>
    ///  Copies the characters of this segment into <paramref name="destination"/>. Throws if the
    ///  destination is too short.
    /// </summary>
    /// <param name="destination">The span to copy the segment's characters into.</param>
    public void CopyTo(Span<char> destination)
        => AsSpan().CopyTo(destination);

    /// <summary>
    ///  Attempts to copy the characters of this segment into <paramref name="destination"/>, returning
    ///  <see langword="false"/> without copying if the destination is too short.
    /// </summary>
    /// <param name="destination">The span to copy the segment's characters into.</param>
    /// <returns>
    ///  <see langword="true"/> if the characters were copied; <see langword="false"/> if
    ///  <paramref name="destination"/> was too short.
    /// </returns>
    public bool TryCopyTo(Span<char> destination)
        => AsSpan().TryCopyTo(destination);

    /// <summary>
    ///  Copies <paramref name="count"/> characters starting at <paramref name="sourceIndex"/> into
    ///  <paramref name="destination"/> beginning at <paramref name="destinationIndex"/>. Uses the native
    ///  <see cref="string.CopyTo(int, char[], int, int)"/> on all targets, avoiding span construction.
    /// </summary>
    /// <param name="sourceIndex">
    ///  The zero-based index, relative to the start of the segment, of the first character to copy.
    /// </param>
    /// <param name="destination">The array to copy the characters into.</param>
    /// <param name="destinationIndex">The index in <paramref name="destination"/> at which copying begins.</param>
    /// <param name="count">The number of characters to copy.</param>
    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count)
    {
        Assumed.NotNull(destination);
        Assumed.PositiveOrZero(sourceIndex);
        Assumed.PositiveOrZero(count);
        Assumed.LessThanOrEqual(sourceIndex + count, Length);

        if (count > 0)
        {
            // count > 0 implies Length > 0, so the segment has a non-null buffer.
            Buffer!.CopyTo(Offset + sourceIndex, destination, destinationIndex, count);
        }
    }

    /// <summary>
    ///  Forms a slice of this segment starting at the specified index and continuing to the end. Enables
    ///  range expressions such as <c>segment[start..]</c>.
    /// </summary>
    /// <param name="start">
    ///  The zero-based index, relative to the start of this segment, at which the slice begins.
    /// </param>
    /// <returns>
    ///  A <see cref="StringSegment"/> covering the characters from <paramref name="start"/> to the end.
    /// </returns>
    public StringSegment Slice(int start)
    {
        Assumed.PositiveOrZero(start);
        Assumed.LessThanOrEqual(start, Length);

        // A null segment can only be sliced with (0), which leaves it unchanged.
        return HasValue ? new StringSegment(Buffer, Offset + start, Length - start) : this;
    }

    /// <summary>
    ///  Forms a slice of this segment of the given length starting at the specified index. Enables range
    ///  expressions such as <c>segment[start..end]</c>.
    /// </summary>
    /// <param name="start">
    ///  The zero-based index, relative to the start of this segment, at which the slice begins.
    /// </param>
    /// <param name="length">The number of characters in the slice.</param>
    /// <returns>
    ///  A <see cref="StringSegment"/> covering the requested range.
    /// </returns>
    public StringSegment Slice(int start, int length)
    {
        Assumed.PositiveOrZero(start);
        Assumed.PositiveOrZero(length);
        Assumed.LessThanOrEqual(start + length, Length);

        // A null segment can only be sliced with (0, 0), which leaves it unchanged.
        return HasValue ? new StringSegment(Buffer, Offset + start, length) : this;
    }

    /// <summary>
    ///  Returns a reference to the first character of the segment, enabling the segment to be used in a
    ///  <see langword="fixed"/> statement. Returns a null reference for a null or empty segment.
    /// </summary>
    /// <returns>
    ///  A reference to the first character, or a null reference if the segment is null or empty.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public unsafe ref char GetPinnableReference()
    {
        if (!HasValue || Length == 0)
        {
            // Return a null ref so the compiler emits a null pointer.
            return ref Unsafe.AsRef<char>(null);
        }

        return ref MemoryMarshal.GetReference(AsSpan());
    }

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

#if NET
        return AsSpan().Equals(other.AsSpan(), comparisonType);
#else
        // For ordinal comparisons, segments of different lengths can never be equal, so we can skip the
        // character-by-character comparison entirely.
        if (comparisonType is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase && Length != other.Length)
        {
            return false;
        }

        return CompareCore(Buffer, Offset, Length, other.Buffer!, other.Offset, other.Length, comparisonType) == 0;
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

#if NET
        return AsSpan().Equals(other.AsSpan(), comparisonType);
#else
        // For ordinal comparisons, segments of different lengths can never be equal, so we can skip the
        // character-by-character comparison entirely.
        if (comparisonType is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase && Length != other!.Length)
        {
            return false;
        }

        return CompareCore(Buffer, Offset, Length, other!, 0, other!.Length, comparisonType) == 0;
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

    /// <summary>
    ///  Compares two <see cref="StringSegment"/> instances using the specified comparison and returns an
    ///  integer that indicates their relative order. A null segment sorts before a non-null segment, and two
    ///  null segments compare as equal.
    /// </summary>
    /// <param name="a">The first segment to compare.</param>
    /// <param name="b">The second segment to compare.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  A negative value if <paramref name="a"/> precedes <paramref name="b"/>, zero if they occur in the
    ///  same position in the sort order, or a positive value if <paramref name="a"/> follows <paramref name="b"/>.
    /// </returns>
    public static int Compare(StringSegment a, StringSegment b, StringComparison comparisonType)
    {
        if (!a.HasValue)
        {
            // A null segment sorts before any non-null segment; two null segments are equal.
            return b.HasValue ? -1 : 0;
        }

        if (!b.HasValue)
        {
            return 1;
        }

        return CompareCore(a.Buffer, a.Offset, a.Length, b.Buffer, b.Offset, b.Length, comparisonType);
    }

    /// <summary>
    ///  Compares this segment to <paramref name="other"/> using an ordinal comparison and returns an integer
    ///  that indicates their relative order.
    /// </summary>
    /// <param name="other">The segment to compare with this segment.</param>
    /// <returns>
    ///  A negative value if this segment precedes <paramref name="other"/>, zero if they occur in the same
    ///  position in the sort order, or a positive value if this segment follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(StringSegment other)
        => Compare(this, other, StringComparison.Ordinal);

    /// <summary>
    ///  Compares this segment to <paramref name="other"/> using the specified comparison and returns an
    ///  integer that indicates their relative order. Note that, unlike
    ///  <see cref="string.CompareTo(string)"/>, the parameterless overloads use
    ///  <see cref="StringComparison.Ordinal"/> rather than a culture-sensitive comparison.
    /// </summary>
    /// <param name="other">The segment to compare with this segment.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the segments are compared.</param>
    /// <returns>
    ///  A negative value if this segment precedes <paramref name="other"/>, zero if they occur in the same
    ///  position in the sort order, or a positive value if this segment follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(StringSegment other, StringComparison comparisonType)
        => Compare(this, other, comparisonType);

    /// <summary>
    ///  Compares this segment to <paramref name="other"/> using an ordinal comparison and returns an integer
    ///  that indicates their relative order.
    /// </summary>
    /// <param name="other">The string to compare with this segment.</param>
    /// <returns>
    ///  A negative value if this segment precedes <paramref name="other"/>, zero if they occur in the same
    ///  position in the sort order, or a positive value if this segment follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(string? other)
        => Compare(this, other, StringComparison.Ordinal);

    /// <summary>
    ///  Compares this segment to <paramref name="other"/> using the specified comparison and returns an
    ///  integer that indicates their relative order.
    /// </summary>
    /// <param name="other">The string to compare with this segment.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  A negative value if this segment precedes <paramref name="other"/>, zero if they occur in the same
    ///  position in the sort order, or a positive value if this segment follows <paramref name="other"/>.
    /// </returns>
    public int CompareTo(string? other, StringComparison comparisonType)
        => Compare(this, other, comparisonType);

    /// <summary>
    ///  Compares two string regions using the given <see cref="StringComparison"/>, allowing the two regions
    ///  to differ in length, and returns an integer that indicates their relative order.
    /// </summary>
    /// <param name="buffer1">The string containing the first region.</param>
    /// <param name="offset1">The index in <paramref name="buffer1"/> at which the first region begins.</param>
    /// <param name="length1">The number of characters in the first region.</param>
    /// <param name="buffer2">The string containing the second region.</param>
    /// <param name="offset2">The index in <paramref name="buffer2"/> at which the second region begins.</param>
    /// <param name="length2">The number of characters in the second region.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the regions are compared.</param>
    /// <returns>
    ///  A negative value if the first region precedes the second, zero if they are equal, or a positive value
    ///  if the first region follows the second.
    /// </returns>
    private static int CompareCore(string buffer1, int offset1, int length1, string buffer2, int offset2, int length2, StringComparison comparisonType)
#if NET
        => buffer1.AsSpan(offset1, length1).CompareTo(buffer2.AsSpan(offset2, length2), comparisonType);
#else
        // Use the independent-length CompareInfo overload for all modes: unlike the single-length
        // string.Compare overload, it orders correctly when the two regions differ in length.
        => comparisonType switch
        {
            StringComparison.Ordinal
                => CultureInfo.InvariantCulture.CompareInfo.Compare(buffer1, offset1, length1, buffer2, offset2, length2, CompareOptions.Ordinal),
            StringComparison.OrdinalIgnoreCase
                => CultureInfo.InvariantCulture.CompareInfo.Compare(buffer1, offset1, length1, buffer2, offset2, length2, CompareOptions.OrdinalIgnoreCase),
            StringComparison.CurrentCulture
                => CultureInfo.CurrentCulture.CompareInfo.Compare(buffer1, offset1, length1, buffer2, offset2, length2, CompareOptions.None),
            StringComparison.CurrentCultureIgnoreCase
                => CultureInfo.CurrentCulture.CompareInfo.Compare(buffer1, offset1, length1, buffer2, offset2, length2, CompareOptions.IgnoreCase),
            StringComparison.InvariantCulture
                => CultureInfo.InvariantCulture.CompareInfo.Compare(buffer1, offset1, length1, buffer2, offset2, length2, CompareOptions.None),
            StringComparison.InvariantCultureIgnoreCase
                => CultureInfo.InvariantCulture.CompareInfo.Compare(buffer1, offset1, length1, buffer2, offset2, length2, CompareOptions.IgnoreCase),

            _ => Assumed.Unreachable<int>(),
        };
#endif

    /// <summary>
    ///  Returns the hash code for this segment. The hash is consistent with ordinal equality, so equal
    ///  segments over different buffers produce the same hash code.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
#if NET
        return string.GetHashCode(AsSpan());
#else
        if (!HasValue)
        {
            return 0;
        }

        // .NET Framework uses the DJB2 (Daniel J. Bernstein) algorithm. It iterates through to the first null character.
        // Here we don't know if we'll have one so we use the length and unroll to get the next best thing. The speed
        // converges on rough equivalence with about 100 characters and above. At smaller sizes there is about a
        // 5ns overhead penalty.

        int remaining = Length;

        if (remaining == 0)
        {
            // "".GetHashCode();
            return 371857150;
        }

        unsafe
        {
            fixed (char* ptr = Buffer)
            {
                // For strings 10-100+ chars, unrolling by 4 provides best performance
                int hash1 = 5381;
                int hash2 = hash1;

                char* p = ptr + Offset;

                // Process 4 characters at a time
                while (remaining >= 4)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                    hash2 = ((hash2 << 5) + hash2) ^ p[1];
                    hash1 = ((hash1 << 5) + hash1) ^ p[2];
                    hash2 = ((hash2 << 5) + hash2) ^ p[3];

                    p += 4;
                    remaining -= 4;
                }

                // Handle remaining characters
                if (remaining == 3)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                    hash2 = ((hash2 << 5) + hash2) ^ p[1];
                    hash1 = ((hash1 << 5) + hash1) ^ p[2];
                }
                else if (remaining == 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                    hash2 = ((hash2 << 5) + hash2) ^ p[1];
                }
                else if (remaining == 1)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
#endif
    }

    /// <summary>
    ///  Indicates whether this segment begins with the specified character.
    /// </summary>
    /// <param name="value">The character to compare to the start of the segment.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment is non-empty and its first character equals
    ///  <paramref name="value"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool StartsWith(char value)
        => HasValue && Length > 0 && Buffer[Offset] == value;

    /// <summary>
    ///  Indicates whether this segment begins with the specified string using the specified comparison.
    /// </summary>
    /// <param name="value">The string to compare to the start of the segment.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment begins with <paramref name="value"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool StartsWith(string value, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.NotNull(value);

        if (value.Length > Length)
        {
            return false;
        }

        // A prefix match is an equality comparison of this segment's leading value.Length characters
        // against value. CompareCore handles the culture-sensitive comparison on both target frameworks.
        return value.Length == 0
            || CompareCore(Buffer!, Offset, value.Length, value, 0, value.Length, comparisonType) == 0;
    }

    /// <summary>
    ///  Indicates whether this segment begins with the characters in the specified span using an ordinal
    ///  comparison.
    /// </summary>
    /// <param name="value">The characters to compare to the start of the segment.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment begins with <paramref name="value"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool StartsWith(ReadOnlySpan<char> value)
        => AsSpan().StartsWith(value);

    /// <summary>
    ///  Indicates whether this segment ends with the specified character.
    /// </summary>
    /// <param name="value">The character to compare to the end of the segment.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment is non-empty and its last character equals
    ///  <paramref name="value"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool EndsWith(char value)
        => HasValue && Length > 0 && Buffer[Offset + Length - 1] == value;

    /// <summary>
    ///  Indicates whether this segment ends with the specified string using the specified comparison.
    /// </summary>
    /// <param name="value">The string to compare to the end of the segment.</param>
    /// <param name="comparisonType">One of the enumeration values that specifies how the values are compared.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment ends with <paramref name="value"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool EndsWith(string value, StringComparison comparisonType = StringComparison.Ordinal)
    {
        Assumed.NotNull(value);

        if (value.Length > Length)
        {
            return false;
        }

        // A suffix match is an equality comparison of this segment's trailing value.Length characters
        // against value. CompareCore handles the culture-sensitive comparison on both target frameworks.
        return value.Length == 0
            || CompareCore(Buffer!, Offset + Length - value.Length, value.Length, value, 0, value.Length, comparisonType) == 0;
    }

    /// <summary>
    ///  Indicates whether this segment ends with the characters in the specified span using an ordinal
    ///  comparison.
    /// </summary>
    /// <param name="value">The characters to compare to the end of the segment.</param>
    /// <returns>
    ///  <see langword="true"/> if the segment ends with <paramref name="value"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool EndsWith(ReadOnlySpan<char> value)
        => AsSpan().EndsWith(value);

    /// <summary>
    ///  Returns the segment's characters as a <see cref="string"/>, or the empty string for a null segment.
    /// </summary>
    /// <returns>
    ///  The segment's characters as a string.
    /// </returns>
    public override string ToString()
        => Value ?? string.Empty;

    /// <summary>
    ///  Implicitly converts a <see cref="string"/> to a <see cref="StringSegment"/> that views the entire
    ///  string. A <see langword="null"/> string yields a null segment.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    /// <returns>
    ///  A <see cref="StringSegment"/> over <paramref name="value"/>.
    /// </returns>
    public static implicit operator StringSegment(string? value)
        => new(value);

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to a read-only span over its characters.
    /// </summary>
    /// <param name="segment">The segment to convert.</param>
    /// <returns>
    ///  A <see cref="ReadOnlySpan{T}"/> covering the segment's characters.
    /// </returns>
    public static implicit operator ReadOnlySpan<char>(StringSegment segment)
        => segment.AsSpan();

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to read-only memory over its characters.
    /// </summary>
    /// <param name="segment">The segment to convert.</param>
    /// <returns>
    ///  A <see cref="ReadOnlyMemory{T}"/> covering the segment's characters.
    /// </returns>
    public static implicit operator ReadOnlyMemory<char>(StringSegment segment)
        => segment.AsMemory();

    /// <summary>
    ///  Indicates whether two segments are equal using an ordinal comparison.
    /// </summary>
    /// <param name="left">The first segment to compare.</param>
    /// <param name="right">The second segment to compare.</param>
    /// <returns>
    ///  <see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator ==(StringSegment left, StringSegment right)
        => left.Equals(right);

    /// <summary>
    ///  Indicates whether two segments are not equal using an ordinal comparison.
    /// </summary>
    /// <param name="left">The first segment to compare.</param>
    /// <param name="right">The second segment to compare.</param>
    /// <returns>
    ///  <see langword="true"/> if the segments are not equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator !=(StringSegment left, StringSegment right)
        => !left.Equals(right);
}
