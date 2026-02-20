// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.Collections;

internal static class ReadOnlyCollection
{
    public static ReadOnlyCollectionBase<T> Create<T>(IEnumerable<T>? backing)
        => backing is not null
            ? new ReadOnlyCollection<T>(backing)
            : ReadOnlyCollectionBase<T>.Empty;
}

/// <summary>
/// A read-only live wrapper over a collection.
/// It does not prevent modification of the values themselves.
/// </summary>
/// <typeparam name="T">Type of element in the collection.</typeparam>
internal sealed class ReadOnlyCollection<T> : ReadOnlyCollectionBase<T>
{
    private IEnumerable<T> _backing;

    internal ReadOnlyCollection(IEnumerable<T> backing)
    {
        Contract.ThrowIfFalse(backing != null, "Need backing collection");

        _backing = backing;
    }

    public override int Count => BackingCollection.Count;

    private ICollection<T> BackingCollection
    {
        get
        {
            if (_backing is not ICollection<T> backingCollection)
            {
                backingCollection = [.. _backing];
                _backing = backingCollection;
            }

            return backingCollection;
        }
    }

    public override bool Contains(T item)
        => BackingCollection.Contains(item);

    public override void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (_backing is ICollection<T> backingCollection)
        {
            backingCollection.CopyTo(array, arrayIndex);
        }
        else
        {
            int i = arrayIndex;
            foreach (T entry in _backing)
            {
                array[i] = entry;
                i++;
            }
        }
    }

    protected override void CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        int i = index;
        foreach (T entry in _backing)
        {
            array.SetValue(entry, i);
            i++;
        }
    }

    public override IEnumerator<T> GetEnumerator()
        => _backing.GetEnumerator();
}
