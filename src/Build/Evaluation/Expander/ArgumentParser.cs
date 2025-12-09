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
    /// <summary>
    ///  Tries to parse a single string argument from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly one string argument was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArg(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0)
    {
        if (args is [string value0])
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        return false;
    }

    /// <summary>
    ///  Tries to parse two string arguments from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The first parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg1">The second parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly two string arguments were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1)
    {
        if (args is [string value0, string value1])
        {
            arg0 = value0;
            arg1 = value1;
            return true;
        }

        arg0 = null;
        arg1 = null;
        return false;
    }

    /// <summary>
    ///  Tries to parse three string arguments from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The first parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg1">The second parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg2">The third parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly three string arguments were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1,
        [NotNullWhen(true)] out string? arg2)
    {
        if (args is [string value0, string value1, string value2])
        {
            arg0 = value0;
            arg1 = value1;
            arg2 = value2;
            return true;
        }

        arg0 = null;
        arg1 = null;
        arg2 = null;
        return false;
    }

    /// <summary>
    ///  Tries to parse four string arguments from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The first parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg1">The second parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg2">The third parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg3">The fourth parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly four string arguments were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1,
        [NotNullWhen(true)] out string? arg2,
        [NotNullWhen(true)] out string? arg3)
    {
        if (args is [string value0, string value1, string value2, string value3])
        {
            arg0 = value0;
            arg1 = value1;
            arg2 = value2;
            arg3 = value3;
            return true;
        }

        arg0 = null;
        arg1 = null;
        arg2 = null;
        arg3 = null;
        return false;
    }

    /// <summary>
    ///  Tries to parse one string argument and two integer arguments from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg1">The first parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <param name="arg2">The second parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if one string and two integer arguments were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0,
        out int arg1,
        out int arg2)
    {
        if (args is [string value0, string value1, string value2] &&
            TryParseInt(value1, out arg1) &&
            TryParseInt(value2, out arg2))
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        arg1 = default;
        arg2 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse a single <see cref="Version"/> argument from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed Version argument, or <see langword="null"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly one Version argument was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArg(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out Version? arg0)
    {
        if (args is [string value0] &&
            Version.TryParse(value0, out arg0))
        {
            return true;
        }

        arg0 = null;
        return false;
    }

    /// <summary>
    ///  Tries to parse a single char argument from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed char argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly one char argument was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArg(ReadOnlySpan<object?> args, out char arg0)
    {
        if (args is [var value0] &&
            TryConvertToChar(value0, out arg0))
        {
            return true;
        }

        arg0 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse a single integer argument from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly one integer argument was successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArg(ReadOnlySpan<object?> args, out int arg0)
    {
        if (args is [var value0] &&
            TryConvertToInt(value0, out arg0))
        {
            return true;
        }

        arg0 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse two integer arguments from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The first parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <param name="arg1">The second parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly two integer arguments were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(ReadOnlySpan<object?> args, out int arg0, out int arg1)
    {
        if (args is [var value0, var value1] &&
            TryConvertToInt(value0, out arg0) &&
            TryConvertToInt(value1, out arg1))
        {
            return true;
        }

        arg0 = default;
        arg1 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse two double arguments from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The first parsed double argument, or <see langword="default"/> if parsing failed.</param>
    /// <param name="arg1">The second parsed double argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if exactly two double arguments were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(ReadOnlySpan<object?> args, out double arg0, out double arg1)
    {
        if (args is [var value0, var value1] &&
            TryConvertToDouble(value0, out arg0) &&
            TryConvertToDouble(value1, out arg1))
        {
            return true;
        }

        arg0 = default;
        arg1 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse one integer argument and one char argument from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <param name="arg1">The parsed char argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if one integer and one char argument were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(ReadOnlySpan<object?> args, out int arg0, out char arg1)
    {
        if (args is [var value0, var value1] &&
            TryConvertToInt(value0, out arg0) &&
            TryConvertToChar(value1, out arg1))
        {
            return true;
        }

        arg0 = default;
        arg1 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse one string argument and one integer argument from the provided arguments.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg1">The parsed integer argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if one string and one integer argument were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0,
        out int arg1)
    {
        if (args is [string value0, string value1] &&
            TryParseInt(value1, out arg1))
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        arg1 = default;
        return false;
    }

    /// <summary>
    ///  Tries to parse one string argument and one <see cref="StringComparison"/> enum argument from the provided arguments.
    ///  Supports both fully-qualified and unqualified enum names (e.g., "Ordinal" or "System.StringComparison.Ordinal").
    ///  Rejects integer values to maintain type safety.
    /// </summary>
    /// <param name="args">The arguments to parse.</param>
    /// <param name="arg0">The parsed string argument, or <see langword="null"/> if parsing failed.</param>
    /// <param name="arg1">The parsed StringComparison argument, or <see langword="default"/> if parsing failed.</param>
    /// <returns>
    ///  <see langword="true"/> if one string and one StringComparison argument were successfully parsed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryGetArgs(
        ReadOnlySpan<object?> args,
        [NotNullWhen(true)] out string? arg0,
        out StringComparison arg1)
    {
        if (args is [string value0, string value1] &&
            TryParseEnum(value1, out arg1))
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        arg1 = default;
        return false;
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
    ///  Executes an arithmetic operation on two arguments, automatically selecting between integer and floating-point overloads.
    ///  Prefers integer arithmetic if both arguments can be converted to long; otherwise, uses double arithmetic.
    /// </summary>
    /// <param name="args">The array of arguments (must contain exactly two elements).</param>
    /// <param name="integerOperation">The operation to perform for integer arithmetic.</param>
    /// <param name="realOperation">The operation to perform for floating-point arithmetic.</param>
    /// <param name="resultValue">The result of the operation, or <see langword="null"/> if execution failed.</param>
    /// <returns>
    ///  <see langword="true"/> if the operation was successfully executed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryExecuteArithmeticOverload(
        ReadOnlySpan<object?> args,
        Func<long, long, long> integerOperation,
        Func<double, double, double> realOperation,
        [NotNullWhen(true)] out object? resultValue)
    {
        if (args is [var value0, var value1])
        {
            if (TryConvertToLong(value0, out long longArg0) && TryConvertToLong(value1, out long longArg1))
            {
                resultValue = integerOperation(longArg0, longArg1);
                return true;
            }

            if (TryConvertToDouble(value0, out double doubleArg0) && TryConvertToDouble(value1, out double doubleArg1))
            {
                resultValue = realOperation(doubleArg0, doubleArg1);
                return true;
            }
        }

        resultValue = null;
        return false;
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
    internal static bool TryParseInt(string value, out int result)
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
    internal static bool TryParseLong(string value, out long result)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out result);

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
    internal static bool TryParseEnum<TEnum>(string value, out TEnum result)
        where TEnum : struct
    {
        var enumType = typeof(TEnum);

        if (!enumType.IsEnum)
        {
            result = default;
            return false;
        }

        // Reject int values for enums. In C#, this would require a cast, which is not supported in MSBuild expressions.
        if (TryParseInt(value, out _))
        {
            result = default;
            return false;
        }

        ReadOnlySpan<char> span = value.AsSpan();

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
}
