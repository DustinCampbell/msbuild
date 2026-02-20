// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
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
        public static ReadOnlyDictionary<TKey, TValue> Empty => Empty<TKey, TValue>.Instance;
    }
}
#endif
