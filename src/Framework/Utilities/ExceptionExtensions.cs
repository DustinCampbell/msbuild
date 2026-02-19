// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Microsoft.Build;

internal static class ExceptionExtensions
{
    extension(InvalidOperationException)
    {
        /// <inheritdoc cref="InvalidOperationException(string)"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw(string? message = null)
            => throw new InvalidOperationException(message);
    }
}
