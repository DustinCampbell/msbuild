// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build;

internal static partial class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <summary>
        ///  Throws an exception if <paramref name="argument"/> is non-<see langword="null"/> and empty.
        /// </summary>
        /// <param name="argument">
        ///  The string argument to validate as null or non-empty.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        /// </param>
        /// <exception cref="ArgumentException">
        ///  <paramref name="argument"/> is empty.
        /// </exception>
        public static void ThrowIfNonNullAndEmpty(string? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            if (argument != null)
            {
                ArgumentException.ThrowIfNullOrEmpty(argument, paramName);
            }
        }

        /// <summary>
        ///  Throws an exception if <paramref name="argument"/> is <see langword="null"/> or empty.
        /// </summary>
        /// <param name="argument">
        ///  The string argument to validate as non-null and non-empty.
        /// </param>
        /// <param name="paramName">
        ///  The name of the parameter with which <paramref name="argument"/> corresponds.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///  <paramref name="argument"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///  <paramref name="argument"/> is empty.
        /// </exception>
        public static void ThrowIfNullOrEmpty<T>([NotNull] IEnumerable<T>? argument, [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        {
            ArgumentNullException.ThrowIfNull(argument, paramName);

            if (argument.IsEmpty)
            {
                ThrowEmpty(argument, paramName);
            }

            Contract.ThrowIfNull(argument);
        }
    }

#pragma warning disable IDE0051 // Private member is unused.

    [DoesNotReturn]
    private static void ThrowEmpty<T>(IEnumerable<T> argument, string? paramName)
        => throw new ArgumentException(SR.Argument_ContainsNoElements, paramName);
}
