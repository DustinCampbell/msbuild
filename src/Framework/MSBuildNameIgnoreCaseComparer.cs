// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Text;

namespace Microsoft.Build.Collections;

/// <summary>
///  Compares MSBuild names case-insensitively without requiring constrained ranges or
///  <see cref="StringSegment"/> values to be realized as strings.
/// </summary>
/// <remarks>
///  Use this comparer for MSBuild property, item type, and metadata names, whose grammar is
///  <c>[A-Za-z_][A-Za-z0-9_-]*</c>. It is not a general-purpose case-insensitive string comparer.
/// </remarks>
[Serializable]
internal class MSBuildNameIgnoreCaseComparer :
    IConstrainedEqualityComparer<string>,
    IEqualityComparer<string>,
    IEqualityComparer<StringSegment>
#if NET
    , IAlternateEqualityComparer<StringSegment, string>
#endif
{
    /// <summary>
    ///  The architecture-specific implementation used by legacy equality and hashing paths.
    /// </summary>
    private static readonly NativeMethods.ProcessorArchitectures s_runningProcessorArchitecture =
        NativeMethods.ProcessorArchitecture;

    /// <summary>
    ///  Gets the shared comparer instance.
    /// </summary>
    internal static MSBuildNameIgnoreCaseComparer Default { get; } = new();

    /// <summary>
    ///  Determines whether two MSBuild names are equal.
    /// </summary>
    /// <param name="x">The first name, or <see langword="null"/>.</param>
    /// <param name="y">The second name, or <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> when the names are equal under MSBuild's case-insensitive name
    ///  semantics; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Length != y.Length)
        {
            return false;
        }

        return EqualsCore(x, 0, y, 0, x.Length);
    }

    /// <summary>
    ///  Computes the hash code for an MSBuild name.
    /// </summary>
    /// <param name="obj">The name to hash.</param>
    /// <returns>The case-insensitive MSBuild name hash.</returns>
    public int GetHashCode(string obj)
        => GetHashCode(obj, 0, obj?.Length ?? 0);

    /// <summary>
    ///  Determines whether two source-backed MSBuild names are equal without realizing either segment.
    /// </summary>
    /// <param name="x">The first name segment.</param>
    /// <param name="y">The second name segment.</param>
    /// <returns>
    ///  <see langword="true"/> when the segments are equal under MSBuild's case-insensitive name
    ///  semantics; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(StringSegment x, StringSegment y)
    {
        if (!x.HasValue)
        {
            return !y.HasValue;
        }

        if (!y.HasValue)
        {
            return false;
        }

        if (x.Length != y.Length)
        {
            return false;
        }

        if (ReferenceEquals(x.Buffer, y.Buffer) && x.Offset == y.Offset)
        {
            return true;
        }

        return EqualsCore(x.Buffer, x.Offset, y.Buffer, y.Offset, x.Length);
    }

    /// <summary>
    ///  Computes the hash code for a source-backed MSBuild name.
    /// </summary>
    /// <param name="obj">The name segment to hash.</param>
    /// <returns>The case-insensitive MSBuild name hash.</returns>
    public int GetHashCode(StringSegment obj)
        => GetHashCode(obj.Buffer, obj.Offset, obj.Length);

#if NET
    /// <summary>
    ///  Determines whether a source-backed alternate key equals a stored MSBuild name.
    /// </summary>
    /// <param name="alternate">The alternate name segment.</param>
    /// <param name="other">The stored name.</param>
    /// <returns>
    ///  <see langword="true"/> when the names are equal under MSBuild's case-insensitive name
    ///  semantics; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(StringSegment alternate, string other)
    {
        if (!alternate.HasValue)
        {
            return other is null;
        }

        if (other is null || alternate.Length != other.Length)
        {
            return false;
        }

        if (ReferenceEquals(alternate.Buffer, other) && alternate.Offset == 0)
        {
            return true;
        }

        return EqualsCore(alternate.Buffer, alternate.Offset, other, 0, alternate.Length);
    }

    /// <summary>
    ///  Realizes an alternate key when a dictionary operation needs to store it.
    /// </summary>
    /// <param name="alternate">The alternate name segment.</param>
    /// <returns>The realized name.</returns>
    public string Create(StringSegment alternate)
        => alternate.ValueOrEmpty;
