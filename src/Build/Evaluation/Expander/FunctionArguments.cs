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

    private ImmutableArray<StringSegment> _expressions;
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

    internal void AppendExpandedValue(object? value)
    {
        int oldCount = Count;
        object?[] expandedValues = new object?[oldCount + 1];
        for (int i = 0; i < oldCount; i++)
        {
            expandedValues[i] = s_notEvaluated;
        }

        if (_expandedValues is not null)
        {
            Array.Copy(_expandedValues, expandedValues, oldCount);
        }
        else
        {
            if (_expandedIndex0PlusOne != 0)
            {
                expandedValues[_expandedIndex0PlusOne - 1] = _expandedValue0;
            }

            if (_expandedIndex1PlusOne != 0)
            {
                expandedValues[_expandedIndex1PlusOne - 1] = _expandedValue1;
            }
        }

        expandedValues[oldCount] = value;
        _expressions = _expressions.Add(default);
        _expandedValues = expandedValues;
        _expandedIndex0PlusOne = 0;
        _expandedIndex1PlusOne = 0;
        _expandedValue0 = null;
        _expandedValue1 = null;
        _materializedValues = null;
    }

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

        if (typedValue is not null)
        {
            value = null;
            return false;
        }

        value = GetString(text, index);
        return true;
    }

    private readonly string GetString(StringSegment text, int index)
    {
        string value;

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
            value = EscapingUtilities.UnescapeAll(text).Value!;
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

        return value;
    }

    internal readonly bool TryGetUnescapedText(int index, out StringSegment value)
    {
        if (TryGetValue(index, out StringSegment text, out object? typedValue)
            && typedValue is null)
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
                return TryConvertToInt32(typedValue, out value);
            }

            text = EscapingUtilities.UnescapeAll(text);
            return int.TryParse(text, out value);
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
                return TryConvertToDouble(typedValue, out value);
            }

            text = EscapingUtilities.UnescapeAll(text);
            if (long.TryParse(text, out long integerValue)
                && integerValue is >= -9_007_199_254_740_992L and <= 9_007_199_254_740_992L)
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

        value = 0;
        return false;
    }

    internal readonly bool TryGetInt64(int index, out long value)
    {
        if (TryGetValue(index, out StringSegment text, out object? typedValue))
        {
            if (typedValue is not null)
            {
                return TryConvertToInt64(typedValue, out value);
            }

            text = EscapingUtilities.UnescapeAll(text);
            return long.TryParse(text, out value);
        }

        value = 0;
        return false;
    }

    internal readonly bool TryGetChar(int index, out char value)
    {
        if (TryGetValue(index, out StringSegment text, out object? typedValue))
        {
            if (typedValue is char character)
            {
                value = character;
                return true;
            }

            if (typedValue is null)
            {
                text = EscapingUtilities.UnescapeAll(text);
                if (text.Length == 1)
                {
                    value = text[0];
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    internal readonly bool TryGetStringComparison(int index, out StringComparison value)
    {
        if (!TryGetUnescapedText(index, out StringSegment text))
        {
            value = default;
            return false;
        }

        if (TryParseStringComparison(text, out value))
        {
            return true;
        }

        // Preserve uncommon inputs accepted by the historical string-based parser.
        string stringValue = text.Value!;
        if (int.TryParse(stringValue, out _))
        {
            value = default;
            return false;
        }

        if (stringValue.IndexOf('.') >= 0)
        {
            stringValue = stringValue.Replace("System.StringComparison.", string.Empty)
                .Replace("StringComparison.", string.Empty);
        }

        return Enum.TryParse(stringValue, out value);
    }

    internal readonly bool TryGetVersion(int index, out Version? value)
    {
        if (TryGetUnescapedText(index, out StringSegment text) && !text.IsEmpty)
        {
#if NET
            return Version.TryParse(text.AsSpan(), out value);
#else
            return Version.TryParse(text.Value, out value);
#endif
        }

        value = null;
        return false;
    }

    internal readonly bool TryGetStrings(out string[] values)
    {
        values = new string[Count];
        for (int i = 0; i < values.Length; i++)
        {
            if (!TryGetString(i, out string? value) || value is null)
            {
                values = null!;
                return false;
            }

            values[i] = value;
        }

        return true;
    }

    internal readonly object? GetObject(int index)
    {
        if (!TryGetValue(index, out StringSegment text, out object? typedValue))
        {
            return null;
        }

        return typedValue is not null
            ? typedValue
            : GetString(text, index);
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

    private static bool TryParseStringComparison(StringSegment text, out StringComparison value)
    {
        const string systemPrefix = "System.StringComparison.";
        const string prefix = "StringComparison.";

        text = text.Trim();
        if (text.StartsWith(systemPrefix))
        {
            text = text.Slice(systemPrefix.Length);
        }
        else if (text.StartsWith(prefix))
        {
            text = text.Slice(prefix.Length);
        }

        if (text.Equals(nameof(StringComparison.CurrentCulture)))
        {
            value = StringComparison.CurrentCulture;
            return true;
        }

        if (text.Equals(nameof(StringComparison.CurrentCultureIgnoreCase)))
        {
            value = StringComparison.CurrentCultureIgnoreCase;
            return true;
        }

        if (text.Equals(nameof(StringComparison.InvariantCulture)))
        {
            value = StringComparison.InvariantCulture;
            return true;
        }

        if (text.Equals(nameof(StringComparison.InvariantCultureIgnoreCase)))
        {
            value = StringComparison.InvariantCultureIgnoreCase;
            return true;
        }

        if (text.Equals(nameof(StringComparison.Ordinal)))
        {
            value = StringComparison.Ordinal;
            return true;
        }

        if (text.Equals(nameof(StringComparison.OrdinalIgnoreCase)))
        {
            value = StringComparison.OrdinalIgnoreCase;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryConvertToInt32(object? value, out int result)
    {
        switch (value)
        {
            case double doubleValue when doubleValue >= int.MinValue && doubleValue <= int.MaxValue:
                result = Convert.ToInt32(doubleValue);
                if (Math.Abs(result - doubleValue) == 0)
                {
                    return true;
                }

                break;
            case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                result = Convert.ToInt32(longValue);
                return true;
            case int intValue:
                result = intValue;
                return true;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = 0;
        return false;
    }

    private static bool TryConvertToInt64(object? value, out long result)
    {
        switch (value)
        {
            case double doubleValue when doubleValue >= long.MinValue && doubleValue <= long.MaxValue:
                result = (long)doubleValue;
                if (Math.Abs(result - doubleValue) == 0)
                {
                    return true;
                }

                break;
            case long longValue:
                result = longValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result):
                return true;
        }

        result = 0;
        return false;
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double doubleValue:
                result = doubleValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case string text when double.TryParse(text, NumberStyles.Number | NumberStyles.Float, CultureInfo.InvariantCulture, out result):
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
