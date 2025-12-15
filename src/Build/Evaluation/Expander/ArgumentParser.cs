// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

#if NETFRAMEWORK
using static Microsoft.NET.StringTools.Strings;
#endif

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides helper methods for parsing and validating arguments passed to intrinsic functions.
/// </summary>
internal static class ArgumentParser
{
    internal ref struct ArgProcessor
    {
        private readonly ReadOnlySpan<object?> _args;
        private int _index;

        public ArgProcessor(ReadOnlySpan<object?> args)
        {
            _args = args;
            _index = 0;
        }

        public readonly bool AtEnd => _args.Length == _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetString([NotNullWhen(true)] out string? value)
        {
            if (TryGetNextArg(out object? arg) && arg is string s)
            {
                value = s;
                _index++;
                return true;
            }

            value = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStringOrNull(out string? value)
        {
            if (TryGetNextArg(out object? arg) && arg is string or null)
            {
                value = (string?)arg;
                _index++;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetChar(out char value)
        {
            if (TryGetNextArg(out object? arg))
            {
                if (arg is string { Length: 1 } s)
                {
                    value = s[0];
                    _index++;
                    return true;
                }

                if (arg is char c)
                {
                    value = c;
                    _index++;
                    return true;
                }
            }

            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool TryGetNextArg(out object? arg)
        {
            if (!AtEnd)
            {
                arg = _args[_index];
                return true;
            }

            arg = null;
            return false;
        }

    }

    /// <summary>
    ///  Try to convert value to char.
    ///  Accepts char values directly or single-character strings.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="arg">The converted char value, or <see langword="default"/> if conversion failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the value was successfully converted to char; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryConvertToChar(object? value, out char arg)
    {
        switch (value)
        {
            case char c:
                arg = c;
                return true;

            case string { Length: 1 } s:
                arg = s[0];
                return true;

            default:
                arg = default;
                return false;
        }
    }

    /// <summary>
    ///  Try to convert value to int.
    ///  Accepts int values directly, parseable strings, longs within int range, and doubles that are exact integers within int range.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="arg">The converted int value, or <see langword="default"/> if conversion failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the value was successfully converted to int; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryConvertToInt(object? value, out int arg)
    {
        switch (value)
        {
            case string s when TryParseInt(s, out arg):
                return true;

            case int i:
                arg = i;
                return true;

            case long l and >= int.MinValue and <= int.MaxValue:
                arg = Convert.ToInt32(l);
                return true;

            case double d and >= int.MinValue and <= int.MaxValue:
                arg = Convert.ToInt32(d);

                if (Math.Abs(arg - d) == 0)
                {
                    return true;
                }

                goto default;

            default:
                arg = default;
                return false;
        }
    }

    /// <summary>
    ///  Try to convert value to long.
    ///  Accepts long values directly, parseable strings, ints, and doubles that are exact integers within long range.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="arg">The converted long value, or <see langword="default"/> if conversion failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the value was successfully converted to long; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryConvertToLong(object? value, out long arg)
    {
        switch (value)
        {
            case string s when TryParseLong(s, out arg):
                return true;

            case int i:
                arg = i;
                return true;

            case long l:
                arg = l;
                return true;

            case double d and >= long.MinValue and <= long.MaxValue:
                arg = (long)d;
                if (Math.Abs(arg - d) == 0)
                {
                    return true;
                }

                goto default;

            default:
                arg = default;
                return false;
        }
    }

    /// <summary>
    ///  Try to convert value to double.
    ///  Accepts double values directly, parseable strings, ints, and longs.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <param name="arg">The converted double value, or <see langword="default"/> if conversion failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the value was successfully converted to double; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryConvertToDouble(object? value, out double arg)
    {
        switch (value)
        {
            case string str when TryParseDouble(str, out arg):
                return true;

            case double d:
                arg = d;
                return true;

            case int i:
                arg = i;
                return true;

            case long l:
                arg = l;
                return true;

            default:
                arg = default;
                return false;
        }
    }

    internal static bool TryConvertToStringArray(ReadOnlySpan<object?> args, [NotNullWhen(true)] out string[]? result)
    {
        if (args.Length == 0)
        {
            result = [];
            return true;
        }

        result = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is not string s)
            {
                result = null;
                return false;
            }

            result ??= new string[args.Length];
            result[i] = s;
        }

        return result != null;
    }

    /// <summary>
    ///  Determines whether the value is or can be interpreted as a floating-point number.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>
    ///  <see langword="true"/> if the value is a double or a string that can be parsed as a double; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool IsFloatingPointRepresentation(object? value)
        => value is double || (value is string str && TryParseDouble(str, out _));

    /// <summary>
    ///  Tries to parse a string as an enum value of the specified type.
    ///  Supports both fully-qualified and unqualified enum names (e.g., "Value" or "System.EnumType.Value").
    ///  Rejects integer values to maintain type safety, as casting integers to enums requires explicit casts in C#.
    /// </summary>
    /// <typeparam name="TEnum">The enum type to parse. Must be a struct and an enum.</typeparam>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed enum value, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the string was successfully parsed as an enum value; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryConvertToEnum<TEnum>(object? value, out TEnum result)
        where TEnum : struct
    {
        var enumType = typeof(TEnum);

        if (!enumType.IsEnum || value is not string s)
        {
            result = default;
            return false;
        }

        // Reject int values for enums. In C#, this would require a cast, which is not supported in MSBuild expressions.
        if (TryParseInt(s, out _))
        {
            result = default;
            return false;
        }

        ReadOnlySpan<char> span = s.AsSpan();

        span = SkipTextAndDot(span, enumType.Namespace);
        span = SkipTextAndDot(span, enumType.Name);

#if NET
        // Should this pass 'ignoreCase: true' to be consistent with how MSBuild
        // handles type::method names in property functions?
        if (Enum.TryParse(span, out result))
        {
            return true;
        }
#else
        // Should this pass 'ignoreCase: true' to be consistent with how MSBuild
        // handles type::method names in property functions?
        if (Enum.TryParse(WeakIntern(span), out result))
        {
            return true;
        }
#endif

        result = default;
        return false;

        static ReadOnlySpan<char> SkipTextAndDot(ReadOnlySpan<char> span, ReadOnlySpan<char> text)
        {
            // Should this compare with OrdinalIgnoreCase to be consistent with how MSBuild
            // handles type::method names in property functions?
            if (span.StartsWith(text))
            {
                span = span[..text.Length];

                // Skip trailing dot.
                if (span is ['.', ..])
                {
                    span = span[1..];
                }
            }

            return span;
        }
    }

    /// <summary>
    ///  Tries to parse a string as a double using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed double value, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryParseDouble(string value, out double result)
        => double.TryParse(value, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out result);

    /// <summary>
    ///  Tries to parse a string as an int using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed int value, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseInt(string value, out int result)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out result);

    /// <summary>
    ///  Tries to parse a string as a long using invariant culture.
    /// </summary>
    /// <param name="value">The string to parse.</param>
    /// <param name="result">The parsed long value, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the string was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryParseLong(string value, out long result)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out result);
}