#endif

    /// <summary>
    ///  Determines whether an MSBuild name equals a constrained range of another string.
    /// </summary>
    /// <param name="compareToString">The complete name to compare, or <see langword="null"/>.</param>
    /// <param name="constrainedString">The string containing the constrained name, or <see langword="null"/>.</param>
    /// <param name="start">The constrained name's starting index.</param>
    /// <param name="lengthToCompare">The constrained name's length.</param>
    /// <returns>
    ///  <see langword="true"/> when the names are equal under MSBuild's case-insensitive name
    ///  semantics; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Equals(string? compareToString, string? constrainedString, int start, int lengthToCompare)
    {
        Assumed.PositiveOrZero(
            lengthToCompare,
            $"Invalid {nameof(lengthToCompare)} '{constrainedString}' {start} {lengthToCompare}");
        Assumed.PositiveOrZero(
            start,
            $"Invalid {nameof(start)} '{constrainedString}' {start} {lengthToCompare}");
        Assumed.LessThanOrEqual(
            start,
            (constrainedString?.Length ?? 0) - lengthToCompare,
            $"Invalid {nameof(start)} '{constrainedString}' {start} {lengthToCompare}");

        if (ReferenceEquals(compareToString, constrainedString))
        {
            return true;
        }

        if (compareToString is null ||
            constrainedString is null ||
            lengthToCompare != compareToString.Length)
        {
            return false;
        }

        return EqualsCore(compareToString, 0, constrainedString, start, lengthToCompare);
    }

    /// <summary>
    ///  Computes the hash code for a constrained range containing an MSBuild name.
    /// </summary>
    /// <param name="obj">The string containing the constrained name, or <see langword="null"/>.</param>
    /// <param name="start">The constrained name's starting index.</param>
    /// <param name="length">The constrained name's length.</param>
    /// <returns>
    ///  The case-insensitive MSBuild name hash, or <c>0</c> when <paramref name="obj"/> is
    ///  <see langword="null"/>.
    /// </returns>
    public int GetHashCode(string? obj, int start, int length)
    {
        if (obj is null)
        {
            return 0;
        }

        if (s_runningProcessorArchitecture is NativeMethods.ProcessorArchitectures.IA64 or NativeMethods.ProcessorArchitectures.ARM)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Substring(start, length));
        }

        unsafe
        {
            // This algorithm is based on the 32-bit CLR string hash implementation.
            fixed (char* src = obj)
            {
                int hash1 = (5381 << 16) + 5381;
                int hash2 = hash1;
                char* src2 = src + start;
                var pint = (int*)src2;

                while (length > 0)
                {
                    // Fold two UTF-16 code units to uppercase ASCII simultaneously.
                    int val = pint[0] & 0x00DF00DF;

                    // Ignore the second code unit when only one character remains.
                    if (length == 1)
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            val &= 0xFFFF;
                        }
                        else
                        {
                            val &= unchecked((int)0xFFFF0000);
                        }
                    }

                    hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ val;
                    if (length <= 2)
                    {
                        break;
                    }

                    val = pint[1] & 0x00DF00DF;

                    // Ignore the fourth code unit when only three characters remain.
                    if (length == 3)
                    {
                        if (BitConverter.IsLittleEndian)
                        {
                            val &= 0xFFFF;
                        }
                        else
                        {
                            val &= unchecked((int)0xFFFF0000);
                        }
                    }

                    hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ val;
                    pint += 2;
                    length -= 4;
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }

    /// <summary>
    ///  Compares two equal-length string ranges using MSBuild's case-insensitive name semantics.
    /// </summary>
    /// <remarks>
    ///  The caller must validate both ranges and ensure they have equal lengths before invoking this method.
    /// </remarks>
    /// <param name="x">The string containing the first range.</param>
    /// <param name="offsetX">The first range's starting index.</param>
    /// <param name="y">The string containing the second range.</param>
    /// <param name="offsetY">The second range's starting index.</param>
    /// <param name="length">The number of characters to compare.</param>
    /// <returns>
    ///  <see langword="true"/> when the ranges are equal under MSBuild's case-insensitive name
    ///  semantics; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool EqualsCore(string x, int offsetX, string y, int offsetY, int length)
    {
#if NET
        return x.AsSpan(offsetX, length).Equals(y.AsSpan(offsetY, length), StringComparison.OrdinalIgnoreCase);
#else
        if (s_runningProcessorArchitecture is NativeMethods.ProcessorArchitectures.IA64 or NativeMethods.ProcessorArchitectures.ARM)
        {
            return string.Compare(x, offsetX, y, offsetY, length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        // The unsafe path is faster than the BCL implementation for the constrained ASCII grammar accepted
        // by MSBuild names: [A-Za-z_][A-Za-z0-9_-]*.
        unsafe
        {
            fixed (char* px = x, py = y)
            {
                char* currentX = px + offsetX;
                char* currentY = py + offsetY;

                for (int i = 0; i < length; i++, currentX++, currentY++)
                {
                    // Clear bit 0x20 to fold ASCII letters to uppercase.
                    if ((*currentX & 0x00DF) != (*currentY & 0x00DF))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
#endif
    }
}
