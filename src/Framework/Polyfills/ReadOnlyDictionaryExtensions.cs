// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Build;

internal static class ReadOnlyDictionaryExtensions
{
    private static class Empty<TKey, TValue>
        where TKey : notnull
    {
        public static readonly ReadOnlyDictionary<TKey, TValue> Instance = new(new Dictionary<TKey, TValue>());
    }

    extension<TKey, TValue>(ReadOnlyDictionary<TKey, TValue>)
        where TKey : notnull
    {
        /// <summary>
        ///  Gets an empty <see cref="ReadOnlyDictionary{TKey, TValue}"/>.
        /// </summary>
        /// <value>
        ///  An empty <see cref="ReadOnlyDictionary{TKey, TValue}"/>.
        /// </value>
        /// <remarks>
        ///  The returned instance is immutable and will always be empty.
        /// </remarks>
        public static ReadOnlyDictionary<TKey, TValue> Empty => Empty<TKey, TValue>.Instance;
    }

    /// <summary>
    /// Returns a read-only <see cref="ReadOnlyDictionary{TKey, TValue}"/> wrapper
    /// for the current dictionary.
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
    /// <param name="dictionary">The dictionary to wrap.</param>
    /// <returns>
    ///  An object that acts as a read-only wrapper around the current <see cref="IDictionary{TKey, TValue}"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is null.</exception>
    public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        where TKey : notnull
        => new(dictionary);
}
#endif
