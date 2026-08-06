// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
/// Provides typed, allocation-conscious access to property-function arguments.
/// </summary>
/// <remarks>
/// Argument expressions remain as <see cref="StringSegment"/> instances until a caller requires a
/// <see cref="string"/> or reflection-compatible <see cref="object"/> array. Nested property expressions are
/// evaluated by the caller in source order and cached through <see cref="SetExpandedValue"/>.
/// </remarks>
internal struct FunctionArguments
{
    private static readonly object s_notEvaluated = new();

    private readonly ImmutableArray<StringSegment> _expressions;
    private readonly Type _receiverType;
    private readonly string _methodName;

    private int _expandedIndex0PlusOne;
    private int _expandedIndex1PlusOne;
    private object? _expandedValue0;
    private object? _expandedValue1;
    private object?[]? _expandedValues;
    private object[]? _materializedValues;

    internal FunctionArguments(ImmutableArray<StringSegment> expressions, Type receiverType, string methodName)
    {
        _expressions = expressions;
        _receiverType = receiverType;
        _methodName = methodName;
        _expandedIndex0PlusOne = 0;
        _expandedIndex1PlusOne = 0;
        _expandedValue0 = null;
        _expandedValue1 = null;
        _expandedValues = null;
        _materializedValues = null;
    }

    internal readonly int Count => _expressions.IsDefault ? 0 : _expressions.Length;

    internal void SetExpandedValue(int index, object? value)
    {
        if (_expandedValues is not null)
        {
            _expandedValues[index] = value;
            return;
        }

        int indexPlusOne = index + 1;
        if (_expandedIndex0PlusOne == 0 || _expandedIndex0PlusOne == indexPlusOne)
        {
            _expandedIndex0PlusOne = indexPlusOne;
            _expandedValue0 = value;
            return;
        }

        if (_expandedIndex1PlusOne == 0 || _expandedIndex1PlusOne == indexPlusOne)
        {
            _expandedIndex1PlusOne = indexPlusOne;
            _expandedValue1 = value;
            return;
        }

        _expandedValues = new object?[Count];
        for (int i = 0; i < _expandedValues.Length; i++)
        {
            _expandedValues[i] = s_notEvaluated;
        }

        _expandedValues[_expandedIndex0PlusOne - 1] = _expandedValue0;
        _expandedValues[_expandedIndex1PlusOne - 1] = _expandedValue1;
        _expandedValues[index] = value;
    }

    internal readonly bool TryGetString(int index, out string? value)
    {
        if (!TryGetValue(index, out StringSegment text, out object? typedValue))
        {
            value = null;
            return false;
        }

        if (typedValue is not null || !text.HasValue)
        {
            value = null;
            return false;
        }

        // File path normalization historically happens before unescaping.
        if (_receiverType == typeof(System.IO.File)
            || _receiverType == typeof(System.IO.Directory)
            || _receiverType == typeof(System.IO.Path))
        {
            value = FileUtilities.FixFilePath(text.Value!);
            value = EscapingUtilities.UnescapeAll(value);
        }
        else
        {
            value = EscapingUtilities.UnescapeAll(text).Value;
        }

        if ((_receiverType == typeof(System.IO.File) || _receiverType == typeof(System.IO.Directory))
            && IsFileOrDirectoryPathArgument(_methodName, index))
        {
            AbsolutePath? resolved = FileUtilities.MakeFullPathFromThreadWorkingDirectory(value!);
            if (resolved.HasValue)
            {
                value = resolved.GetValueOrDefault();
            }
        }

        return true;
    }

    internal readonly bool TryGetUnescapedText(int index, out StringSegment value)
    {
        if (TryGetValue(index, out StringSegment text, out object? typedValue)
            && typedValue is null
            && text.HasValue)
        {
            value = EscapingUtilities.UnescapeAll(text);
            return true;
        }

        value = default;
        return false;
    }

