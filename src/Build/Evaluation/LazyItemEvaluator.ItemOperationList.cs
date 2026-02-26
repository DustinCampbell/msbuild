// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
    where P : class, IProperty, IEquatable<P>, IValued
    where I : class, IItem<M>, IMetadataTable
    where M : class, IMetadatum
    where D : class, IItemDefinition<M>
{
    /// <summary>
    /// A mutable flat list of item operations for a single item type.
    /// Point-in-time snapshots are captured as <see cref="ItemListRef"/> values that pair
    /// this list with a count (the number of operations visible at capture time).
    /// </summary>
    private sealed class ItemOperationList
    {
        private readonly List<ItemOperation> _operations = new();

        /// <summary>
        /// Operation counts at which an external caller will query this list.
        /// Only these counts produce cached results.
        /// </summary>
        private HashSet<int> _referencedCounts;

        /// <summary>
        /// Sparse cache keyed by operation count. Each entry stores the <see cref="GlobSet"/>
        /// and the resulting immutable item collection after applying operations [0..Count).
        /// Typically 1–2 entries per list.
        /// </summary>
        private List<CacheEntry> _cache;

        private struct CacheEntry
        {
            public int Count;
            public GlobSet GlobsToIgnore;
            public OrderedItemDataCollection Items;
        }

        public int Count => _operations.Count;

        public void Add(ItemOperation operation) => _operations.Add(operation);

        public void MarkAsReferenced(int count)
        {
            _referencedCounts ??= new HashSet<int>();
            _referencedCounts.Add(count);
        }

        public I[] GetMatchedItems(int count, GlobSet globsToIgnore)
        {
            OrderedItemDataCollection.Builder items = GetItemData(count, globsToIgnore);
            using var builder = new RefArrayBuilder<I>(initialCapacity: items.Count);

            foreach (ItemData data in items)
            {
                if (data.ConditionResult)
                {
                    builder.Add(data.Item);
                }
            }

            return builder.AsSpan().ToArray();
        }

        public OrderedItemDataCollection.Builder GetItemData(int count, GlobSet globsToIgnore)
        {
            if (TryGetFromCache(count, globsToIgnore, out OrderedItemDataCollection cached))
            {
                return cached.ToBuilder();
            }

            // Ensure we cache the result at this count.
            MarkAsReferenced(count);

            return ComputeItems(count, globsToIgnore);
        }

        private bool TryGetFromCache(int count, GlobSet globsToIgnore, out OrderedItemDataCollection items)
        {
            if (_cache != null)
            {
                foreach (CacheEntry entry in _cache)
                {
                    if (entry.Count == count && ReferenceEquals(entry.GlobsToIgnore, globsToIgnore))
                    {
                        items = entry.Items;
                        return true;
                    }
                }
            }

            items = null;
            return false;
        }

        private void SetCache(int count, GlobSet globsToIgnore, OrderedItemDataCollection items)
        {
            _cache ??= new List<CacheEntry>(2);

            for (int i = 0; i < _cache.Count; i++)
            {
                if (_cache[i].Count == count)
                {
                    _cache[i] = new CacheEntry { Count = count, GlobsToIgnore = globsToIgnore, Items = items };
                    return;
                }
            }

            _cache.Add(new CacheEntry { Count = count, GlobsToIgnore = globsToIgnore, Items = items });
        }

        /// <summary>
        /// Evaluates operations [0..<paramref name="count"/>) and returns the resulting items.
        /// Preserves all existing optimizations:
        /// <list type="bullet">
        ///   <item>Backward pre-scan for <see cref="RemoveOperation"/> globs (globsToIgnore propagation).</item>
        ///   <item>Forward <see cref="UpdateOperation"/> batching.</item>
        ///   <item>Cache lookup/population at referenced counts.</item>
        /// </list>
        /// </summary>
        private OrderedItemDataCollection.Builder ComputeItems(int count, GlobSet globsToIgnore)
        {
            // Phase 1: Walk backward from count-1 looking for a cache hit and
            // accumulating globsToIgnore from RemoveOperations.
            OrderedItemDataCollection.Builder items = null;
            int startIndex = 0;
            Stack<GlobSet> globsToIgnoreStack = null;

            for (int i = count - 1; i >= 0; i--)
            {
                GlobSet currentGlobs = globsToIgnoreStack?.Peek() ?? globsToIgnore;

                // Check whether the result of [0..i] (count = i+1) is cached with
                // the current accumulated globs.
                if (TryGetFromCache(i + 1, currentGlobs, out OrderedItemDataCollection cached))
                {
                    items = cached.ToBuilder();
                    startIndex = i + 1;
                    break;
                }

                if (_operations[i] is RemoveOperation removeOperation)
                {
                    globsToIgnoreStack ??= new Stack<GlobSet>();

                    HashSet<string> globs = removeOperation.GetRemovedGlobs();
                    foreach (string glob in currentGlobs.Globs)
                    {
                        globs.Add(glob);
                    }

                    globsToIgnoreStack.Push(GlobSet.Create(globs));
                }
            }

            items ??= OrderedItemDataCollection.CreateBuilder();

            GlobSet currentGlobsToIgnore = (globsToIgnoreStack == null || globsToIgnoreStack.Count == 0)
                ? globsToIgnore
                : globsToIgnoreStack.Peek();

            // Phase 2: Apply operations forward from startIndex to count-1.
            var updateBatch = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);
            bool addedToBatch = false;

            for (int i = startIndex; i < count; i++)
            {
                ItemOperation op = _operations[i];

                if (op is UpdateOperation updateOp)
                {
                    bool addToBatch = true;
                    int j;
                    for (j = 0; j < updateOp.Spec.Fragments.Count; j++)
                    {
                        ItemSpecFragment frag = updateOp.Spec.Fragments[j];
                        if (MSBuildConstants.CharactersForExpansion.Any(frag.TextFragment.Contains))
                        {
                            addToBatch = false;
                            break;
                        }

                        string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(frag.TextFragment, frag.ProjectDirectory);
                        if (updateBatch.ContainsKey(fullPath))
                        {
                            addToBatch = false;
                            break;
                        }

                        updateBatch.Add(fullPath, updateOp);
                    }

                    if (!addToBatch)
                    {
                        for (int k = 0; k < j; k++)
                        {
                            updateBatch.Remove(updateOp.Spec.Fragments[k].TextFragment);
                        }
                    }
                    else
                    {
                        addedToBatch = true;
                        continue;
                    }
                }

                if (addedToBatch)
                {
                    addedToBatch = false;
                    ProcessNonWildCardItemUpdates(updateBatch, items);
                }

                if (op is RemoveOperation)
                {
                    globsToIgnoreStack.Pop();
                    currentGlobsToIgnore = (globsToIgnoreStack == null || globsToIgnoreStack.Count == 0)
                        ? globsToIgnore
                        : globsToIgnoreStack.Peek();
                }

                op.Apply(items, currentGlobsToIgnore);

                // Cache if this count is referenced.
                int countAfterOp = i + 1;
                if (_referencedCounts?.Contains(countAfterOp) == true)
                {
                    SetCache(countAfterOp, currentGlobsToIgnore, items.ToImmutable());
                }
            }

            ProcessNonWildCardItemUpdates(updateBatch, items);

            return items;
        }

        private static void ProcessNonWildCardItemUpdates(Dictionary<string, UpdateOperation> itemsWithNoWildcards, OrderedItemDataCollection.Builder items)
        {
            if (itemsWithNoWildcards.Count > 0)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(items[i].Item.EvaluatedInclude, items[i].Item.ProjectDirectory);
                    if (itemsWithNoWildcards.TryGetValue(fullPath, out UpdateOperation op))
                    {
                        items[i] = op.UpdateItem(items[i]);
                    }
                }

                itemsWithNoWildcards.Clear();
            }
        }
    }
}
