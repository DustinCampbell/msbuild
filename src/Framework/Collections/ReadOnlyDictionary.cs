// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Microsoft.Build.Collections;

internal static class ReadOnlyDictionary
{
    public static ReadOnlyDictionary<TKey, TValue> Create<TKey, TValue>(IDictionary<TKey, TValue>? dictionary)
        where TKey : notnull
        => dictionary is { Count: > 0 }
            ? new ReadOnlyDictionary<TKey, TValue>(dictionary)
            : ReadOnlyDictionary<TKey, TValue>.Empty;

    public static ReadOnlyDictionary<TKey, TValue> Empty<TKey, TValue>()
        where TKey : notnull
        => ReadOnlyDictionary<TKey, TValue>.Empty;
}
