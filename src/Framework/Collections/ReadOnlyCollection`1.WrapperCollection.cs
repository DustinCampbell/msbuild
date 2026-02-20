// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class ReadOnlyCollection<T>
{
    private sealed class WrapperCollection : ReadOnlyCollection<T>
    {
        private IEnumerable<T> _backing;

        public WrapperCollection(IEnumerable<T> backing)
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
}
