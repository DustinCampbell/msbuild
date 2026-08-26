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
    private readonly ArgumentList _source;
    private readonly string[]? _sourceStrings;
    private object[]? _materialized;

    public FunctionArguments(ArgumentList source)
    {
        _source = source;
        _sourceStrings = null;
        _materialized = null;
    }

    public FunctionArguments(string[]? values)
    {
        _source = default;
        _sourceStrings = values ?? [];
        _materialized = null;
    }

    public readonly int Count
        => _materialized?.Length ?? _sourceStrings?.Length ?? _source.Count;

    public readonly int Length
        => Count;

    public readonly object? this[int index]
        => _materialized is null
            ? GetSource(index).Value
            : _materialized[index];

    public readonly bool IsMaterialized
        => _materialized is not null;

    public readonly StringSegment GetSource(int index)
        => _sourceStrings is null
            ? _source[index]
            : _sourceStrings[index];

    public void SetMaterialized(object[] values)
    {
        Assumed.GreaterThanOrEqual(values.Length, Count);
        _materialized = values;
    }

    public readonly object[] ToObjectArray()
    {
        if (_materialized is not null)
        {
            return _materialized;
        }

        object[] values = new object[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = GetSource(i).Value!;
        }

        return values;
    }

    public readonly bool ContainsExpandableExpression()
    {
        if (_materialized is not null)
        {
            return false;
        }

        for (int i = 0; i < Count; i++)
        {
            StringSegment argument = GetSource(i);
            if (argument.HasValue && argument.ContainsAny('$', '%'))
            {
                return true;
            }
        }

        return false;
    }

    public readonly bool TryGetArg(out string? arg0)
    {
        if (Count != 1)
        {
            arg0 = null;
            return false;
        }

        return TryGetString(0, out arg0);
    }

    public readonly bool TryGetArg(out StringSegment arg0)
    {
        if (Count != 1)
        {
            arg0 = default;
            return false;
        }

        return TryGetSegment(0, out arg0);
    }

    public readonly bool TryGetArgs(out string? arg0, out string? arg1)
    {
        arg0 = null;
        arg1 = null;

        if (Count != 2)
        {
            return false;
        }

        return TryGetString(0, out arg0)
            && TryGetString(1, out arg1);
    }

    public readonly bool TryGetArgs(out StringSegment arg0, out StringSegment arg1)
    {
        arg0 = default;
        arg1 = default;

        if (Count != 2)
        {
            return false;
        }

        return TryGetSegment(0, out arg0)
            && TryGetSegment(1, out arg1);
    }

    public readonly bool TryGetArgs(out string? arg0, out string? arg1, out string? arg2)
    {
        arg0 = null;
        arg1 = null;
        arg2 = null;

        if (Count != 3)
        {
            return false;
        }

        return TryGetString(0, out arg0)
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

        if (Count != 4)
        {
            return false;
        }

        return TryGetString(0, out arg0)
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

    public readonly bool TryGetArgs(out int arg0)
        => TryGetArg(out arg0);

    public readonly bool TryGetArgs(out int arg0, out int arg1)
    {
        arg0 = 0;
        arg1 = 0;

        if (Count != 2)
        {
            return false;
        }

        return TryConvertToInt(0, out arg0)
            && TryConvertToInt(1, out arg1);
    }

    public readonly bool TryGetArgs(out double arg0, out double arg1)
    {
        arg0 = 0;
        arg1 = 0;

        if (Count != 2)
        {
            return false;
        }

        return TryConvertToDouble(0, out arg0)
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

        if (_materialized is not null && _materialized[1] is char ch)
        {
            arg1 = ch.ToString();
            return TryConvertToInt(0, out arg0);
        }

        return TryConvertToInt(0, out arg0)
            && TryGetString(1, out arg1);
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

        if (Count != 2)
        {
            return false;
        }

        return TryGetString(0, out arg0)
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

        if (Count != 3)
        {
            return false;
        }

        return TryGetString(0, out arg0)
            && TryConvertToInt(1, out arg1)
            && TryConvertToInt(2, out arg2);
    }

    public readonly bool TryGetArg(out Version? arg0)
    {
        if (Count != 1)
        {
            arg0 = null;
            return false;
        }

        if (!TryGetString(0, out string? value)
            || string.IsNullOrEmpty(value)
            || !Version.TryParse(value, out arg0))
        {
            arg0 = null;
            return false;
        }

        return true;
    }

    public readonly bool TryGetArgs(out string? arg0, out StringComparison arg1)
    {
        arg0 = null;
        arg1 = default;
        return Count == 2
            && TryGetString(0, out arg0)
            && TryGetStringComparison(1, out arg1);
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
        out object? resultValue)
    {
        resultValue = null;

        if (Count != 2)
        {
            return false;
        }

        if (TryConvertToLong(0, out long argLong0) && TryConvertToLong(1, out long argLong1))
        {
            resultValue = integerOperation(argLong0, argLong1);
            return true;
        }

        if (TryConvertToDouble(0, out double argDouble0) && TryConvertToDouble(1, out double argDouble1))
        {
            resultValue = realOperation(argDouble0, argDouble1);
            return true;
        }

        return false;
    }

    public readonly bool ElementsAre(Type type)
    {
        if (_materialized is null)
        {
            if (type != typeof(string))
            {
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                if (!GetSource(i).HasValue)
                {
                    return false;
                }
            }

            return true;
        }

        for (int i = 0; i < _materialized.Length; i++)
        {
            if (_materialized[i]?.GetType() != type)
            {
                return false;
            }
        }

        return true;
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

    public static bool IsFloatingPointRepresentation(object value)
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
            value = _materialized[index] as string;
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

        if (_materialized[index] is string text)
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
        => _materialized is null
            ? int.TryParse(GetSource(index), out result)
            : TryConvertToInt(_materialized[index], out result);

    private readonly bool TryConvertToLong(int index, out long result)
        => _materialized is null
            ? long.TryParse(GetSource(index), out result)
            : TryConvertToLong(_materialized[index], out result);

    private readonly bool TryConvertToDouble(int index, out double result)
        => _materialized is null
            ? double.TryParse(
                GetSource(index).Value,
                NumberStyles.Number | NumberStyles.Float,
                CultureInfo.InvariantCulture.NumberFormat,
                out result)
            : TryConvertToDouble(_materialized[index], out result);
}