    internal readonly bool TryGetInt32(int index, out int value)
    {
        if (TryGetValue(index, out StringSegment text, out object? typedValue))
        {
            if (typedValue is not null)
            {
                return ArgumentParser.TryConvertToInt(typedValue, out value);
            }

            if (text.HasValue)
            {
                text = EscapingUtilities.UnescapeAll(text);
                if (TryParsePositiveInt32(text, out value))
                {
                    return true;
                }

#if NET
                return int.TryParse(text.AsSpan(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#else
                return int.TryParse(text.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#endif
            }
        }

        value = 0;
        return false;
    }

    internal readonly bool TryGetDouble(int index, out double value)
    {
        if (TryGetValue(index, out StringSegment text, out object? typedValue))
        {
            if (typedValue is not null)
            {
                return ArgumentParser.TryConvertToDouble(typedValue, out value);
            }

            if (text.HasValue)
            {
                text = EscapingUtilities.UnescapeAll(text);
                if (TryParsePositiveInt64(text, out long integerValue) && integerValue <= 9_007_199_254_740_992L)
                {
                    value = integerValue;
                    return true;
                }

#if NET
                return double.TryParse(text.AsSpan(), NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#else
                return double.TryParse(text.Value, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out value);
#endif
            }
        }

        value = 0;
        return false;
    }

    internal readonly object? GetObject(int index)
    {
        if (TryGetValue(index, out _, out object? typedValue) && typedValue is not null)
        {
            return typedValue;
        }

        return TryGetString(index, out string? value) ? value : null;
    }

    internal object[] MaterializeAll()
    {
        if (Count == 0)
        {
            return [];
        }

        if (_materializedValues is not null)
        {
            return _materializedValues;
        }

        object[] values = new object[Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = GetObject(i)!;
        }

        _materializedValues = values;
        return values;
    }

    private readonly bool TryGetValue(int index, out StringSegment text, out object? typedValue)
    {
        object? expandedValue;
        bool hasExpandedValue;

        if (_expandedValues is not null)
        {
            expandedValue = _expandedValues[index];
            hasExpandedValue = !ReferenceEquals(expandedValue, s_notEvaluated);
        }
        else if (_expandedIndex0PlusOne == index + 1)
        {
            expandedValue = _expandedValue0;
            hasExpandedValue = true;
        }
        else if (_expandedIndex1PlusOne == index + 1)
        {
            expandedValue = _expandedValue1;
            hasExpandedValue = true;
        }
        else
        {
            expandedValue = null;
            hasExpandedValue = false;
        }

        if (hasExpandedValue)
        {
            if (expandedValue is string expandedText)
            {
                text = expandedText;
                typedValue = null;
                return true;
            }

            text = default;
            typedValue = expandedValue;
            return expandedValue is not null;
        }

        text = _expressions[index];
        typedValue = null;
        return text.HasValue;
    }

    /// <summary>
    /// Determines whether an argument for a System.IO.File or System.IO.Directory method is a path that must
    /// be resolved against the thread-local working directory.
    /// </summary>
    private static bool IsFileOrDirectoryPathArgument(string methodName, int index)
    {
        if (index == 0)
        {
            return true;
        }

        if (index == 1)
        {
            return string.Equals(methodName, "Copy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(methodName, "Move", StringComparison.OrdinalIgnoreCase)
                || string.Equals(methodName, "Replace", StringComparison.OrdinalIgnoreCase);
        }

        return index == 2 && string.Equals(methodName, "Replace", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParsePositiveInt32(StringSegment value, out int result)
    {
        if (value.IsEmpty)
        {
            result = 0;
            return false;
        }

        uint parsed = 0;
        for (int i = 0; i < value.Length; i++)
        {
            uint digit = (uint)(value[i] - '0');
            if (digit > 9 || parsed > (int.MaxValue - digit) / 10)
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digit;
        }

        result = (int)parsed;
        return true;
    }

    private static bool TryParsePositiveInt64(StringSegment value, out long result)
    {
        if (value.IsEmpty)
        {
            result = 0;
            return false;
        }

        ulong parsed = 0;
        for (int i = 0; i < value.Length; i++)
        {
            ulong digit = (uint)(value[i] - '0');
            if (digit > 9 || parsed > ((ulong)long.MaxValue - digit) / 10)
            {
                result = 0;
                return false;
            }

            parsed = (parsed * 10) + digit;
        }

        result = (long)parsed;
        return true;
    }
}
