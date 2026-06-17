// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

internal abstract partial class CompactDictionary<TKey, TValue>
{
    private sealed class NoEntries : CompactDictionary<TKey, TValue>
    {
        private protected override TKey[] KeysCore => [];

        private protected override TValue[] ValuesCore => [];

        public override int Count => 0;

        public override TValue this[TKey key]
            => throw new KeyNotFoundException();

        public override bool ContainsKey(TKey key)
            => false;

        public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            value = default;
            return false;
        }
    }
}
