// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

internal abstract partial class CompactDictionary<TKey, TValue>
{
    private sealed class TwoEntries(TKey key1, TValue value1, TKey key2, TValue value2, IEqualityComparer<TKey> comparer) : CompactDictionary<TKey, TValue>
    {
        private protected override TKey[] KeysCore => field ??= [key1, key2];

        private protected override TValue[] ValuesCore => field ??= [value1, value2];

        public override int Count => 2;

        public override TValue this[TKey key]
            => comparer.Equals(key1, key)
                ? value1
                : comparer.Equals(key2, key)
                ? value2 : throw new KeyNotFoundException();

        public override bool ContainsKey(TKey key)
            => comparer.Equals(key1, key) || comparer.Equals(key2, key);

        public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (comparer.Equals(key1, key))
            {
                value = value1;
                return true;
            }

            if (comparer.Equals(key2, key))
            {
                value = value2;
                return true;
            }

            value = default;
            return false;
        }
    }
}
