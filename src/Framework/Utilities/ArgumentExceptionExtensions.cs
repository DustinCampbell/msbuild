// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

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
    }
}
