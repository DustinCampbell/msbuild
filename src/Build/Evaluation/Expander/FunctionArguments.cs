// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Holds source argument segments and their expanded values.
/// </summary>
internal struct FunctionArguments
{
    private static readonly object s_source = new();
    private static readonly object s_requiresMaterialization = new();

    private readonly ArgumentList _source;
    private readonly string[]? _sourceStrings;
    private object?[]? _materialized;
    private IFunctionArgumentMaterializer? _materializer;

    public FunctionArguments(ArgumentList source)
    {
        _source = source;
        _sourceStrings = null;
        _materialized = null;
        _materializer = null;
    }

    public FunctionArguments(string[]? values)
    {
        _source = default;
        _sourceStrings = values ?? [];
        _materialized = null;
        _materializer = null;
    }

    public readonly int Count
        => _materialized?.Length ?? _sourceStrings?.Length ?? _source.Count;

    public readonly int Length
        => Count;

    public readonly object? this[int index]
        => GetValue(index);

    public readonly bool IsMaterialized
    {
        get
        {
            if (_materialized is null)
            {
                return false;
            }

            for (int i = 0; i < _materialized.Length; i++)
            {
                if (IsPending(_materialized[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly StringSegment GetSource(int index)
        => _sourceStrings is null
            ? _source[index]
            : _sourceStrings[index];

    public void ConfigureMaterialization(IFunctionArgumentMaterializer materializer, bool materializeAllArguments)
    {
        _materializer = materializer;

        if (_materialized is null
            && Count > 0
            && (materializeAllArguments || ContainsMaterializationRequirement()))
        {
            InitializeMaterializedValues(materializeAllArguments);
        }
    }

    public void ClearMaterializer()
        => _materializer = null;

    public object?[] MaterializeAll()
    {
        if (_materialized is null)
        {
            object?[] materializedValues = new object?[Count];
            for (int i = 0; i < materializedValues.Length; i++)
            {
                materializedValues[i] = Materialize(i, useMaterializer: true);
            }

            _materialized = materializedValues;
            return materializedValues;
        }

        object?[] values = _materialized;
        for (int i = 0; i < values.Length; i++)
        {
            if (IsPending(values[i]))
            {
                values[i] = Materialize(i, useMaterializer: true);
            }
        }

        return values;
    }

    public readonly object?[] ToObjectArray()
    {
        if (IsMaterialized)
        {
            return _materialized!;
        }

        object?[] values = new object?[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = _materialized is not null && !IsPending(_materialized[i])
                ? _materialized[i]
                : GetSource(i).Value;
        }

        return values;
    }

    /// <summary>
    ///  Determines whether any pending argument requires expansion or unescaping.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> when at least one pending argument requires materialization; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public readonly bool ContainsMaterializationRequirement()
    {
        for (int i = 0; i < Count; i++)
        {
            if (_materialized is not null
                && !ReferenceEquals(_materialized[i], s_requiresMaterialization))
            {
                continue;
            }

            if (GetRequirements(i) != FunctionArgumentRequirements.None)
            {
                return true;
            }
        }

        return false;
    }

    public readonly bool TryGetArg(out string? arg0)
    {
        arg0 = null;

        return Count == 1
            && TryGetString(0, out arg0);
    }

    public readonly bool TryGetArg(out StringSegment arg0)
    {
        arg0 = default;

        return Count == 1
            && TryGetSegment(0, out arg0);
    }

    public readonly bool TryGetArgs(out string? arg0, out string? arg1)
    {
        arg0 = null;
        arg1 = null;

        return Count == 2
            && TryGetString(0, out arg0)
            && TryGetString(1, out arg1);
    }

    public readonly bool TryGetArgs(out StringSegment arg0, out StringSegment arg1)
    {
        arg0 = default;
        arg1 = default;

        return Count == 2
            && TryGetSegment(0, out arg0)
            && TryGetSegment(1, out arg1);
    }

    public readonly bool TryGetArgs(out string? arg0, out string? arg1, out string? arg2)
    {
        arg0 = null;
        arg1 = null;
        arg2 = null;

        return Count == 3
            && TryGetString(0, out arg0)
            && TryGetString(1, out arg1)
            && TryGetString(2, out arg2);
    }

    public readonly bool TryGetArgs(
        out string? arg0,
        out string? arg1,
        out string? arg2,
        out string? arg3)
    {
        arg0 = null;
        arg1 = null;
        arg2 = null;
        arg3 = null;

        return Count == 4
            && TryGetString(0, out arg0)
            && TryGetString(1, out arg1)
            && TryGetString(2, out arg2)
            && TryGetString(3, out arg3);
    }

    public readonly bool TryGetArg(out int arg0)
    {
        if (Count != 1)
        {
            arg0 = 0;
            return false;
        }

        return TryConvertToInt(0, out arg0);
    }

    public readonly bool TryGetArgs(out int arg0, out int arg1)
    {
        arg0 = 0;
        arg1 = 0;

        return Count == 2
            && TryConvertToInt(0, out arg0)
            && TryConvertToInt(1, out arg1);
    }

    public readonly bool TryGetArgs(out double arg0, out double arg1)
    {
        arg0 = 0;
        arg1 = 0;

        return Count == 2
            && TryConvertToDouble(0, out arg0)
            && TryConvertToDouble(1, out arg1);
    }

    public readonly bool TryGetArgs(out int arg0, out string? arg1)
    {
        arg0 = 0;
        arg1 = null;

        if (Count != 2)
        {
            return false;
        }

        if (!TryConvertToInt(0, out arg0))
        {
            return false;
        }

        if (_materialized is not null && EnsureMaterialized(1) is char ch)
        {
            arg1 = ch.ToString();
            return true;
        }

        return TryGetString(1, out arg1);
    }

    public readonly bool TryGetArgs(out int arg0, out StringSegment arg1)
    {
        arg0 = 0;
        arg1 = default;

        return Count == 2
            && TryConvertToInt(0, out arg0)
            && TryGetSegment(1, out arg1);
    }

    public readonly bool TryGetArgs(out string? arg0, out int arg1)
    {
        arg0 = null;
        arg1 = 0;

        return Count == 2
            && TryGetString(0, out arg0)
            && TryConvertToInt(1, out arg1);
    }

    public readonly bool TryGetArgs(out StringSegment arg0, out int arg1)
    {
        arg0 = default;
        arg1 = 0;

        return Count == 2
            && TryGetSegment(0, out arg0)
            && TryConvertToInt(1, out arg1);
    }

    public readonly bool TryGetArgs(out string? arg0, out int arg1, out int arg2)
    {
        arg0 = null;
        arg1 = 0;
        arg2 = 0;

        return Count == 3
            && TryGetString(0, out arg0)
            && TryConvertToInt(1, out arg1)
            && TryConvertToInt(2, out arg2);
    }

    public readonly bool TryGetArg(out Version? arg0)
    {
        arg0 = null;

        return Count == 1
            && TryGetString(0, out string? value)
            && !value.IsNullOrEmpty()
            && Version.TryParse(value, out arg0);
    }

    public readonly bool TryGetArgs(out StringSegment arg0, out StringComparison arg1)
    {
        arg0 = default;
        arg1 = default;

        return Count == 2
            && TryGetSegment(0, out arg0)
            && TryGetStringComparison(1, out arg1);
    }

    public readonly bool TryExecuteArithmeticOverload(
        Func<long, long, long> integerOperation,
        Func<double, double, double> realOperation,
        out object? result)
    {
        if (Count != 2)
        {
            result = null;
            return false;
        }

        if (TryConvertToLong(0, out long argLong0) && TryConvertToLong(1, out long argLong1))
        {
            result = integerOperation(argLong0, argLong1);
            return true;
        }

        if (TryConvertToDouble(0, out double argDouble0) && TryConvertToDouble(1, out double argDouble1))
        {
            result = realOperation(argDouble0, argDouble1);
            return true;
        }

        result = null;
        return false;
    }

    public static bool TryConvertToInt(object? value, out int result)
    {
        switch (value)
        {
            case double d when d is >= int.MinValue and <= int.MaxValue:
                result = Convert.ToInt32(d);
                if (Math.Abs(result - d) == 0)
                {
                    return true;
                }

                break;

            case long l when l is >= int.MinValue and <= int.MaxValue:
                result = Convert.ToInt32(l);
                return true;

            case int i:
                result = i;
                return true;

            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out result):
                return true;
        }

        result = 0;
        return false;
    }

    public static bool TryConvertToLong(object? value, out long result)
    {
        switch (value)
        {
            case double d when d is >= long.MinValue and <= long.MaxValue:
                result = (long)d;
                if (Math.Abs(result - d) == 0)
                {
                    return true;
                }

                break;

            case long l:
                result = l;
                return true;

            case int i:
                result = i;
                return true;

            case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out result):
                return true;
        }

        result = 0;
        return false;
    }

    public static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d:
                result = d;
                return true;

            case long l:
                result = l;
                return true;

            case int i:
                result = i;
                return true;

            case string s when double.TryParse(s, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out result):
                return true;

            default:
                result = 0;
                return false;
        }
    }

    public static bool IsFloatingPointRepresentation(object? value)
        => value is double
        || (value is string text
            && double.TryParse(
                text,
                NumberStyles.Number | NumberStyles.Float,
                CultureInfo.InvariantCulture.NumberFormat,
                out _));

    private readonly bool TryGetString(int index, out string? value)
    {
        if (_materialized is not null)
        {
            value = EnsureMaterialized(index) as string;
            return value is not null;
        }

        StringSegment segment = GetSource(index);
        value = segment.Value;
        return segment.HasValue;
    }

    private readonly bool TryGetSegment(int index, out StringSegment value)
    {
        if (_materialized is null)
        {
            value = GetSource(index);
            return value.HasValue;
        }

        object? storedValue = _materialized[index];
        if (ReferenceEquals(storedValue, s_source))
        {
            value = GetSource(index);
            return value.HasValue;
        }

        if (EnsureMaterialized(index) is string text)
        {
            value = text;
            return true;
        }

        value = default;
        return false;
    }

    private readonly bool TryGetStringComparison(int index, out StringComparison result)
    {
        result = default;

        if (!TryGetString(index, out string? comparisonTypeName)
            || comparisonTypeName is null
            || int.TryParse(comparisonTypeName, out _))
        {
            return false;
        }

        if (comparisonTypeName.IndexOf('.') >= 0)
        {
            comparisonTypeName = comparisonTypeName
                .Replace("System.StringComparison.", string.Empty)
                .Replace("StringComparison.", string.Empty);
        }

        return Enum.TryParse(comparisonTypeName, out result);
    }

    private readonly bool TryConvertToInt(int index, out int result)
        => _materialized is null || ReferenceEquals(_materialized[index], s_source)
            ? int.TryParse(GetSource(index), out result)
            : TryConvertToInt(EnsureMaterialized(index), out result);

    private readonly bool TryConvertToLong(int index, out long result)
        => _materialized is null || ReferenceEquals(_materialized[index], s_source)
            ? long.TryParse(GetSource(index), out result)
            : TryConvertToLong(EnsureMaterialized(index), out result);

    private readonly bool TryConvertToDouble(int index, out double result)
        => _materialized is null || ReferenceEquals(_materialized[index], s_source)
            ? double.TryParse(
                GetSource(index).Value,
                NumberStyles.Number | NumberStyles.Float,
                CultureInfo.InvariantCulture.NumberFormat,
                out result)
            : TryConvertToDouble(EnsureMaterialized(index), out result);

    private readonly object? GetValue(int index)
        => _materialized is null
            ? GetSource(index).Value
            : EnsureMaterialized(index);

    private readonly object? EnsureMaterialized(int index)
    {
        object?[] values = _materialized!;
        object? value = values[index];
        if (IsPending(value))
        {
            value = Materialize(index, useMaterializer: ReferenceEquals(value, s_requiresMaterialization));
            values[index] = value;
        }

        return value;
    }

    private readonly object? Materialize(int index, bool useMaterializer)
    {
        StringSegment source = GetSource(index);
        return !useMaterializer || _materializer is null
            ? source.Value
            : _materializer.Materialize(source, index, GetRequirements(index));
    }

    private void InitializeMaterializedValues(bool materializeAllArguments)
        => _materialized = CreateMaterializedValues(materializeAllArguments);

    private readonly object?[] CreateMaterializedValues(bool materializeAllArguments)
    {
        object?[] values = new object?[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = materializeAllArguments
                || GetRequirements(i) != FunctionArgumentRequirements.None
                ? s_requiresMaterialization
                : s_source;
        }

        return values;
    }

    private readonly FunctionArgumentRequirements GetRequirements(int index)
    {
        if (_sourceStrings is null)
        {
            return _source.GetRequirements(index);
        }

        return PropertyFunctionArgument.GetRequirements(_sourceStrings[index]);
    }

    private static bool IsPending(object? value)
        => ReferenceEquals(value, s_source)
        || ReferenceEquals(value, s_requiresMaterialization);
}
