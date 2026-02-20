// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class ReadOnlyCollection<T> : IReadOnlyCollection<T>, ICollection<T>, ICollection
{
    public static ReadOnlyCollection<T> Empty => EmptyCollection.Instance;

    public static ReadOnlyCollection<T> Create(IEnumerable<T>? backing)
        => backing is not null
            ? new WrapperCollection(backing)
            : EmptyCollection.Instance;

    public abstract int Count { get; }

    public bool IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public void Add(T item)
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public void Clear()
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public abstract bool Contains(T item);

    public abstract void CopyTo(T[] array, int arrayIndex);

    void ICollection.CopyTo(Array array, int index)
        => CopyTo(array, index);

    protected abstract void CopyTo(Array array, int index);

    public abstract IEnumerator<T> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool Remove(T item)
    {
        InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);
        return false;
    }
}
