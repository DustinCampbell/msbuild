// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Build;

internal static class StringExtensions
{
    /// <inheritdoc cref="string.IsNullOrEmpty(string)"/>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value)
        => string.IsNullOrEmpty(value);

    /// <inheritdoc cref="string.IsNullOrWhiteSpace(string)"/>
    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value)
        => string.IsNullOrWhiteSpace(value);

    /// <summary>
    ///  Gets the bounds of <paramref name="value"/> after trimming leading and trailing white-space characters.
    /// </summary>
    /// <param name="value">The string whose trim bounds to compute.</param>
    /// <param name="startIndex">The index at which the trimmed value begins.</param>
    /// <param name="length">The length of the trimmed value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static void GetTrimBounds(this string value, out int startIndex, out int length)
    {
        if (value is null)
        {
            ThrowInvalidTrimBounds(value, 0, 0);
        }

        GetTrimBoundsCore(value, 0, value.Length, out startIndex, out length);
    }

    /// <summary>
    ///  Gets the bounds of a range within <paramref name="value"/> after trimming leading and trailing white-space
    ///  characters.
    /// </summary>
    /// <param name="value">The string containing the range whose trim bounds to compute.</param>
    /// <param name="startIndex">The index at which the range begins.</param>
    /// <param name="length">The length of the range.</param>
    /// <param name="trimmedStartIndex">The index at which the trimmed range begins.</param>
    /// <param name="trimmedLength">The length of the trimmed range.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  <paramref name="startIndex"/> or <paramref name="length"/> does not identify a valid range within
    ///  <paramref name="value"/>.
    /// </exception>
    public static void GetTrimBounds(this string value, int startIndex, int length, out int trimmedStartIndex, out int trimmedLength)
    {
        if (value is null ||
            (uint)startIndex > (uint)value.Length ||
            (uint)length > (uint)(value.Length - startIndex))
        {
            ThrowInvalidTrimBounds(value, startIndex, length);
        }

        GetTrimBoundsCore(value, startIndex, length, out trimmedStartIndex, out trimmedLength);
    }

    private static void GetTrimBoundsCore(string value, int startIndex, int length, out int trimmedStartIndex, out int trimmedLength)
    {
        int endIndex = startIndex + length;

        while (startIndex < endIndex && char.IsWhiteSpace(value[startIndex]))
        {
            startIndex++;
        }

        while (endIndex > startIndex && char.IsWhiteSpace(value[endIndex - 1]))
        {
            endIndex--;
        }

        trimmedStartIndex = startIndex;
        trimmedLength = endIndex - startIndex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidTrimBounds([NotNull] string? value, int startIndex, int length)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(startIndex, value.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, value.Length - startIndex);
    }

    extension(string)
    {
        /// <summary>
        ///  Allocates a string of the specified length filled with null characters.
        /// </summary>
        /// <remarks>
        /// <para>
        ///  This is implemented as <c>new string('\0', length)</c>, which is effectively a pure
        ///  allocation with no redundant zero-fill on <em>both</em> .NET Framework and modern .NET.
        ///  Newly allocated managed memory is already zero-initialized by the GC, and the
        ///  <see cref="string(char, int)"/> constructor special-cases <c>'\0'</c> to skip the
        ///  memset that it would otherwise perform for a non-null fill character:
        /// </para>
        /// <list type="bullet">
        ///  <item>
        ///   <description>
        ///    On modern .NET, the constructor checks <c>if (c != '\0')</c> before filling, so the
        ///    fill is elided for <c>'\0'</c> (see the runtime's <c>String.Ctor(char, int)</c>).
        ///   </description>
        ///  </item>
        ///  <item>
        ///   <description>
        ///    On .NET Framework, the constructor is a native internal call, but it likewise skips
        ///    the fill for <c>'\0'</c>. This was verified empirically with BenchmarkDotNet: on
        ///    net472, <c>new string('\0', n)</c> tracks the raw allocation lower bound and is many
        ///    times faster than <c>new string('a', n)</c> (the gap scaling linearly with <c>n</c>,
        ///    the signature of an elided fill).
        ///   </description>
        ///  </item>
        /// </list>
        /// </remarks>
        public static string FastAllocateString(int length)
            // new string('\0', length) skips the memset on both .NET Framework and modern .NET
            // (the ctor special-cases '\0'), so this is effectively a pure allocation.
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
