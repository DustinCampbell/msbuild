// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class ReadOnlyCollection<T>
{
    private sealed class CollectionWrapper(ICollection<T> source) : ReadOnlyCollection<T>
    {
        public override int Count => source.Count;

        public override bool Contains(T item)
            => source.Contains(item);

        protected override void CopyToCore(T[] array, int index)
            => source.CopyTo(array, index);

        protected override void CopyToCore(Array array, int index)
        {
            // If the source collection is an array, we can use Array.Copy.
            // If the source collection is an IReadOnlyList<T>, we can use the indexer to copy the elements.
            // If the source collection is an ICollection, we can use its CopyTo method.
            // Otherwise, we can fall back to enumerating the collection and copying the elements one by one.
            if (source is T[] sourceArray)
            {
                Array.Copy(sourceArray, sourceIndex: 0, array, index, sourceArray.Length);
            }
            else if (source is IReadOnlyList<T> sourceList)
            {
                int count = sourceList.Count;

                for (int i = 0, j = index; i < count; i++, j++)
                {
                    array.SetValue(sourceList[i], j);
                }
            }
            else if (source is ICollection sourceCollection)
            {
                sourceCollection.CopyTo(array, index);
            }
            else
            {
                int i = index;
                foreach (T entry in source)
                {
                    array.SetValue(entry, i);
                    i++;
                }
            }
        }

        public override IEnumerator<T> GetEnumerator()
            => source.GetEnumerator();
    }
}
