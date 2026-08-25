// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.Text;

/// <summary>
///  Identifies a buffer-independent range that can be combined with a string to create a
///  <see cref="StringSegment"/>.
/// </summary>
/// <remarks>
///  The default value represents an empty range at offset zero. <see cref="Null"/> represents the absence of
///  a range and converts to a null <see cref="StringSegment"/>.
/// </remarks>
internal readonly record struct StringSegmentRange
{
    /// <summary>
    ///  Initializes a new range.
    /// </summary>
    /// <param name="offset">The zero-based offset of the range within a string, or <c>-1</c> for null.</param>
    /// <param name="length">The number of characters in the range.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public StringSegmentRange(int offset, int length)
    {
        if (offset < -1 || length < 0 || (offset == -1 && length != 0))
        {
            ValidateArguments(offset, length);
        }

        Offset = offset;
        Length = length;

        static void ValidateArguments(int offset, int length)
        {
            Assumed.GreaterThanOrEqual(offset, -1);

            if (offset == -1)
            {
                Assumed.Zero(length);
            }
            else
            {
                Assumed.PositiveOrZero(length);
            }
        }
    }

    /// <summary>
    ///  Gets the zero-based offset of the range within a string, or <c>-1</c> for null.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    ///  Gets the number of characters in the range.
    /// </summary>
    public int Length { get; }

    /// <summary>
    ///  Gets a range representing the absence of a <see cref="StringSegment"/>.
    /// </summary>
    public static StringSegmentRange Null { get; } = new(offset: -1, length: 0);

    /// <summary>
    ///  Gets a value indicating whether this range represents a null <see cref="StringSegment"/>.
    /// </summary>
    public bool IsNull => Offset == -1 && Length == 0;

    /// <summary>
    ///  Gets a value indicating whether this range represents an empty <see cref="StringSegment"/>.
    /// </summary>
    public bool IsEmpty => Offset >= 0 && Length == 0;

    /// <summary>
    ///  Creates a <see cref="StringSegment"/> over this range in <paramref name="buffer"/>.
    /// </summary>
    /// <param name="buffer">The string containing this range.</param>
    /// <returns>
    ///  A segment over <paramref name="buffer"/>, or a null segment when this range is <see cref="Null"/> or
    ///  <paramref name="buffer"/> is <see langword="null"/>.
    /// </returns>
    public StringSegment ToSegment(string? buffer)
        => new(buffer, this);

    /// <summary>
    ///  Creates a buffer-independent range for <paramref name="segment"/>.
    /// </summary>
    /// <param name="segment">The segment whose offset and length are retained.</param>
    /// <returns>
    ///  The segment range, or <see cref="Null"/> when <paramref name="segment"/> is a null segment.
    /// </returns>
    public static implicit operator StringSegmentRange(StringSegment segment)
        => segment.HasValue
            ? new StringSegmentRange(segment.Offset, segment.Length)
            : Null;
}
