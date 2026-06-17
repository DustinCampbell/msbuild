// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

internal abstract partial class CompactDictionary<TKey, TValue>
{
    private sealed class ThreeOrMoreEntries(Dictionary<TKey, TValue> dictionary) : CompactDictionary<TKey, TValue>
    {
        private protected override TKey[] KeysCore => field ??= [.. dictionary.Keys];

        private protected override TValue[] ValuesCore => field ??= [.. dictionary.Values];

        public override int Count
            => dictionary.Count;

        public override TValue this[TKey key]
            => dictionary[key];

        public override bool ContainsKey(TKey key)
            => dictionary.ContainsKey(key);

        public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            => dictionary.TryGetValue(key, out value);
    }
}
