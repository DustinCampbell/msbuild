// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        /// <summary>
        /// A point-in-time snapshot of a <see cref="ItemOperationList"/>: the list plus the
        /// number of operations visible at capture time. Stored in
        /// <c>referencedItemLists</c> dictionaries to give each operation a stable view
        /// of the item types it depends on.
        /// </summary>
        private readonly struct ItemListRef
        {
            public readonly ItemOperationList List;
            public readonly int Count;

            public ItemListRef(ItemOperationList list, int count)
            {
                List = list;
                Count = count;
            }

            public I[] GetMatchedItems(GlobSet globsToIgnore)
                => List.GetMatchedItems(Count, globsToIgnore);

            public OrderedItemDataCollection.Builder GetItemData(GlobSet globsToIgnore)
                => List.GetItemData(Count, globsToIgnore);
        }
    }
}
