// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build;

internal static class InvalidOperationExceptionExtensions
{
    extension(InvalidOperationException)
    {
        [DoesNotReturn]
        public static void Throw(ResourceString resourceString)
            => throw new InvalidOperationException(resourceString.TextWithoutCode);

        [DoesNotReturn]
        public static void Throw(ResourceString resourceString, object? arg0)
            => throw new InvalidOperationException(resourceString.FormatStripCode(arg0));

        [DoesNotReturn]
        public static void Throw(ResourceString resourceString, object? arg0, object? arg1)
            => throw new InvalidOperationException(resourceString.FormatStripCode(arg0, arg1));

        [DoesNotReturn]
        public static void Throw(ResourceString resourceString, object? arg0, object? arg1, object? arg2)
            => throw new InvalidOperationException(resourceString.FormatStripCode(arg0, arg1, arg2));

        [DoesNotReturn]
        public static void Throw(ResourceString resourceString, params object?[] args)
            => throw new InvalidOperationException(resourceString.FormatStripCode(args));

#if NET
        [DoesNotReturn]
        public static void Throw(ResourceString resourceString, params ReadOnlySpan<object?> args)
            => throw new InvalidOperationException(resourceString.FormatStripCode(args));
#endif

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resourceString)
        {
            if (!condition)
            {
                Throw(resourceString);
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resourceString, object? arg0)
        {
            if (!condition)
            {
                Throw(resourceString, arg0);
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resourceString, object? arg0, object? arg1)
        {
            if (!condition)
            {
                Throw(resourceString, arg0, arg1);
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resourceString, object? arg0, object? arg1, object? arg2)
        {
            if (!condition)
            {
                Throw(resourceString, arg0, arg1, arg2);
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resourceString, params object?[] args)
        {
            if (!condition)
            {
                Throw(resourceString, args);
            }
        }

#if NET
        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resourceString, params ReadOnlySpan<object?> args)
        {
            if (!condition)
            {
                Throw(resourceString, args);
            }
        }
#endif

        public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, ResourceString resourceString)
        {
            if (condition)
            {
                Throw(resourceString);
            }
        }

        public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, ResourceString resourceString, object? arg0)
        {
            if (condition)
            {
                Throw(resourceString, arg0);
            }
        }

        public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, ResourceString resourceString, object? arg0, object? arg1)
        {
            if (condition)
            {
                Throw(resourceString, arg0, arg1);
            }
        }

        public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, ResourceString resourceString, object? arg0, object? arg1, object? arg2)
        {
            if (condition)
            {
                Throw(resourceString, arg0, arg1, arg2);
            }
        }

        public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, ResourceString resourceString, params object?[] args)
        {
            if (condition)
            {
                Throw(resourceString, args);
            }
        }

#if NET
        public static void ThrowIfTrue([DoesNotReturnIf(true)] bool condition, ResourceString resourceString, params ReadOnlySpan<object?> args)
        {
            if (condition)
            {
                Throw(resourceString, args);
            }
        }
#endif
    }
}
