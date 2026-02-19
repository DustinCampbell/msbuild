// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

/// <summary>
/// A read-only wrapper over an empty collection.
/// </summary>
/// <typeparam name="T">Type of element in the collection.</typeparam>
internal sealed class ReadOnlyEmptyCollection<T> : IReadOnlyCollection<T>, ICollection<T>, ICollection
{
    public static ReadOnlyEmptyCollection<T> Instance => field ??= new();

    private ReadOnlyEmptyCollection()
    {
    }

    public int Count => 0;

    public bool IsReadOnly => true;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    public void Add(T item)
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public void Clear()
        => InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);

    public bool Contains(T item)
        => false;

    public void CopyTo(T[] array, int arrayIndex)
    {
    }

    public bool Remove(T item)
    {
        InvalidOperationException.Throw(SR.OM_NotSupportedReadOnlyCollection);
        return false;
    }

    void ICollection.CopyTo(Array array, int index)
    {
    }

    public IEnumerator<T> GetEnumerator()
        => Enumerator.Instance;

    IEnumerator IEnumerable.GetEnumerator()
        => Enumerator.Instance;

    private sealed class Enumerator : IEnumerator<T>
    {
        public static readonly Enumerator Instance = new();

        private Enumerator()
        {
        }

        public T Current
        {
            get
            {
                InvalidOperationException.Throw();
                return default;
            }
        }

        object IEnumerator.Current => Current!;

        public void Dispose()
        {
        }

        public bool MoveNext()
            => false;

        public void Reset()
        {
        }
    }
}
