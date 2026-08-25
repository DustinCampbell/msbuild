// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Collections;

/// <summary>
///  Creates <see cref="OneOrMany{T}"/> values for collection expressions.
/// </summary>
internal static class OneOrMany
{
    /// <summary>
    ///  Creates a <see cref="OneOrMany{T}"/> containing the supplied values.
    /// </summary>
    /// <typeparam name="T">The type of value to store.</typeparam>
    /// <param name="values">The values to store.</param>
    /// <returns>
    ///  A collection containing <paramref name="values"/>.
    /// </returns>
    public static OneOrMany<T> Create<T>(ReadOnlySpan<T> values)
        => values.Length switch
        {
            0 => default,
            1 => new OneOrMany<T>(values[0]),
            _ => new OneOrMany<T>(values[0], values[1..]),
        };
}
