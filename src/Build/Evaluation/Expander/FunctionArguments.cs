// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Provides typed access to function arguments and materializes them on demand.
/// </summary>
internal struct FunctionArguments
{
    private static readonly object s_unmaterialized = new();

    private readonly ArgumentList _argumentList;
    private readonly string?[]? _arguments;
    private object?[]? _materializedArguments;
    private IFunctionArgumentMaterializer? _materializer;

    public FunctionArguments(string[]? arguments)
    {
        _argumentList = default;
        _arguments = arguments ?? [];
        _materializedArguments = null;
        _materializer = null;
    }

    public FunctionArguments(ArgumentList argumentList)
    {
        _argumentList = argumentList;
        _arguments = null;
        _materializedArguments = null;
        _materializer = null;
    }

    public readonly int Count => _arguments?.Length ?? _argumentList.Count;

    public readonly object? this[int index]
        => _materializedArguments is null
            ? GetSource(index).Value
            : EnsureMaterialized(index);

    [MemberNotNullWhen(true, nameof(_materializedArguments))]
    public readonly bool IsMaterialized
    {
        get
        {
            if (_materializedArguments is null)
            {
                return false;
            }

            for (int i = 0; i < _materializedArguments.Length; i++)
            {
                if (ReferenceEquals(_materializedArguments[i], s_unmaterialized))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void ConfigureMaterialization(IFunctionArgumentMaterializer materializer, bool materializeOnAccess)
    {
        _materializer = materializer;

        if (_materializedArguments is null && materializeOnAccess && Count > 0)
        {
            _materializedArguments = new object?[Count];

            for (int i = 0; i < _materializedArguments.Length; i++)
            {
                _materializedArguments[i] = s_unmaterialized;
            }
        }
    }

    public object?[] MaterializeAll()
    {
        if (_materializedArguments is null)
        {
            object?[] values = new object?[Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Materialize(i);
            }

            _materializedArguments = values;
            return values;
        }

        for (int i = 0; i < _materializedArguments.Length; i++)
        {
            EnsureMaterialized(i);
        }

        return _materializedArguments;
    }

    public readonly object?[] ToObjectArray()
    {
        if (IsMaterialized)
        {
            return _materializedArguments;
        }

        object?[] values = new object?[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = _materializedArguments is not null && !ReferenceEquals(_materializedArguments[i], s_unmaterialized)
                ? _materializedArguments[i]
                : GetSource(i).Value;
        }

        return values;
    }

    public readonly bool ContainsExpandableExpression()
    {
        if (_arguments is not null)
        {
            foreach (string? argument in _arguments)
            {
                if (argument is not null && (argument.IndexOf('$') >= 0 || argument.IndexOf('%') >= 0))
                {
                    return true;
                }
            }
        }
        else
        {
            for (int i = 0; i < _argumentList.Count; i++)
            {
                if (_argumentList.GetFlags(i) != ArgumentFlags.None)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public readonly bool TryGetArg([NotNullWhen(true)] out string? arg0)
    {
        if (Count == 1 &&
            this[0] is string value0)
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        return false;
    }

    public readonly bool TryGetArg(out int arg0)
    {
        if (Count == 1 &&
            TryConvertToInt(this[0], out arg0))
        {
            return true;
        }

        arg0 = 0;
        return false;
    }

    public readonly bool TryGetArg(out char arg0)
    {
        if (Count == 1 &&
            TryConvertToChar(this[0], out arg0))
        {
            return true;
        }

        arg0 = default;
        return false;
    }

    public readonly bool TryGetArgs([NotNullWhen(true)] out string? arg0, [NotNullWhen(true)] out string? arg1)
    {
        if (Count == 2 &&
            this[0] is string value0 &&
            this[1] is string value1)
        {
            arg0 = value0;
            arg1 = value1;
            return true;
        }

        arg0 = null;
        arg1 = null;
        return false;
    }

    public readonly bool TryGetArgs(
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1,
        [NotNullWhen(true)] out string? arg2)
    {
        if (Count == 3 &&
            this[0] is string value0 &&
            this[1] is string value1 &&
            this[2] is string value2)
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

    public readonly bool TryGetArgs(
        [NotNullWhen(true)] out string? arg0,
        [NotNullWhen(true)] out string? arg1,
        [NotNullWhen(true)] out string? arg2,
        [NotNullWhen(true)] out string? arg3)
    {
        if (Count == 4 &&
            this[0] is string value0 &&
            this[1] is string value1 &&
            this[2] is string value2 &&
            this[3] is string value3)
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

    public readonly bool TryGetArgs([NotNullWhen(true)] out string[]? args)
    {
        args = new string[Count];

        for (int i = 0; i < args.Length; i++)
        {
            if (this[i] is not string value)
            {
                args = null;
                return false;
            }

            args[i] = value;
        }

        return true;
    }

    public readonly bool TryGetArgs([NotNullWhen(true)] out string? arg0, out int arg1, out int arg2)
    {
        if (Count == 3 &&
            this[0] is string value0 &&
            this[1] is string value1 && int.TryParse(value1, out arg1) &&
            this[2] is string value2 && int.TryParse(value2, out arg2))
        {
            arg0 = value0;
            return true;
        }

        arg0 = null;
        arg1 = 0;
        arg2 = 0;
        return false;
    }

    public readonly bool TryGetArg([NotNullWhen(true)] out Version? arg0)
    {
        if (Count == 1 &&
            TryConvertToVersion(this[0], out arg0))
        {
            return true;
        }

        arg0 = null;
        return false;
    }

    public readonly bool TryGetArgs([NotNullWhen(true)] out string? arg0, out StringComparison arg1)
    {
        if (Count == 2 &&
            this[0] is string value0 &&
            this[1] is string value1)
        {
            arg0 = value0;

            // reject enums as ints. In C# this would require a cast, which is not supported in msbuild expressions
            if (!int.TryParse(value1, out _))
            {
                if (value1.IndexOf('.') >= 0)
                {
                    value1 = value1
                        .Replace("System.StringComparison.", "")
                        .Replace("StringComparison.", "");
                }

                return Enum.TryParse(value1, out arg1);
            }
        }

        arg0 = null;
        arg1 = default;
        return false;
    }

    public readonly bool TryGetArgs(out int arg0)
    {
        if (Count == 1 &&
            TryConvertToInt(this[0], out arg0))
        {
            return true;
        }

        arg0 = 0;
        return false;
    }

    public readonly bool TryGetArgs(out int arg0, out int arg1)
    {
        if (Count == 2 &&
            TryConvertToInt(this[0], out arg0) &&
            TryConvertToInt(this[1], out arg1))
        {
            return true;
        }

        arg0 = 0;
        arg1 = 0;
        return false;
    }

    public readonly bool TryGetArgs(out double arg0, out double arg1)
    {
        if (Count == 2 &&
            TryConvertToDouble(this[0], out arg0) &&
            TryConvertToDouble(this[1], out arg1))
        {
            return true;
        }

        arg0 = 0;
        arg1 = 0;
        return false;
    }

    public readonly bool TryGetArgs([NotNullWhen(true)] out string? arg0, out int arg1)
    {
        if (Count == 2 &&
            this[0] is string value1 &&
            this[1] is string value2 && int.TryParse(value2, out arg1))
        {
            arg0 = value1;
            return true;
        }

        arg0 = null;
        arg1 = 0;
        return false;
    }

    public readonly bool TryGetArgs(out int arg0, out char arg1)
    {
        if (Count == 2 &&
            TryConvertToInt(this[0], out arg0) &&
            TryConvertToChar(this[1], out arg1))
        {
            return true;
        }

        arg0 = 0;
        arg1 = default;
        return false;
    }

    public static bool TryConvertToChar(object? value, out char arg)
    {
        switch (value)
        {
            case char c:
                arg = c;
                return true;

            case string { Length: 1 } s:
                arg = s[0];
                return true;
        }

        arg = default;
        return false;
    }

    public static bool TryConvertToVersion(object? value, [NotNullWhen(true)] out Version? arg0)
    {
        if (value is string { Length: > 0 } val && Version.TryParse(val, out arg0))
        {
            return true;
        }

        arg0 = null;
        return false;
    }

    /// <summary>
    /// Try to convert value to int.
    /// </summary>
    public static bool TryConvertToInt(object? value, out int arg)
    {
        switch (value)
        {
            case double d when d is >= int.MinValue and <= int.MaxValue:
                arg = Convert.ToInt32(d);
                if (Math.Abs(arg - d) == 0)
                {
                    return true;
                }

                break;

            case long l when l is >= int.MinValue and <= int.MaxValue:
                arg = Convert.ToInt32(l);
                return true;

            case int i:
                arg = i;
                return true;

            case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                return true;
        }

        arg = 0;
        return false;
    }

    /// <summary>
    /// Try to convert value to long.
    /// </summary>
    public static bool TryConvertToLong(object? value, out long arg)
    {
        switch (value)
        {
            case double d when d is >= long.MinValue and <= long.MaxValue:
                arg = (long)d;
                if (Math.Abs(arg - d) == 0)
                {
                    return true;
                }

                break;

            case long l:
                arg = l;
                return true;

            case int i:
                arg = i;
                return true;

            case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out arg):
                return true;
        }

        arg = 0;
        return false;
    }

    /// <summary>
    /// Try to convert value to double.
    /// </summary>
    public static bool TryConvertToDouble(object? value, out double arg)
    {
        switch (value)
        {
            case double d:
                arg = d;
                return true;

            case long l:
                arg = l;
                return true;

            case int i:
                arg = i;
                return true;

            case string s when double.TryParse(s, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out arg):
                return true;

            default:
                arg = 0;
                return false;
        }
    }

    public static bool IsFloatingPointRepresentation(object? value)
        => value is double ||
            (value is string s &&
            double.TryParse(s, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out _));

    public readonly bool TryExecuteArithmeticOverload(
        Func<long, long, long> integerOperation,
        Func<double, double, double> realOperation,
        out object? resultValue)
    {
        if (Count == 2)
        {
            object? value0 = this[0];
            object? value1 = this[1];

            if (TryConvertToLong(value0, out long argLong0) &&
                TryConvertToLong(value1, out long argLong1))
            {
                resultValue = integerOperation(argLong0, argLong1);
                return true;
            }

            if (TryConvertToDouble(value0, out double argDouble0) &&
                TryConvertToDouble(value1, out double argDouble1))
            {
                resultValue = realOperation(argDouble0, argDouble1);
                return true;
            }
        }

        resultValue = null;
        return false;
    }

    private readonly object? EnsureMaterialized(int index)
    {
        Assumed.NotNull(_materializedArguments);

        object? value = _materializedArguments[index];

        if (ReferenceEquals(value, s_unmaterialized))
        {
            value = Materialize(index);
            _materializedArguments[index] = value;
        }

        return value;
    }

    private readonly object? Materialize(int index)
        => _materializer is null
            ? GetSource(index).Value
            : _materializer.Materialize(GetSource(index), index);

    private readonly StringSegment GetSource(int index)
        => _arguments is null
            ? _argumentList[index]
            : _arguments[index];
}
