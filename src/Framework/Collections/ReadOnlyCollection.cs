// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.Build.Collections;

/// <summary>
/// A read-only live wrapper over a collection.
/// It does not prevent modification of the values themselves.
/// </summary>
/// <typeparam name="T">Type of element in the collection.</typeparam>
internal sealed class ReadOnlyCollection<T> : IReadOnlyCollection<T>, ICollection<T>, ICollection
{
    private IEnumerable<T> _backing;

    internal ReadOnlyCollection(IEnumerable<T> backing)
    {
        Contract.ThrowIfFalse(backing != null, "Need backing collection");

        _backing = backing;
    }

    public int Count => BackingCollection.Count;

    public bool IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

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

    public void Add(T item)
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public void Clear()
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public bool Contains(T item)
        => BackingCollection.Contains(item);

    public void CopyTo(T[] array, int arrayIndex)
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

    public bool Remove(T item)
    {
        InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);
        return false;
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        int i = index;
        foreach (T entry in _backing)
        {
            array.SetValue(entry, i);
            i++;
        }
    }

    public IEnumerator<T> GetEnumerator()
        => _backing.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => _backing.GetEnumerator();
}
