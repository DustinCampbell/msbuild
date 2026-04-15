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
