// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
#if !NET
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
#endif
using Microsoft.Build.Text;

namespace Microsoft.Build.Collections;

/// <summary>
///  Provides ASCII case-insensitive equality and hashing for MSBuild names.
/// </summary>
/// <remarks>
///  Valid MSBuild names use a restricted ASCII character set. This comparer takes advantage of that
///  constraint to compare or hash whole strings and string regions without first allocating substrings.
///  It is not a general-purpose ordinal case-insensitive comparer.
/// </remarks>
[Serializable]
internal sealed class MSBuildNameIgnoreCaseComparer :
    IConstrainedEqualityComparer<string>,
    IEqualityComparer<string>,
    IEqualityComparer<StringSegment>
{
#if !NET
    private const int HashSeed = (5381 << 16) + 5381;
    private const int HashMultiplier = 1566083941;
    private const int EmptyHashCode = unchecked(HashSeed + (HashSeed * HashMultiplier));

    // The unsafe comparison and packed hashing implementations are not supported on IA64 or ARM.
    private static readonly NativeMethods.ProcessorArchitectures s_runningProcessorArchitecture = NativeMethods.ProcessorArchitecture;

    private static bool IsUnsupportedArchitecture
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => s_runningProcessorArchitecture is NativeMethods.ProcessorArchitectures.IA64 or NativeMethods.ProcessorArchitectures.ARM;
    }
