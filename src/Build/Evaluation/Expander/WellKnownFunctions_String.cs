// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
#if !NET
using System.Text;
using Microsoft.Build.Framework;
#endif
using Microsoft.Build.Text;
#if NET
using Microsoft.Build.Utilities;
#endif

namespace Microsoft.Build.Evaluation.Expander;

internal partial class WellKnownFunctions
{
    private enum StringFunction : byte
    {
        None,
        Concat,
        Contains,
        Copy,
        EndsWith,
        Equals,
        Format,
        GetChars,
        GetHashCode,
        IndexOf,
        IndexOfAny,
        Insert,
        IsNullOrEmpty,
        IsNullOrWhiteSpace,
        Join,
        LastIndexOf,
        LastIndexOfAny,
        Length,
        PadLeft,
        PadRight,
        Remove,
        Replace,
        Split,
        StartsWith,
        Substring,
        ToLower,
        ToLowerInvariant,
        ToString,
        ToUpper,
        ToUpperInvariant,
        Trim,
        TrimEnd,
        TrimStart,
    }

    internal static bool TryExecuteStringFunction(
        StringSegment methodName,
        string text,
        ref FunctionArguments args,
        out object? result)
    {
        StringFunction function = GetStringFunction(methodName);
        if (args.Length == 0)
        {
            switch (function)
            {
                case StringFunction.Length:
                    result = text.Length;
                    return true;

                case StringFunction.GetHashCode:
                    result = text.GetHashCode();
                    return true;

                case StringFunction.ToString:
                    result = text;
                    return true;

                case StringFunction.ToUpperInvariant:
                    result = text.ToUpperInvariant();
                    return true;

                case StringFunction.ToLowerInvariant:
                    result = text.ToLowerInvariant();
                    return true;

                case StringFunction.ToUpper:
                    result = text.ToUpper();
                    return true;

                case StringFunction.ToLower:
                    result = text.ToLower();
                    return true;

                case StringFunction.Trim:
                    result = text.Trim();
                    return true;

                case StringFunction.TrimStart:
                    result = text.TrimStart();
                    return true;

                case StringFunction.TrimEnd:
                    result = text.TrimEnd();
                    return true;
            }
        }

        return TryExecuteStringFunctionCore(function, text, ref args, out result);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryExecuteStringFunctionCore(
        StringFunction function,
        string text,
        ref FunctionArguments args,
        out object? result)
    {
        StringSegment value = text;

        switch (function)
        {
            case StringFunction.StartsWith:
                if (args.TryGetArg(out StringSegment startsWith))
                {
                    result = value.StartsWith(startsWith, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out startsWith, out StringComparison startsWithComparison))
                {
                    result = value.StartsWith(startsWith, startsWithComparison);
                    return true;
                }

                if (args.TryGetArgs(out startsWith, out bool ignoreStartsWithCase, out CultureInfo? startsWithCulture))
                {
                    result = text.StartsWith(startsWith.ValueOrEmpty, ignoreStartsWithCase, startsWithCulture);
                    return true;
                }

                break;

            case StringFunction.EndsWith:
                if (args.TryGetArg(out StringSegment endsWith))
                {
                    result = value.EndsWith(endsWith, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out endsWith, out StringComparison endsWithComparison))
                {
                    result = value.EndsWith(endsWith, endsWithComparison);
                    return true;
                }

                if (args.TryGetArgs(out endsWith, out bool ignoreEndsWithCase, out CultureInfo? endsWithCulture))
                {
                    result = text.EndsWith(endsWith.ValueOrEmpty, ignoreEndsWithCase, endsWithCulture);
                    return true;
                }

                break;

            case StringFunction.Contains:
                if (args.TryGetArg(out StringSegment contains))
                {
                    result = value.Contains(contains);
                    return true;
                }

                if (args.TryGetArgs(out contains, out StringComparison containsComparison))
                {
                    result = value.Contains(contains, containsComparison);
                    return true;
                }

                break;

            case StringFunction.Equals:
                if (args.TryGetArg(out StringSegment equals))
                {
                    result = value.Equals(equals);
                    return true;
                }

                if (args.TryGetArgs(out equals, out StringComparison equalsComparison))
                {
                    result = value.Equals(equals, equalsComparison);
                    return true;
                }

                break;

            case StringFunction.IndexOf:
                if (args.TryGetArg(out StringSegment indexOf))
                {
                    result = value.IndexOf(indexOf, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out indexOf, out int indexOfStart))
                {
                    result = value.IndexOf(indexOf, indexOfStart, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out indexOf, out StringComparison indexOfComparison))
                {
                    result = value.IndexOf(indexOf, indexOfComparison);
                    return true;
                }

                if (args.TryGetArgs(out indexOf, out indexOfStart, out int indexOfCount))
                {
                    result = value.IndexOf(indexOf, indexOfStart, indexOfCount, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out indexOf, out indexOfStart, out StringComparison indexOfStartComparison))
                {
                    result = value.IndexOf(indexOf, indexOfStart, indexOfStartComparison);
                    return true;
                }

                if (args.TryGetArgs(out indexOf, out indexOfStart, out indexOfCount, out StringComparison indexOfCountComparison))
                {
                    result = value.IndexOf(indexOf, indexOfStart, indexOfCount, indexOfCountComparison);
                    return true;
                }

                break;

            case StringFunction.LastIndexOf:
                if (args.TryGetArg(out StringSegment lastIndexOf))
                {
                    result = value.LastIndexOf(lastIndexOf, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out lastIndexOf, out int lastIndexOfStart))
                {
                    result = value.LastIndexOf(lastIndexOf, lastIndexOfStart, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out lastIndexOf, out StringComparison lastIndexOfComparison))
                {
                    result = value.LastIndexOf(lastIndexOf, lastIndexOfComparison);
                    return true;
                }

                if (args.TryGetArgs(out lastIndexOf, out lastIndexOfStart, out int lastIndexOfCount))
                {
                    result = value.LastIndexOf(lastIndexOf, lastIndexOfStart, lastIndexOfCount, StringComparison.CurrentCulture);
                    return true;
                }

                if (args.TryGetArgs(out lastIndexOf, out lastIndexOfStart, out StringComparison lastIndexOfStartComparison))
                {
                    result = value.LastIndexOf(lastIndexOf, lastIndexOfStart, lastIndexOfStartComparison);
                    return true;
                }

                if (args.TryGetArgs(out lastIndexOf, out lastIndexOfStart, out lastIndexOfCount, out StringComparison lastIndexOfCountComparison))
                {
                    result = value.LastIndexOf(lastIndexOf, lastIndexOfStart, lastIndexOfCount, lastIndexOfCountComparison);
                    return true;
                }

                break;

            case StringFunction.IndexOfAny:
                if (args.TryGetArg(out StringSegment indexOfAny))
                {
                    result = value.IndexOfAny(indexOfAny.AsSpan());
                    return true;
                }

                break;

            case StringFunction.LastIndexOfAny:
                if (args.TryGetArg(out StringSegment lastIndexOfAny))
                {
                    result = value.LastIndexOfAny(lastIndexOfAny.AsSpan());
                    return true;
                }

                break;

            case StringFunction.Length when args.Length == 0:
                result = text.Length;
                return true;

            case StringFunction.Substring:
                if (args.TryGetArg(out int substringStart))
                {
                    result = text.Substring(substringStart);
                    return true;
                }

                if (args.TryGetArgs(out substringStart, out int substringLength))
                {
                    result = text.Substring(substringStart, substringLength);
                    return true;
                }

                break;

            case StringFunction.Insert:
                if (args.TryGetArgs(out int insertIndex, out StringSegment insertValue))
                {
                    result = Insert(value, insertIndex, insertValue);
                    return true;
                }

                break;

            case StringFunction.Remove:
                if (args.TryGetArg(out int removeStart))
                {
                    result = text.Remove(removeStart);
                    return true;
                }

                if (args.TryGetArgs(out removeStart, out int removeCount))
                {
                    result = text.Remove(removeStart, removeCount);
                    return true;
                }

                break;

            case StringFunction.Replace:
                if (args.Length == 2 && args.TryGetSegment(0, out StringSegment oldValue))
                {
                    if (args.TryGetSegment(1, out StringSegment replacement))
                    {
                        result = Replace(value, oldValue, replacement);
                        return true;
                    }
                }

                break;

            case StringFunction.Split:
                if (args.TryGetArg(out StringSegment separator) && separator.Length == 1)
                {
                    result = text.Split(separator[0]);
                    return true;
                }

                if (args.TryGetArgs(out separator, out int splitCount) && separator.Length == 1)
                {
                    result = Split(value, separator[0], splitCount);
                    return true;
                }

                break;

            case StringFunction.PadLeft:
                if (args.TryGetArg(out int leftWidth))
                {
                    result = text.PadLeft(leftWidth);
                    return true;
                }

                if (args.TryGetArgs(out leftWidth, out StringSegment leftPadding) && leftPadding.Length == 1)
                {
                    result = text.PadLeft(leftWidth, leftPadding[0]);
                    return true;
                }

                break;

            case StringFunction.PadRight:
                if (args.TryGetArg(out int rightWidth))
                {
                    result = text.PadRight(rightWidth);
                    return true;
                }

                if (args.TryGetArgs(out rightWidth, out StringSegment rightPadding) && rightPadding.Length == 1)
                {
                    result = text.PadRight(rightWidth, rightPadding[0]);
                    return true;
                }

                break;

            case StringFunction.Trim:
                if (args.Length == 0)
                {
                    result = text.Trim();
                    return true;
                }

                if (args.TryGetArg(out StringSegment trimChars) && !trimChars.IsEmpty)
                {
                    result = value.Trim(trimChars.AsSpan()).ValueOrEmpty;
                    return true;
                }

                break;

            case StringFunction.TrimStart:
                if (args.Length == 0)
                {
                    result = text.TrimStart();
                    return true;
                }

                if (args.TryGetArg(out StringSegment trimStartChars) && !trimStartChars.IsEmpty)
                {
                    result = value.TrimStart(trimStartChars.AsSpan()).ValueOrEmpty;
                    return true;
                }

                break;

            case StringFunction.TrimEnd:
                if (args.Length == 0)
                {
                    result = text.TrimEnd();
                    return true;
                }

                if (args.TryGetArg(out StringSegment trimEndChars) && !trimEndChars.IsEmpty)
                {
                    result = value.TrimEnd(trimEndChars.AsSpan()).ValueOrEmpty;
                    return true;
                }

                break;

            case StringFunction.GetChars:
                if (args.TryGetArg(out int characterIndex))
                {
                    result = text[characterIndex];
                    return true;
                }

                break;

            case StringFunction.GetHashCode when args.Length == 0:
                result = text.GetHashCode();
                return true;

            case StringFunction.ToString when args.Length == 0:
                result = text;
                return true;

            case StringFunction.ToUpperInvariant when args.Length == 0:
                result = text.ToUpperInvariant();
                return true;

            case StringFunction.ToLowerInvariant when args.Length == 0:
                result = text.ToLowerInvariant();
                return true;

            case StringFunction.ToUpper when args.Length == 0:
                result = text.ToUpper();
                return true;

            case StringFunction.ToLower when args.Length == 0:
                result = text.ToLower();
                return true;
        }

        result = null;
        return false;
    }

    internal static bool TryExecuteStaticStringFunction(
        StringSegment methodName,
        ref FunctionArguments args,
        out object? result)
    {
        switch (GetStringFunction(methodName))
        {
            case StringFunction.Concat:
                return TryExecuteStringConcat(ref args, out result);

            case StringFunction.IsNullOrWhiteSpace when args.Length == 1:
                if (args.TryGetSegment(0, out StringSegment whiteSpace))
                {
                    result = whiteSpace.IsNullOrWhiteSpace();
                    return true;
                }

                if (args.GetValue(0) is null)
                {
                    result = true;
                    return true;
                }

                break;

            case StringFunction.IsNullOrEmpty when args.Length == 1:
                if (args.TryGetSegment(0, out StringSegment empty))
                {
                    result = empty.IsNullOrEmpty;
                    return true;
                }

                if (args.GetValue(0) is null)
                {
                    result = true;
                    return true;
                }

                break;

            case StringFunction.Copy:
                if (args.TryGetArg(out StringSegment copy))
                {
                    result = copy.ValueOrEmpty;
                    return true;
                }

                break;

            case StringFunction.Equals:
                if (args.TryGetArgs(out StringSegment left, out StringSegment right))
                {
                    result = left.Equals(right);
                    return true;
                }

                if (args.Length == 3 &&
                    args.TryGetSegment(0, out left) &&
                    args.TryGetSegment(1, out right) &&
                    args.TryGetStringComparison(2, out StringComparison comparison))
                {
                    result = left.Equals(right, comparison);
                    return true;
                }

                break;

            case StringFunction.Format:
                if (args.Length is >= 2 and <= 4 && args.TryGetSegment(0, out StringSegment format))
                {
                    result = args.Length switch
                    {
                        2 => string.Format(format.ValueOrEmpty, args.GetValue(1)),
                        3 => string.Format(format.ValueOrEmpty, args.GetValue(1), args.GetValue(2)),
                        _ => string.Format(format.ValueOrEmpty, args.GetValue(1), args.GetValue(2), args.GetValue(3)),
                    };
                    return true;
                }

                break;

            case StringFunction.Join:
                if (args.Length >= 2 &&
                    args.TryGetSegment(0, out StringSegment separator) &&
                    TryJoin(separator, ref args, out string? joined))
                {
                    result = joined;
                    return true;
                }

                break;
        }

        result = null;
        return false;
    }

    private static string Insert(StringSegment value, int index, StringSegment inserted)
    {
        if ((uint)index > (uint)value.Length)
        {
            return value.ValueOrEmpty.Insert(index, inserted.ValueOrEmpty);
        }

#if NET
        return string.Concat(value.AsSpan(0, index), inserted.AsSpan(), value.AsSpan(index));
#else
        StringBuilder builder = StringBuilderCache.Acquire(value.Length + inserted.Length);
        builder.AppendSegment(value[..index]);
        builder.AppendSegment(inserted);
        builder.AppendSegment(value[index..]);
        return StringBuilderCache.GetStringAndRelease(builder);
#endif
    }

    private static string Replace(StringSegment value, StringSegment oldValue, StringSegment newValue)
    {
        if (oldValue.IsEmpty)
        {
            return value.ValueOrEmpty.Replace(oldValue.ValueOrEmpty, newValue.ValueOrEmpty);
        }

        int match = value.IndexOf(oldValue);
        if (match < 0)
        {
            return value.ValueOrEmpty;
        }

#if NET
        using ValueStringBuilder builder = new(initialCapacity: value.Length);
#else
        StringBuilder builder = StringBuilderCache.Acquire(value.Length);
#endif
        int start = 0;

        do
        {
#if NET
            builder.Append(value.AsSpan(start, match - start));
            builder.Append(newValue.AsSpan());
#else
            builder.AppendSegment(value[start..match]);
            builder.AppendSegment(newValue);
#endif
            start = match + oldValue.Length;
            match = value.IndexOf(oldValue, start);
        }
        while (match >= 0);

#if NET
        builder.Append(value.AsSpan(start));
        return builder.ToString();
#else
        builder.AppendSegment(value[start..]);
        return StringBuilderCache.GetStringAndRelease(builder);
#endif
    }

    private static string[] Split(StringSegment value, char separator, int count)
    {
        if (count < 0)
        {
            return value.ValueOrEmpty.Split([separator], count);
        }

        if (count == 0)
        {
            return [];
        }

        string[] values = new string[Math.Min(count, value.Length + 1)];
        int valueCount = 0;
        int start = 0;

        while (valueCount < count - 1)
        {
            int separatorIndex = value.IndexOf(separator, start);
            if (separatorIndex < 0)
            {
                break;
            }

            values[valueCount++] = value[start..separatorIndex].ValueOrEmpty;
            start = separatorIndex + 1;
        }

        values[valueCount++] = value[start..].ValueOrEmpty;
        if (valueCount != values.Length)
        {
            Array.Resize(ref values, valueCount);
        }

        return values;
    }

    private static bool TryJoin(
        StringSegment separator,
        ref FunctionArguments args,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? result)
    {
        for (int i = 1; i < args.Length; i++)
        {
            if (!args.TryGetSegment(i, out _))
            {
                result = null;
                return false;
            }
        }

#if NET
        using ValueStringBuilder builder = new(stackalloc char[256]);
#else
        StringBuilder builder = StringBuilderCache.Acquire();
#endif
        for (int i = 1; i < args.Length; i++)
        {
            if (i > 1)
            {
#if NET
                builder.Append(separator.AsSpan());
#else
                builder.AppendSegment(separator);
#endif
            }

            _ = args.TryGetSegment(i, out StringSegment value);
#if NET
            builder.Append(value.AsSpan());
#else
            builder.AppendSegment(value);
#endif
        }

#if NET
        result = builder.ToString();
#else
        result = StringBuilderCache.GetStringAndRelease(builder);
#endif
        return true;
    }

    private static StringFunction GetStringFunction(StringSegment name)
    {
        switch (name.Length)
        {
            case 4:
                if (name.Equals(nameof(string.Copy), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Copy;
                }

                if (name.Equals(nameof(string.Join), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Join;
                }

                if (name.Equals(nameof(string.Trim), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Trim;
                }

                break;
            case 5 when name.Equals(nameof(string.Split), StringComparison.OrdinalIgnoreCase):
                return StringFunction.Split;
            case 6:
                if (name.Equals(nameof(string.Concat), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Concat;
                }

                if (name.Equals(nameof(string.Equals), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Equals;
                }

                if (name.Equals(nameof(string.Format), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Format;
                }

                if (name.Equals(nameof(string.Insert), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Insert;
                }

                if (name.Equals(nameof(string.Length), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Length;
                }

                if (name.Equals(nameof(string.Remove), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Remove;
                }

                break;
            case 7:
                if (name.Equals(nameof(string.IndexOf), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.IndexOf;
                }

                if (name.Equals(nameof(string.PadLeft), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.PadLeft;
                }

                if (name.Equals(nameof(string.Replace), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Replace;
                }

                if (name.Equals(nameof(string.ToLower), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.ToLower;
                }

                if (name.Equals(nameof(string.ToUpper), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.ToUpper;
                }

                if (name.Equals(nameof(string.TrimEnd), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.TrimEnd;
                }

                break;
            case 8:
                if (name.Equals(nameof(string.Contains), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Contains;
                }

                if (name.Equals(nameof(string.EndsWith), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.EndsWith;
                }

                if (name.Equals(nameof(string.PadRight), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.PadRight;
                }

                if (name.Equals(nameof(string.ToString), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.ToString;
                }

                break;
            case 9:
                if (name.Equals("get_Chars", StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.GetChars;
                }

                if (name.Equals(nameof(string.Substring), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.Substring;
                }

                if (name.Equals(nameof(string.TrimStart), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.TrimStart;
                }

                break;
            case 10:
                if (name.Equals(nameof(string.IndexOfAny), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.IndexOfAny;
                }

                if (name.Equals(nameof(string.StartsWith), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.StartsWith;
                }

                break;
            case 11:
                if (name.Equals(nameof(string.GetHashCode), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.GetHashCode;
                }

                if (name.Equals(nameof(string.LastIndexOf), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.LastIndexOf;
                }

                break;
            case 13 when name.Equals(nameof(string.IsNullOrEmpty), StringComparison.OrdinalIgnoreCase):
                return StringFunction.IsNullOrEmpty;
            case 14 when name.Equals(nameof(string.LastIndexOfAny), StringComparison.OrdinalIgnoreCase):
                return StringFunction.LastIndexOfAny;
            case 16:
                if (name.Equals(nameof(string.ToLowerInvariant), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.ToLowerInvariant;
                }

                if (name.Equals(nameof(string.ToUpperInvariant), StringComparison.OrdinalIgnoreCase))
                {
                    return StringFunction.ToUpperInvariant;
                }

                break;
            case 18 when name.Equals(nameof(string.IsNullOrWhiteSpace), StringComparison.OrdinalIgnoreCase):
                return StringFunction.IsNullOrWhiteSpace;
        }

        return StringFunction.None;
    }
}
