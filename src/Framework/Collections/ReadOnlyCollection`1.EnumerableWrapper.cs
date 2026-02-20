// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class ReadOnlyCollection<T>
{
    private sealed class EnumerableWrapper(IEnumerable<T> source) : ReadOnlyCollection<T>
    {
        private IEnumerable<T> _source = source;

        public override int Count => Collection.Count;

        private ICollection<T> Collection
        {
            get
            {
                if (_source is not ICollection<T> collection)
                {
                    collection = [.. _source];
                    _source = collection;
                }

                return collection;
            }
        }

        public override bool Contains(T item)
            => Collection.Contains(item);

        protected override void CopyToCore(T[] array, int index)
            => Collection.CopyTo(array, index);

        protected override void CopyToCore(Array array, int index)
            => ((ICollection)Collection).CopyTo(array, index);

        public override IEnumerator<T> GetEnumerator()
            => Collection.GetEnumerator();
    }
}