#endif

    /// <summary>
    ///  Gets the shared comparer instance.
    /// </summary>
    public static MSBuildNameIgnoreCaseComparer Default { get; } = new();

    private MSBuildNameIgnoreCaseComparer()
    {
    }

    /// <summary>
    ///  Determines whether two strings represent the same MSBuild name.
    /// </summary>
    /// <param name="x">The first string to compare, or <see langword="null"/>.</param>
    /// <param name="y">The second string to compare, or <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> when both values are <see langword="null"/> or contain the same ASCII
    ///  characters, ignoring case; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? x, string? y)
        => EqualsCore(x, 0, x?.Length ?? 0, y, 0, y?.Length ?? 0);

    /// <summary>
    ///  Determines whether a string and a string segment represent the same MSBuild name.
    /// </summary>
    /// <param name="x">The string to compare, or <see langword="null"/>.</param>
    /// <param name="y">The string segment to compare.</param>
    /// <returns>
    ///  <see langword="true"/> when the values contain the same ASCII characters, ignoring case;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? x, StringSegment y)
        => EqualsCore(x, 0, x?.Length ?? 0, y.Buffer, y.Offset, y.Length);

    /// <summary>
    ///  Determines whether two string segments represent the same MSBuild name.
    /// </summary>
    /// <param name="x">The first string segment to compare.</param>
    /// <param name="y">The second string segment to compare.</param>
    /// <returns>
    ///  <see langword="true"/> when the segments contain the same ASCII characters, ignoring case;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(StringSegment x, StringSegment y)
        => EqualsCore(x.Buffer, x.Offset, x.Length, y.Buffer, y.Offset, y.Length);

    /// <summary>
    ///  Returns a case-insensitive hash code for an MSBuild name.
    /// </summary>
    /// <param name="obj">The string to hash.</param>
    /// <returns>
    ///  A case-insensitive hash code for <paramref name="obj"/>.
    /// </returns>
    public int GetHashCode(string obj)
        => GetHashCodeCore(obj, 0, obj?.Length ?? 0);

    /// <summary>
    ///  Returns a case-insensitive hash code for an MSBuild name stored in a string segment.
    /// </summary>
    /// <param name="obj">The string segment to hash.</param>
    /// <returns>
    ///  A case-insensitive hash code for <paramref name="obj"/>.
    /// </returns>
    public int GetHashCode(StringSegment obj)
        => GetHashCodeCore(obj.Buffer!, obj.Offset, obj.Length);

    /// <summary>
    ///  Determines whether a string and a region of another string represent the same MSBuild name.
    /// </summary>
    /// <param name="compareToString">The string to compare, or <see langword="null"/>.</param>
    /// <param name="constrainedString">The string containing the region to compare, or <see langword="null"/>.</param>
    /// <param name="start">The zero-based starting index of the region in <paramref name="constrainedString"/>.</param>
    /// <param name="lengthToCompare">The number of characters in the region.</param>
    /// <returns>
    ///  <see langword="true"/> when the values contain the same ASCII characters, ignoring case;
    ///  otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? compareToString, string? constrainedString, int start, int lengthToCompare)
    {
        int constrainedStringLength = constrainedString?.Length ?? 0;

        Assumed.PositiveOrZero(lengthToCompare);
        Assumed.PositiveOrZero(start);
        Assumed.LessThanOrEqual(
            start,
            constrainedStringLength - lengthToCompare,
            $"The region starting at {start} with length {lengthToCompare} exceeds the string length {constrainedStringLength}.");

        return EqualsCore(
            x: compareToString,
            xStart: 0,
            xLength: compareToString?.Length ?? 0,
            y: constrainedString,
            yStart: start,
            yLength: lengthToCompare);
    }

    /// <summary>
    ///  Returns a case-insensitive hash code for a region of an MSBuild name.
    /// </summary>
    /// <param name="obj">The string containing the region to hash.</param>
    /// <param name="start">The zero-based starting index of the region.</param>
    /// <param name="length">The number of characters in the region.</param>
    /// <returns>
    ///  A case-insensitive hash code for the specified region.
    /// </returns>
    public int GetHashCode(string obj, int start, int length)
    {
        int objLength = obj?.Length ?? 0;

        Assumed.PositiveOrZero(length);
        Assumed.PositiveOrZero(start);
        Assumed.LessThanOrEqual(
            start,
            objLength - length,
            $"The region starting at {start} with length {length} exceeds the string length {objLength}.");

        return GetHashCodeCore(obj, start, length);
    }

    private static bool EqualsCore(string? x, int xStart, int xLength, string? y, int yStart, int yLength)
    {
        if (ReferenceEquals(x, y) && xStart == yStart && xLength == yLength)
        {
            return true;
        }

        if (x is null || y is null || xLength != yLength)
        {
            return false;
        }

#if NET
        return x.AsSpan(xStart, xLength).Equals(y.AsSpan(yStart, yLength), StringComparison.OrdinalIgnoreCase);
#else
        if (IsUnsupportedArchitecture)
        {
            return string.Compare(x, xStart, y, yStart, xLength, StringComparison.OrdinalIgnoreCase) == 0;
        }

        // Valid MSBuild names are ASCII, so clearing the case bit performs an ordinal
        // case-insensitive comparison without the overhead of the general BCL implementation.
        unsafe
        {
            fixed (char* px = x, py = y)
            {
                for (int i = 0; i < xLength; i++)
                {
                    int chx = px[i + xStart];
                    int chy = py[i + yStart];
                    chx &= 0x00DF;
                    chy &= 0x00DF;

                    if (chx != chy)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
#endif
    }

    private static int GetHashCodeCore(string? obj, int start, int length)
    {
        if (obj is null)
        {
            return 0; // per BCL convention
        }

#if NET
        return string.GetHashCode(obj.AsSpan(start, length), StringComparison.OrdinalIgnoreCase);
#else
        if (IsUnsupportedArchitecture)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Substring(start, length));
        }

        if (length == 0)
        {
            return EmptyHashCode;
        }

        unsafe
        {
            // Based on the 32-bit, non-randomized .NET Framework CLR string::GetHashCode algorithm.
            fixed (char* src = obj)
            {
                int hash1 = HashSeed;
                int hash2 = hash1;

                int* pint = (int*)(src + start);
                int len = length;

                while (len >= 4)
                {
                    // Fold and mix four ASCII characters as two packed 32-bit values.
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ (pint[0] & 0x00DF00DF);
                    hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ (pint[1] & 0x00DF00DF);

                    pint += 2;
                    len -= 4;
                }

                // Mix the first pair of a two- or three-character tail.
                if (len >= 2)
                {
                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ (pint[0] & 0x00DF00DF);
                }

                // Mix the final character of a one- or three-character tail without reading
                // beyond the requested string region.
                if ((len & 1) != 0)
                {
                    int value = ((char*)pint)[len - 1] & 0x00DF;

                    // Match the position the character occupies in a packed 32-bit read.
                    if (!BitConverter.IsLittleEndian)
                    {
                        value <<= 16;
                    }

                    // A one-character tail starts with hash1; a three-character tail has
                    // already mixed its first pair into hash1, so its final character uses hash2.
                    if (len == 1)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ value;
                    }
                    else
                    {
                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ value;
                    }
                }

                return hash1 + (hash2 * HashMultiplier);
            }
        }
#endif
    }
}
