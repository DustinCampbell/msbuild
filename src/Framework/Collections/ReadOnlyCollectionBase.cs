// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract class ReadOnlyCollectionBase<T> : IReadOnlyCollection<T>, ICollection<T>, ICollection
{
    public static ReadOnlyCollectionBase<T> Empty => EmptyCollection.Instance;

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

    private sealed class EmptyCollection : ReadOnlyCollectionBase<T>
    {
        public static readonly EmptyCollection Instance = new();

        private EmptyCollection()
        {
        }

        public override int Count => 0;

        public override bool Contains(T item)
            => false;

        public override void CopyTo(T[] array, int arrayIndex)
        {
        }

        protected override void CopyTo(Array array, int index)
        {
        }

        public override IEnumerator<T> GetEnumerator()
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
}
