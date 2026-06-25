// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if !NET
using System.Runtime.CompilerServices;
#endif

namespace Microsoft.Build.Text;

internal readonly partial struct StringSegment
{
    /// <summary>
    ///  Returns the hash code for this segment. The hash is consistent with ordinal equality, so equal
    ///  segments over different buffers produce the same hash code.
    /// </summary>
    /// <returns>
    ///  A 32-bit signed integer hash code.
    /// </returns>
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
    ///  Returns the hash code for this segment using the specified comparison. Only
    ///  <see cref="StringComparison.Ordinal"/> and <see cref="StringComparison.OrdinalIgnoreCase"/> are
    ///  supported.
    /// </summary>
    /// <param name="comparisonType">
    ///  The comparison whose equality semantics the hash must be consistent with.
    /// </param>
    /// <returns>
    ///  A 32-bit signed integer hash code.
    /// </returns>
    public int GetHashCode(StringComparison comparisonType)
        => comparisonType switch
        {
            StringComparison.Ordinal => GetHashCode(),
            StringComparison.OrdinalIgnoreCase => GetHashCodeOrdinalIgnoreCase(),
            _ => Assumed.Unreachable<int>(),
        };

    /// <summary>
    ///  Returns a hash code consistent with <see cref="StringComparison.OrdinalIgnoreCase"/> equality, so
    ///  segments that differ only by case produce the same hash code.
    /// </summary>
    /// <returns>
    ///  A 32-bit signed integer hash code.
    /// </returns>
    private int GetHashCodeOrdinalIgnoreCase()
    {
#if NET
        return string.GetHashCode(AsSpan(), StringComparison.OrdinalIgnoreCase);
#else
        if (!HasValue)
        {
            return 0;
        }

        // Mirror the ordinal DJB2 hash but case-fold each character so that case-insensitively equal segments
        // hash identically. Unlike the ordinal path there is no external contract to match, so the empty
        // segment simply hashes to the DJB2 seed result.
        int remaining = Length;

        unsafe
        {
            fixed (char* ptr = Buffer)
            {
                int hash1 = 5381;
                int hash2 = hash1;

                char* p = ptr + Offset;

                // Process 4 characters at a time
                while (remaining >= 4)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ FoldOrdinalIgnoreCase(p[0]);
                    hash2 = ((hash2 << 5) + hash2) ^ FoldOrdinalIgnoreCase(p[1]);
                    hash1 = ((hash1 << 5) + hash1) ^ FoldOrdinalIgnoreCase(p[2]);
                    hash2 = ((hash2 << 5) + hash2) ^ FoldOrdinalIgnoreCase(p[3]);

                    p += 4;
                    remaining -= 4;
                }

                // Handle remaining characters
                if (remaining == 3)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ FoldOrdinalIgnoreCase(p[0]);
                    hash2 = ((hash2 << 5) + hash2) ^ FoldOrdinalIgnoreCase(p[1]);
                    hash1 = ((hash1 << 5) + hash1) ^ FoldOrdinalIgnoreCase(p[2]);
                }
                else if (remaining == 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ FoldOrdinalIgnoreCase(p[0]);
                    hash2 = ((hash2 << 5) + hash2) ^ FoldOrdinalIgnoreCase(p[1]);
                }
                else if (remaining == 1)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ FoldOrdinalIgnoreCase(p[0]);
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        // Case-folds a character for ordinal-ignore-case hashing. ASCII characters use a cheap
        // 0x00DF uppercase mask, which is exact within the ASCII range; non-ASCII characters fall
        // back to the invariant upper-casing.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FoldOrdinalIgnoreCase(char value)
            => value < 0x80 ? value & 0x00DF : s_invariantTextInfo.ToUpper(value);
#endif
    }
}
