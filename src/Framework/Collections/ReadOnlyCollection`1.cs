// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract class ReadOnlyCollection<T> : IReadOnlyCollection<T>, ICollection<T>, ICollection
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

    private sealed class WrapperCollection : ReadOnlyCollection<T>
    {
        private IEnumerable<T> _backing;

        internal WrapperCollection(IEnumerable<T> backing)
        {
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

    private sealed class EmptyCollection : ReadOnlyCollection<T>
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
