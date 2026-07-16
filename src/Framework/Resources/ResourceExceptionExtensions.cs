// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build;

/// <summary>
///  Provides <c>ThrowIfFalse</c>/<c>Throw</c> helpers that construct localized
///  <see cref="InvalidOperationException"/>s from a <see cref="ResourceString"/> and formatting
///  arguments. These replace the resource-name-based <c>ErrorUtilities.VerifyThrowInvalidOperation</c>
///  and <c>ErrorUtilities.ThrowInvalidOperation</c> helpers.
/// </summary>
/// <remarks>
///  The condition is checked before any resource lookup or formatting occurs, and the generic
///  argument overloads avoid boxing value-type arguments unless an exception is actually thrown.
///  Unlike <c>ArgumentExceptionExtensions</c> (a polyfill of BCL members) these are new MSBuild
///  APIs and are therefore compiled for every target framework.
/// </remarks>
internal static class InvalidOperationExceptionExtensions
{
    extension(InvalidOperationException)
    {
        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resource)
        {
            if (!condition)
            {
                ThrowCore(resource.FormatStripCode());
            }
        }

        public static void ThrowIfFalse<T0>([DoesNotReturnIf(false)] bool condition, ResourceString resource, T0 arg0)
        {
            if (!condition)
            {
                ThrowCore(resource.FormatStripCode(arg0));
            }
        }

        public static void ThrowIfFalse<T0, T1>([DoesNotReturnIf(false)] bool condition, ResourceString resource, T0 arg0, T1 arg1)
        {
            if (!condition)
            {
                ThrowCore(resource.FormatStripCode(arg0, arg1));
            }
        }

        public static void ThrowIfFalse<T0, T1, T2>([DoesNotReturnIf(false)] bool condition, ResourceString resource, T0 arg0, T1 arg1, T2 arg2)
        {
            if (!condition)
            {
                ThrowCore(resource.FormatStripCode(arg0, arg1, arg2));
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resource, params object?[]? args)
        {
            if (!condition)
            {
                ThrowCore(resource.FormatStripCode(args));
            }
        }

        [DoesNotReturn]
        public static void Throw(ResourceString resource)
            => ThrowCore(resource.FormatStripCode());

        [DoesNotReturn]
        public static void Throw(ResourceString resource, params object?[]? args)
            => ThrowCore(resource.FormatStripCode(args));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCore(string message)
        => throw new InvalidOperationException(message);
}

/// <summary>
///  Provides <c>ThrowIfFalse</c>/<c>Throw</c> helpers that construct localized
///  <see cref="ArgumentException"/>s from a <see cref="ResourceString"/> and formatting arguments.
///  These replace the resource-name-based <c>ErrorUtilities.VerifyThrowArgument</c> and
///  <c>ErrorUtilities.ThrowArgument</c> helpers.
/// </summary>
internal static class ArgumentExceptionResourceExtensions
{
    extension(ArgumentException)
    {
        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resource)
        {
            if (!condition)
            {
                ThrowCore(null, resource.FormatStripCode());
            }
        }

        public static void ThrowIfFalse<T0>([DoesNotReturnIf(false)] bool condition, ResourceString resource, T0 arg0)
        {
            if (!condition)
            {
                ThrowCore(null, resource.FormatStripCode(arg0));
            }
        }

        public static void ThrowIfFalse<T0, T1>([DoesNotReturnIf(false)] bool condition, ResourceString resource, T0 arg0, T1 arg1)
        {
            if (!condition)
            {
                ThrowCore(null, resource.FormatStripCode(arg0, arg1));
            }
        }

        public static void ThrowIfFalse<T0, T1, T2>([DoesNotReturnIf(false)] bool condition, ResourceString resource, T0 arg0, T1 arg1, T2 arg2)
        {
            if (!condition)
            {
                ThrowCore(null, resource.FormatStripCode(arg0, arg1, arg2));
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, ResourceString resource, params object?[]? args)
        {
            if (!condition)
            {
                ThrowCore(null, resource.FormatStripCode(args));
            }
        }

        public static void ThrowIfFalse([DoesNotReturnIf(false)] bool condition, Exception? innerException, ResourceString resource, params object?[]? args)
        {
            if (!condition)
            {
                ThrowCore(innerException, resource.FormatStripCode(args));
            }
        }

        [DoesNotReturn]
        public static void Throw(ResourceString resource, params object?[]? args)
            => ThrowCore(null, resource.FormatStripCode(args));

        [DoesNotReturn]
        public static void Throw(Exception? innerException, ResourceString resource, params object?[]? args)
            => ThrowCore(innerException, resource.FormatStripCode(args));
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCore(Exception? innerException, string message)
        => throw new ArgumentException(message, innerException);
}
