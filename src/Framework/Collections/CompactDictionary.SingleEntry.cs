// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

internal abstract partial class CompactDictionary<TKey, TValue>
{
    private sealed class SingleEntry(TKey key, TValue value, IEqualityComparer<TKey> comparer) : CompactDictionary<TKey, TValue>
    {
        private readonly TKey _key = key;
        private readonly TValue _value = value;

        private protected override TKey[] KeysCore => field ??= [_key];

        private protected override TValue[] ValuesCore => field ??= [_value];

        public override int Count => 1;

        public override TValue this[TKey key]
            => comparer.Equals(_key, key) ? _value : throw new KeyNotFoundException();

        public override bool ContainsKey(TKey key)
            => comparer.Equals(_key, key);

        public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if (comparer.Equals(_key, key))
            {
                value = _value;
                return true;
            }

            value = default;
            return false;
        }
    }
}
