// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class ReadOnlyCollection<T> : IReadOnlyCollection<T>, ICollection<T>, ICollection
{
    public static ReadOnlyCollection<T> Empty => EmptyCollection.Instance;

    public static ReadOnlyCollection<T> Create(IEnumerable<T>? enumerable) => enumerable switch
    {
        ICollection<T> { Count: > 0 } collection => new CollectionWrapper(collection),
        IEnumerable<T> => new EnumerableWrapper(enumerable),
        _ => EmptyCollection.Instance
    };

    public static ReadOnlyCollection<T> Create(ICollection<T>? collection)
        => collection switch
        {
            { Count: > 0 } => new CollectionWrapper(collection),
            _ => EmptyCollection.Instance
        };

    public abstract int Count { get; }

    public bool IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    protected virtual void CopyToCore(T[] array, int index)
    {
    }

    protected virtual void CopyToCore(Array array, int index)
    {
    }

    public void Add(T item)
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public void Clear()
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public abstract bool Contains(T item);

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        CopyToCore(array, arrayIndex);
    }

    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (array is T[] typedArray)
        {
            CopyToCore(typedArray, index);
        }
        else
        {
            CopyToCore(array, index);
        }
    }

    public abstract IEnumerator<T> GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    public bool Remove(T item)
    {
        InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);
        return false;
    }
}
