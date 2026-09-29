// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides indexed, typed access to property-function arguments.
/// </summary>
/// <remarks>
///  Arguments supplied as source text are materialized and cached on first access. The type is passed by reference
///  through well-known function dispatch so changes to its lazy cache are preserved.
/// </remarks>
internal struct Arguments
{
    private static readonly object s_notMaterialized = new();

    private readonly string[]? _sourceValues;
    private readonly int _count;
    private readonly IArgumentMaterializer? _materializer;
    private readonly ExpanderOptions _options;
    private readonly ExpanderContext _context;

    private object?[]? _values;
    private int _materialized;

    /// <summary>
    ///  Initializes a new instance of the <see cref="Arguments"/> struct
    ///  with unevaluated argument text.
    /// </summary>
    /// <param name="sourceValues">The unevaluated argument text.</param>
    /// <param name="materializer">The materializer used to evaluate argument values.</param>
    /// <param name="options">The options controlling expansion.</param>
    /// <param name="context">The context in which arguments are expanded.</param>
    public Arguments(
        string[] sourceValues,
        IArgumentMaterializer materializer,
        ExpanderOptions options,
        ref readonly ExpanderContext context)
    {
        _sourceValues = sourceValues;
        _count = sourceValues.Length;
        _materializer = materializer;
        _options = options;
        _context = context;
        _values = sourceValues.Length == 0 ? [] : null;
        _materialized = 0;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="Arguments"/> struct
    ///  with already evaluated values.
    /// </summary>
    /// <param name="values">The evaluated argument values.</param>
    public Arguments(object?[] values)
    {
        _count = values.Length;
        _values = values;
        _materialized = values.Length;
    }

    /// <summary>
    ///  Gets the number of arguments.
    /// </summary>
    public readonly int Length => _count;

    /// <summary>
    ///  Gets a value indicating whether all arguments have been materialized.
    /// </summary>
    [MemberNotNullWhen(true, nameof(_values))]
    public readonly bool AllMaterialized => _values is not null && _materialized == _count;

    private object?[] GetOrCreateValues()
    {
        if (_values is null)
        {
            _values = new object?[_count];

            for (int i = 0; i < _values.Length; i++)
            {
                _values[i] = s_notMaterialized;
            }
        }

        return _values;
    }

    private object? GetMaterializedArg(int index)
    {
        object?[] values = GetOrCreateValues();
        object? value = values[index];

        if (_materialized < values.Length)
        {
            Assumed.NotNull(_sourceValues);
            Assumed.NotNull(_materializer);

            if (ReferenceEquals(value, s_notMaterialized))
            {
                value = _materializer.MaterializeArgument(_sourceValues[index], index, _options, in _context);
                values[index] = value;
                _materialized++;
            }
        }

        return value;
    }

    /// <summary>
    ///  Materializes all remaining arguments and returns them as an object array.
    /// </summary>
    /// <returns>
    ///  The array containing the evaluated argument values.
    /// </returns>
    /// <remarks>
    ///  Arguments are materialized in index order. No copy is made; changes to the returned array are visible
    ///  through this <see cref="Arguments"/> instance.
    /// </remarks>
    public object?[] ToObjectArray()
    {
        if (AllMaterialized)
        {
            return _values;
        }

        object?[] values = GetOrCreateValues();

        if (_materialized < values.Length)
        {
            for (int i = 0; i < values.Length; i++)
            {
                _ = GetMaterializedArg(i);
            }
        }

        return values;
    }

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
        if (index >= 0 && index < Length)
        {
            result = GetMaterializedArg(index);
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
        => ArgumentParser.TryGetArithmeticArguments(ToObjectArray(), out result);

    /// <summary>
    ///  Attempts to copy the arguments to a string array.
    /// </summary>
    /// <param name="result">The copied string array if every argument is a string; otherwise, <see langword="null"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if every argument is a string; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryConvertToStrings([NotNullWhen(true)] out string[]? result)
        => ArgumentParser.TryConvertToStrings(ToObjectArray(), out result);

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
        if (index < 0 || length < 0 || index + length > Length)
        {
            result = default;
            return false;
        }

        result = new ArraySegment<object?>(ToObjectArray(), index, length);
        return true;
    }
}
