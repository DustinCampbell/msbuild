// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Stores function arguments and materializes their values on demand.
/// </summary>
internal struct FunctionArguments
{
    private static readonly object s_unmaterialized = new();

    private readonly string?[] _arguments;
    private object?[]? _materializedArguments;
    private IFunctionArgumentMaterializer? _materializer;

    public FunctionArguments(string[]? arguments)
    {
        _arguments = arguments ?? [];
        _materializedArguments = null;
        _materializer = null;
    }

    public readonly int Count => _arguments.Length;

    /// <summary>
    ///  Sets the service used to materialize source arguments when their values are requested.
    /// </summary>
    /// <param name="materializer">The argument materializer.</param>
    public void SetMaterializer(IFunctionArgumentMaterializer materializer)
    {
        _materializer = materializer;
    }

    /// <summary>
    ///  Gets the value at <paramref name="index"/>, materializing and caching it when necessary.
    /// </summary>
    /// <param name="index">The zero-based argument index.</param>
    /// <returns>
    ///  The source argument or its materialized value.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? GetValue(int index)
    {
        object?[]? materializedArguments = _materializedArguments;
        if (materializedArguments is not null)
        {
            object? value = materializedArguments[index];
            if (!ReferenceEquals(value, s_unmaterialized))
            {
                return value;
            }
        }

        string? source = _arguments[index];
        if (_materializer is null)
        {
            return source;
        }

        materializedArguments ??= InitializeMaterializedArguments();
        object? materializedValue = _materializer.Materialize(source, index);
        materializedArguments[index] = materializedValue;
        return materializedValue;
    }

    /// <summary>
    ///  Materializes every argument and returns the cached values.
    /// </summary>
    /// <returns>
    ///  The materialized argument values.
    /// </returns>
    public object?[] MaterializeAll()
    {
        if (_materializedArguments is null)
        {
            object?[] values = new object?[Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = _materializer is null
                    ? _arguments[i]
                    : _materializer.Materialize(_arguments[i], i);
            }

            _materializedArguments = values;
            return values;
        }

        for (int i = 0; i < _materializedArguments.Length; i++)
        {
            if (ReferenceEquals(_materializedArguments[i], s_unmaterialized))
            {
                _materializedArguments[i] = _materializer!.Materialize(_arguments[i], i);
            }
        }

        return _materializedArguments;
    }

    /// <summary>
    ///  Returns the currently available values without forcing unmaterialized arguments to be materialized.
    /// </summary>
    /// <returns>
    ///  A snapshot containing cached values where available and source arguments otherwise.
    /// </returns>
    public readonly object?[] SnapshotValues()
    {
        if (_materializedArguments is not null)
        {
            bool containsUnmaterializedValue = false;
            for (int i = 0; i < _materializedArguments.Length; i++)
            {
                if (ReferenceEquals(_materializedArguments[i], s_unmaterialized))
                {
                    containsUnmaterializedValue = true;
                    break;
                }
            }

            if (!containsUnmaterializedValue)
            {
                return _materializedArguments;
            }
        }

        object?[] values = new object?[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = _materializedArguments is not null &&
                !ReferenceEquals(_materializedArguments[i], s_unmaterialized)
                    ? _materializedArguments[i]
                    : _arguments[i];
        }

        return values;
    }

    public readonly bool ContainsExpandableExpression()
    {
        foreach (string? argument in _arguments)
        {
            if (argument is not null && (argument.IndexOf('$') >= 0 || argument.IndexOf('%') >= 0))
            {
                return true;
            }
        }

        return false;
    }

    private object?[] InitializeMaterializedArguments()
    {
        object?[] values = new object?[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = s_unmaterialized;
        }

        _materializedArguments = values;
        return values;
    }
}
