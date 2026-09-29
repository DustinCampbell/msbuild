// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides indexed, typed access to evaluated property-function arguments.
/// </summary>
/// <param name="values">The evaluated argument values.</param>
/// <remarks>
///  The current implementation is a readonly view over an evaluated array. It is passed by reference through
///  well-known function dispatch so it can later cache lazily evaluated values without changing the dispatch API.
/// </remarks>
internal readonly struct Arguments(object?[] values)
{
    private readonly object?[] _values = values;

    /// <summary>
    ///  Gets the number of arguments.
    /// </summary>
    public int Length => _values.Length;

    /// <summary>
    ///  Returns the evaluated arguments as an object array.
    /// </summary>
    /// <returns>
    ///  The array containing the evaluated argument values.
    /// </returns>
    /// <remarks>
    ///  No copy is made. Changes to the returned array are visible through this <see cref="Arguments"/> instance.
    /// </remarks>
    public object?[] ToObjectArray()
        => _values;

    /// <summary>
    ///  Attempts to get the argument at the specified index.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The argument value if the index is valid; otherwise, <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="index"/> is valid; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, out object? result)
    {
        if (index >= 0 && index < _values.Length)
        {
            result = _values[index];
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as a character.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The converted character if successful; otherwise, the default character.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument can be converted to a character; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, out char result)
    {
        if (TryGetArg(index, out object? value))
        {
            return ArgumentParser.TryConvertToChar(value, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as a double-precision number.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The converted number if successful; otherwise, the default value.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument can be converted to a double-precision number; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, out double result)
    {
        if (TryGetArg(index, out object? value))
        {
            return ArgumentParser.TryConvertToDouble(value, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as an enumeration value.
    /// </summary>
    /// <typeparam name="T">The enumeration type.</typeparam>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The converted enumeration value if successful; otherwise, the default value.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument can be converted to <typeparamref name="T"/>; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool TryGetArg<T>(int index, out T result)
        where T : struct, Enum
    {
        if (TryGetArg(index, out object? value))
        {
            return ArgumentParser.TryConvertToEnum(value, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as a 32-bit integer.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The converted integer if successful; otherwise, the default value.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument can be converted to a 32-bit integer; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, out int result)
    {
        if (TryGetArg(index, out object? value))
        {
            return ArgumentParser.TryConvertToInt(value, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as a 64-bit integer.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The converted integer if successful; otherwise, the default value.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument can be converted to a 64-bit integer; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, out long result)
    {
        if (TryGetArg(index, out object? value))
        {
            return ArgumentParser.TryConvertToLong(value, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as a string.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The string if successful; otherwise, <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument is a string; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, [NotNullWhen(true)] out string? result)
    {
        if (TryGetArg(index, out object? value) && value is string s)
        {
            result = s;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to get the argument at the specified index as a version.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="result">The converted version if successful; otherwise, <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if the argument can be converted to a version; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetArg(int index, [NotNullWhen(true)] out Version? result)
    {
        if (TryGetArg(index, out object? value))
        {
            return ArgumentParser.TryConvertToVersion(value, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    ///  Attempts to interpret the arguments using the intrinsic arithmetic overload-selection rules.
    /// </summary>
    /// <param name="result">The converted arithmetic arguments if successful; otherwise, the default value.</param>
    /// <returns>
    ///  <see langword="true"/> if the arguments can be converted to a supported arithmetic pair; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public bool TryGetArithmeticArgs(out ArithmeticArguments result)
        => ArgumentParser.TryGetArithmeticArguments(_values, out result);

    /// <summary>
    ///  Attempts to copy the arguments to a string array.
    /// </summary>
    /// <param name="result">The copied string array if every argument is a string; otherwise, <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if every argument is a string; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryConvertToStrings([NotNullWhen(true)] out string[]? result)
        => ArgumentParser.TryConvertToStrings(_values, out result);

    /// <summary>
    ///  Attempts to get a segment containing the arguments from the specified index through the end.
    /// </summary>
    /// <param name="index">The zero-based index at which the segment begins.</param>
    /// <param name="result">The requested segment if the index is valid; otherwise, the default segment.</param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="index"/> is valid; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetArraySegment(int index, out ArraySegment<object?> result)
        => TryGetArraySegment(index, Length - index, out result);

    /// <summary>
    ///  Attempts to get a segment containing the specified range of arguments.
    /// </summary>
    /// <param name="index">The zero-based index at which the segment begins.</param>
    /// <param name="length">The number of arguments in the segment.</param>
    /// <param name="result">The requested segment if the range is valid; otherwise, the default segment.</param>
    /// <returns>
    ///  <see langword="true"/> if the requested range is valid; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetArraySegment(int index, int length, out ArraySegment<object?> result)
    {
        if (index < 0 || length < 0 || index + length > _values.Length)
        {
            result = default;
            return false;
        }

        result = new ArraySegment<object?>(_values, index, length);
        return true;
    }
}
