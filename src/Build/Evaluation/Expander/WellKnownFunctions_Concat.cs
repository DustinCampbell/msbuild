// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
    internal static bool TryExecuteStringConcat(ref FunctionArguments args, out object? result)
    {
        // Type.DefaultBinder reports an ambiguous match for the zero-argument call. Preserve that behavior by
        // leaving it to reflection.
        if (args.Length == 0)
        {
            result = null;
            return false;
        }

        if (args.Length == 1)
        {
            return TryExecuteSingleStringConcatArgument(ref args, out result);
        }

        if (TryConcatSegments(ref args, out result))
        {
            return true;
        }

        if (args.Length == 2)
        {
            result = string.Concat(args.GetValue(0), args.GetValue(1));
            return true;
        }

        if (args.Length == 3)
        {
            result = string.Concat(args.GetValue(0), args.GetValue(1), args.GetValue(2));
            return true;
        }

        result = ConcatValues(ref args);
        return true;
    }

    private static bool TryExecuteSingleStringConcatArgument(ref FunctionArguments args, out object? result)
    {
        if (args.TryGetSegment(0, out StringSegment segment))
        {
            result = segment.ValueOrEmpty;
            return true;
        }

        object? value = args.GetValue(0);
        if (value is null)
        {
            // Type.DefaultBinder considers an untyped null ambiguous. Treat it as the object overload instead:
            // String.Concat(object?) defines null as contributing an empty string.
            result = string.Empty;
            return true;
        }

        result = value switch
        {
            string?[] strings => string.Concat(strings),
            object?[] objects => string.Concat(objects),
            IEnumerable<string?> strings => string.Concat(strings),

            // Type.DefaultBinder does not infer T for Concat<T>(IEnumerable<T>), so other enumerables bind
            // to Concat(object). Call that overload directly to preserve its runtime-specific null behavior.
            _ => string.Concat(value),
        };

        return true;
    }

    private static bool TryConcatSegments(ref FunctionArguments args, out object? result)
    {
        switch (args.Length)
        {
            case 2:
                if (args.TryGetSegment(0, out StringSegment arg0) &&
                    args.TryGetSegment(1, out StringSegment arg1))
                {
                    result = Concat(arg0, arg1);
                    return true;
                }

                break;

            case 3:
                if (args.TryGetSegment(0, out arg0) &&
                    args.TryGetSegment(1, out arg1) &&
                    args.TryGetSegment(2, out StringSegment arg2))
                {
                    result = Concat(arg0, arg1, arg2);
                    return true;
                }

                break;

            case 4:
                if (args.TryGetSegment(0, out arg0) &&
                    args.TryGetSegment(1, out arg1) &&
                    args.TryGetSegment(2, out arg2) &&
                    args.TryGetSegment(3, out StringSegment arg3))
                {
                    result = Concat(arg0, arg1, arg2, arg3);
                    return true;
                }

                break;
        }

        result = null;
        return false;
    }

    private static string ConcatValues(ref FunctionArguments args)
    {
#if NET
        using ValueStringBuilder builder = new(stackalloc char[256]);
#else
        StringBuilder builder = StringBuilderCache.Acquire();
#endif

        for (int i = 0; i < args.Length; i++)
        {
            if (args.TryGetSegment(i, out StringSegment segment))
            {
#if NET
                builder.Append(segment.AsSpan());
#else
                builder.AppendSegment(segment);
#endif
            }
            else
            {
                // Match String.Concat(object): null and a null ToString() result contribute an empty string.
                builder.Append(args.GetValue(i)?.ToString());
            }
        }

#if NET
        return builder.ToString();
#else
        return StringBuilderCache.GetStringAndRelease(builder);
#endif
    }

    private static string Concat(StringSegment arg0, StringSegment arg1)
    {
        if (arg0.IsNullOrEmpty)
        {
            return arg1.ValueOrEmpty;
        }

        if (arg1.IsNullOrEmpty)
        {
            return arg0.ValueOrEmpty;
        }

#if NET
        return string.Concat(arg0.AsSpan(), arg1.AsSpan());
#else
        StringBuilder builder = StringBuilderCache.Acquire(arg0.Length + arg1.Length);
        builder.AppendSegment(arg0);
        builder.AppendSegment(arg1);
        return StringBuilderCache.GetStringAndRelease(builder);
#endif
    }

    private static string Concat(StringSegment arg0, StringSegment arg1, StringSegment arg2)
    {
        if (arg0.IsNullOrEmpty)
        {
            return Concat(arg1, arg2);
        }

        if (arg1.IsNullOrEmpty)
        {
            return Concat(arg0, arg2);
        }

        if (arg2.IsNullOrEmpty)
        {
            return Concat(arg0, arg1);
        }

#if NET
        return string.Concat(arg0.AsSpan(), arg1.AsSpan(), arg2.AsSpan());
#else
        StringBuilder builder = StringBuilderCache.Acquire(arg0.Length + arg1.Length + arg2.Length);
        builder.AppendSegment(arg0);
        builder.AppendSegment(arg1);
        builder.AppendSegment(arg2);
        return StringBuilderCache.GetStringAndRelease(builder);
#endif
    }

    private static string Concat(StringSegment arg0, StringSegment arg1, StringSegment arg2, StringSegment arg3)
    {
        if (arg0.IsNullOrEmpty)
        {
            return Concat(arg1, arg2, arg3);
        }

        if (arg1.IsNullOrEmpty)
        {
            return Concat(arg0, arg2, arg3);
        }

        if (arg2.IsNullOrEmpty)
        {
            return Concat(arg0, arg1, arg3);
        }

        if (arg3.IsNullOrEmpty)
        {
            return Concat(arg0, arg1, arg2);
        }

#if NET
        return string.Concat(arg0.AsSpan(), arg1.AsSpan(), arg2.AsSpan(), arg3.AsSpan());
#else
        StringBuilder builder = StringBuilderCache.Acquire(arg0.Length + arg1.Length + arg2.Length + arg3.Length);
        builder.AppendSegment(arg0);
        builder.AppendSegment(arg1);
        builder.AppendSegment(arg2);
        builder.AppendSegment(arg3);
        return StringBuilderCache.GetStringAndRelease(builder);
#endif
    }
}
