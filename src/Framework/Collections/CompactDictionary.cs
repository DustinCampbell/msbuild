// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Collections;

/// <summary>
///  A lightweight, immutable dictionary optimized for small numbers of entries (0-2),
///  with O(1) lookup for all sizes. Avoids the overhead of <see cref="ImmutableDictionary{TKey, TValue}"/>
///  which uses O(log n) lookup.
/// </summary>
/// <remarks>
///  Prefer <see cref="CompactDictionary{TKey, TValue}"/> over
///  <see cref="ImmutableDictionary{TKey, TValue}"/> unless the
///  persistence/snapshotting behavior of <see cref="ImmutableDictionary{TKey, TValue}"/>
///  is specifically needed (i.e. cheap snapshots that share structure with previous versions).
/// </remarks>
internal abstract partial class CompactDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    public static readonly CompactDictionary<TKey, TValue> Empty = new NoEntries();

    private protected abstract TKey[] KeysCore { get; }

    private protected abstract TValue[] ValuesCore { get; }

    public abstract int Count { get; }

    public abstract TValue this[TKey key] { get; }

    public abstract bool ContainsKey(TKey key);

    bool IReadOnlyDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
        => TryGetValue(key, out value!);

    public abstract bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value);

    public ImmutableArray<TKey> Keys => ImmutableCollectionsMarshal.AsImmutableArray(KeysCore);

    public ImmutableArray<TValue> Values => ImmutableCollectionsMarshal.AsImmutableArray(ValuesCore);

    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => KeysCore;

    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => ValuesCore;

    public Enumerator GetEnumerator()
        => new(KeysCore, ValuesCore);

    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
