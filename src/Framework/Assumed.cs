// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework;

internal static partial class Assumed
{
    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value being checked.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <param name="message">
    ///  An optional message to include in the exception. If not provided, a default message
    ///  containing <paramref name="valueExpression"/> is used.
    /// </param>
    /// <param name="valueExpression">
    ///  The caller's source text for <paramref name="value"/>. Automatically captured via
    ///  <see cref="CallerArgumentExpressionAttribute"/>.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public static void NotNull<T>(
        [NotNull] this T? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is null)
        {
            ThrowInternalError(message ?? SR.FormatExpectedNonNullValue(valueExpression), path, line);
        }
    }

    /// <inheritdoc cref="NotNull{T}(T, string, string, string, int)"/>
    /// <param name="value">The value to check.</param>
    /// <param name="message">
    ///  An interpolated string message to include in the exception. Only formatted if the assertion fails.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    public static void NotNull<T>(
        [NotNull] this T? value,
        [InterpolatedStringHandlerArgument(nameof(value))] IsNullInterpolatedStringHandler<T> message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is null)
        {
            ThrowInternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="value"/> is not <see langword="null"/> or empty.
    /// </summary>
    /// <param name="value">The string value to check.</param>
    /// <param name="message">
    ///  An optional message to include in the exception. If not provided, a default message
    ///  containing <paramref name="valueExpression"/> is used.
    /// </param>
    /// <param name="valueExpression">
    ///  The caller's source text for <paramref name="value"/>. Automatically captured via
    ///  <see cref="CallerArgumentExpressionAttribute"/>.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  <paramref name="value"/> is <see langword="null"/> or empty.
    /// </exception>
    public static void NotNullOrEmpty(
        [NotNull] this string? value,
        string? message = null,
        [CallerArgumentExpression(nameof(value))] string? valueExpression = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        NotNull(value, message, valueExpression, path, line);

        if (value.Length == 0)
        {
            ThrowInternalError(message ?? SR.FormatExpectedNonNullValue(valueExpression), path, line);
        }
    }

    /// <inheritdoc cref="NotNullOrEmpty(string, string, string, string, int)"/>
    /// <param name="value">The string value to check.</param>
    /// <param name="message">
    ///  An interpolated string message to include in the exception. Only formatted if the assertion fails.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    public static void NotNullOrEmpty(
        [NotNull] this string? value,
        [InterpolatedStringHandlerArgument(nameof(value))] IsNullOrEmptyInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (value is null || value.Length == 0)
        {
            ThrowInternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="false"/>.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="false"/>.</param>
    /// <param name="message">
    ///  An optional message to include in the exception. If not provided, a default message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  <paramref name="condition"/> is <see langword="true"/>.
    /// </exception>
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (condition)
        {
            ThrowInternalError(message ?? SR.ExpectedFalse, path, line);
        }
    }

    /// <inheritdoc cref="False(bool, string, string, int)"/>
    /// <param name="condition">The condition expected to be <see langword="false"/>.</param>
    /// <param name="message">
    ///  An interpolated string message to include in the exception. Only formatted if the assertion fails.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    public static void False(
        [DoesNotReturnIf(true)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ConditionTrueInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (condition)
        {
            ThrowInternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Asserts that <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition expected to be <see langword="true"/>.</param>
    /// <param name="message">
    ///  An optional message to include in the exception. If not provided, a default message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  <paramref name="condition"/> is <see langword="false"/>.
    /// </exception>
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            ThrowInternalError(message ?? SR.ExpectedTrue, path, line);
        }
    }

    /// <inheritdoc cref="True(bool, string, string, int)"/>
    /// <param name="condition">The condition expected to be <see langword="true"/>.</param>
    /// <param name="message">
    ///  An interpolated string message to include in the exception. Only formatted if the assertion fails.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    public static void True(
        [DoesNotReturnIf(false)] bool condition,
        [InterpolatedStringHandlerArgument(nameof(condition))] ConditionFalseInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
    {
        if (!condition)
        {
            ThrowInternalError(message.GetFormattedText(), path, line);
        }
    }

    /// <summary>
    ///  Can be called at points that are assumed to be unreachable at runtime.
    /// </summary>
    /// <param name="message">
    ///  An optional message to include in the exception. If not provided, a default message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  Always thrown. This method never returns.
    /// </exception>
    [DoesNotReturn]
    public static void Unreachable(
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => ThrowInternalError(message ?? SR.UnreachableLocation, path, line);

    /// <summary>
    ///  Can be called at points that are assumed to be unreachable at runtime.
    /// </summary>
    /// <param name="message">
    ///  An interpolated string message to include in the exception. Always formatted.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  Always thrown. This method never returns.
    /// </exception>
    [DoesNotReturn]
    public static void Unreachable(
        UnconditionalInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => ThrowInternalError(message.GetFormattedText(), path, line);

    /// <summary>
    ///  Can be called at points that are assumed to be unreachable at runtime.
    /// </summary>
    /// <typeparam name="T">
    ///  The return type, allowing this method to be used in expression contexts.
    /// </typeparam>
    /// <param name="message">
    ///  An optional message to include in the exception. If not provided, a default message is used.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  Always thrown. This method never returns.
    /// </exception>
    [DoesNotReturn]
    public static T Unreachable<T>(
        string? message = null,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => ThrowInternalError<T>(message ?? SR.UnreachableLocation, path, line);

    /// <summary>
    ///  Can be called at points that are assumed to be unreachable at runtime.
    /// </summary>
    /// <typeparam name="T">
    ///  The return type, allowing this method to be used in expression contexts.
    /// </typeparam>
    /// <param name="message">
    ///  An interpolated string message to include in the exception. Always formatted.
    /// </param>
    /// <param name="path">The source file path of the caller. Automatically captured.</param>
    /// <param name="line">The source line number of the caller. Automatically captured.</param>
    /// <exception cref="InternalErrorException">
    ///  Always thrown. This method never returns.
    /// </exception>
    [DoesNotReturn]
    public static T Unreachable<T>(
        UnconditionalInterpolatedStringHandler message,
        [CallerFilePath] string? path = null,
        [CallerLineNumber] int line = 0)
        => ThrowInternalError<T>(message.GetFormattedText(), path, line);

    [DebuggerHidden]
    [DoesNotReturn]
    private static void ThrowInternalError(string message, string? path, int line)
        => throw new InternalErrorException(message + Environment.NewLine + SR.FormatFileAndLine(path, line));

    [DebuggerHidden]
    [DoesNotReturn]
    private static T ThrowInternalError<T>(string message, string? path, int line)
        => throw new InternalErrorException(message + Environment.NewLine + SR.FormatFileAndLine(path, line));
}
