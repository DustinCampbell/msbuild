// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Runtime.CompilerServices;
#endif

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build;

internal static class StringExtensions
{
    /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        => string.IsNullOrEmpty(value);

    /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
        => string.IsNullOrWhiteSpace(value);

    extension(string)
    {
        /// <summary>
        ///  Allocates a string of the specified length filled with null characters.
        /// </summary>
        public static string FastAllocateString(int length)
            // This calls FastAllocateString in the runtime, with extra checks.
            => new('\0', length);

#if !NET
        /// <summary>
        ///  Creates a new string with a specific length and initializes it after creation by using
        ///  the specified callback.
        /// </summary>
        /// <typeparam name="TState">
        ///  The type of the element to pass to <paramref name="action"/>.
        /// </typeparam>
        /// <param name="length">The length of the string to create.</param>
        /// <param name="state">The element to pass to <paramref name="action"/>.</param>
        /// <param name="action">The callback to initialize the string.</param>
        /// <returns>
        ///  The created string.
        /// </returns>
        /// <remarks>
        /// <para>
        ///  The initial content of the destination span passed to <paramref name="action"/> is undefined.
        ///  Therefore, it is the delegate's responsibility to ensure that every element of the span is assigned.
        ///  Otherwise, the resulting string could contain random characters.
        /// </para>
        /// <para>
        ///  To support interop scenarios, the underlying buffer is guaranteed to be at least 1 greater than
        ///  represented by the span parameter of the action callback. This additional index represents the
        ///  null-terminator and, if written, that is the only value supported. Writing any value other than the
        ///  null-terminator corrupts the string and is considered undefined behavior.
        /// </para>
        /// </remarks>
        public static unsafe string Create<TState>(int length, TState state, SpanAction<char, TState> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            if (length == 0)
            {
                return string.Empty;
            }

            string result = FastAllocateString(length);

            fixed (char* p = result)
            {
                action(new Span<char>(p, length), state);
            }

            return result;
        }

        /// <summary>
        ///  Generates a hash code for the specified string value that matches what <see langword="string"/> generates.
        /// </summary>
        /// <remarks>
        ///  On .NET Framework strings don't go beyond embedded nulls when calculating hash codes. If this matters to
        ///  you, you'll need to slice the span to the first null character. In addition, this is not a safe hashing
        ///  algorithm, it is not resistant to hash collisions, and should not be used for security purposes. It is meant
        ///  to give you a hash code that matches what <see langword="string"/> generates for the same value.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetHashCode(ReadOnlySpan<char> value)
        {
            // .NET Framework uses the DJB2 (Daniel J. Bernstein) algorithm. It iterates through to the first null character.
            // Here we don't know if we'll have one so we use the length and unroll to get the next best thing. The speed
            // converges on rough equivalence with about 100 characters and above. At smaller sizes there is about a
            // 5ns overhead penalty.

            if (value.IsEmpty)
            {
                // "".GetHashCode();
                return 371857150;
            }

            fixed (char* ptr = value)
            {
                // For strings 10-100+ chars, unrolling by 4 provides best performance
                int hash1 = 5381;
                int hash2 = hash1;

                char* p = ptr;
                int remaining = value.Length;

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
}

#if !NET
/// <summary>
///  Encapsulates a method that receives a span of objects of type <typeparamref name="T"/> and a state 
///  object of type <typeparamref name="TArg"/>.
/// </summary>
/// <typeparam name="T">The type of the objects in the span.</typeparam>
/// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
/// <param name="span">A span of objects of type <typeparamref name="T"/>.</param>
/// <param name="arg">A state object of type <typeparamref name="TArg"/>.</param>
/// <remarks>
///  On modern .NET, this delegate is declared with an 'allows ref struct' constraint on <typeparamref name="TArg"/>.
/// </remarks>
internal delegate void SpanAction<T, in TArg>(Span<T> span, TArg arg);
#endif
