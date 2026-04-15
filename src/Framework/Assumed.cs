// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build;

internal static partial class Assumed
{
    /// <summary>
    ///  Asserts that <paramref name="value"/> is <see langword="null"/>. Throws an
    ///  <see cref="InternalErrorException"/> if it is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of value being checked.</typeparam>
    /// <param name="value">The value expected to be <see langword="null"/>.</param>
    /// <param name="message">
    ///  An optional message describing the unexpected condition. If <see langword="null"/>,
    ///  a default message is used.
    /// </param>
    /// <param name="valueExpression">
    ///  The source text of <paramref name="value"/>. Populated automatically and used
    ///  in the default error message.
    /// </param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="value"/> is not <see langword="null"/>.
    /// </exception>
    public static void Null<T>(
        T? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is not null)
        {
            InternalError(message ?? SR.FormatExpectedNull(valueExpression), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is <see langword="null"/>, using an
    ///  interpolated string handler to avoid formatting overhead when the assertion holds.
    /// </summary>
    /// <typeparam name="T">The type of value being checked.</typeparam>
    /// <param name="value">The value expected to be <see langword="null"/>.</param>
    /// <param name="message">An interpolated string describing the unexpected condition.</param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="value"/> is not <see langword="null"/>.
    /// </exception>
    public static void Null<T>(
        T? value,
        [InterpolatedStringHandlerArgument(nameof(value))] IfNullInterpolatedStringHandler<T> message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is not null)
        {
            InternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/>. Throws an
    ///  <see cref="InternalErrorException"/> if it is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of value being checked.</typeparam>
    /// <param name="value">The value expected to be non-<see langword="null"/>.</param>
    /// <param name="message">
    ///  An optional message describing the unexpected condition. If <see langword="null"/>,
    ///  a default message is used.
    /// </param>
    /// <param name="valueExpression">
    ///  The source text of <paramref name="value"/>. Populated automatically and used
    ///  in the default error message.
    /// </param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static void NotNull<T>(
        [NotNull] T? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is null)
        {
            InternalError(message ?? SR.FormatExpectedNonNull(valueExpression), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/>, using an
    ///  interpolated string handler to avoid formatting overhead when the assertion holds.
    /// </summary>
    /// <typeparam name="T">The type of value being checked.</typeparam>
    /// <param name="value">The value expected to be non-<see langword="null"/>.</param>
    /// <param name="message">An interpolated string describing the unexpected condition.</param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static void NotNull<T>(
        [NotNull] T? value,
        [InterpolatedStringHandlerArgument(nameof(value))] IfNotNullInterpolatedStringHandler<T> message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is null)
        {
            InternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="true"/>. Throws an
    ///  <see cref="InternalErrorException"/> if it is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="true"/>.</param>
    /// <param name="message">
    ///  An optional message describing the unexpected condition. If <see langword="null"/>,
    ///  a default message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            InternalError(message ?? SR.ExpectedTrue, path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="true"/>, using an
    ///  interpolated string handler to avoid formatting overhead when the assertion holds.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="true"/>.</param>
    /// <param name="message">An interpolated string describing the unexpected condition.</param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] IfTrueInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            InternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="false"/>. Throws an
    ///  <see cref="InternalErrorException"/> if it is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="false"/>.</param>
    /// <param name="message">
    ///  An optional message describing the unexpected condition. If <see langword="null"/>,
    ///  a default message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="condition"/> is <see langword="true"/>.
    /// </exception>
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (condition)
        {
            InternalError(message ?? SR.ExpectedFalse, path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="false"/>, using an
    ///  interpolated string handler to avoid formatting overhead when the assertion holds.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="false"/>.</param>
    /// <param name="message">An interpolated string describing the unexpected condition.</param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">
    ///  Thrown when <paramref name="condition"/> is <see langword="true"/>.
    /// </exception>
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] IfFalseInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (condition)
        {
            InternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> to indicate that a code path assumed
    ///  to be unreachable was reached at runtime.
    /// </summary>
    /// <param name="message">
    ///  An optional message describing the unexpected condition. If <see langword="null"/>,
    ///  a default "unreachable" message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">Always thrown.</exception>
    [DoesNotReturn]
    public static void Unreachable(
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => InternalError(message ?? SR.Unreachable, path, line);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> to indicate that a code path assumed
    ///  to be unreachable was reached at runtime, using an interpolated string handler to
    ///  avoid formatting overhead when the call is optimized away.
    /// </summary>
    /// <param name="message">An interpolated string describing the unexpected condition.</param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">Always thrown.</exception>
    [DoesNotReturn]
    public static void Unreachable(
        UnreachableInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => InternalError(message.GetFormattedText(), path, line);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> to indicate that a code path assumed
    ///  to be unreachable was reached at runtime. The return type <typeparamref name="T"/>
    ///  allows this method to be used in expression contexts.
    /// </summary>
    /// <typeparam name="T">
    ///  The nominal return type, allowing this method to be used in expression positions
    ///  (e.g., ternary operators or switch expressions). The method never actually returns.
    /// </typeparam>
    /// <param name="message">
    ///  An optional message describing the unexpected condition. If <see langword="null"/>,
    ///  a default "unreachable" message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">Always thrown.</exception>
    [DoesNotReturn]
    public static T Unreachable<T>(
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => InternalError<T>(message ?? SR.Unreachable, path, line);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> to indicate that a code path assumed
    ///  to be unreachable was reached at runtime, using an interpolated string handler to
    ///  avoid formatting overhead when the call is optimized away. The return type
    ///  <typeparamref name="T"/> allows this method to be used in expression contexts.
    /// </summary>
    /// <typeparam name="T">
    ///  The nominal return type, allowing this method to be used in expression positions
    ///  (e.g., ternary operators or switch expressions). The method never actually returns.
    /// </typeparam>
    /// <param name="message">An interpolated string describing the unexpected condition.</param>
    /// <param name="path">The source file path of the caller. Populated automatically.</param>
    /// <param name="line">The source line number of the caller. Populated automatically.</param>
    /// <exception cref="InternalErrorException">Always thrown.</exception>
    [DoesNotReturn]
    public static T Unreachable<T>(
        UnreachableInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => InternalError<T>(message.GetFormattedText(), path, line);

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with a message that includes
    ///  the caller's file path and line number.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InternalError(string message, string? path, int line)
        => throw new InternalErrorException(message + Environment.NewLine + SR.FormatFileAndLine(path, line));

    /// <summary>
    ///  Throws an <see cref="InternalErrorException"/> with a message that includes
    ///  the caller's file path and line number. The return type <typeparamref name="T"/>
    ///  allows this method to be used in expression contexts.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T InternalError<T>(string message, string? path, int line)
        => throw new InternalErrorException(message + Environment.NewLine + SR.FormatFileAndLine(path, line));
}
