// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Identifies the exclusive end of an earlier state in an append-only item history.
    /// </summary>
    private readonly struct ItemListSnapshot
    {
        /// <summary>
        ///  The history containing the captured operations.
        /// </summary>
        private readonly ItemHistory _history;

        /// <summary>
        ///  The number of operations visible to this reference.
        /// </summary>
        private readonly int _operationCount;

        /// <summary>
        ///  Captures the current end of a history before the referencing operation is appended.
        /// </summary>
        /// <param name="history">The referenced history.</param>
        public ItemListSnapshot(ItemHistory history)
        {
            _history = history;
            _operationCount = history.Count;
            history.MarkAsReferenced(_operationCount);
        }

        /// <summary>
        ///  Gets condition-visible items from exactly the captured prefix.
        /// </summary>
        /// <returns>
        ///  The captured items, excluding false-condition items.
        /// </returns>
        public ICollection<I> GetMatchedItems() => _history.GetMatchedItems(_operationCount);
    }

    /// <summary>
    ///  Owns a contiguous operation history and caches only requested earlier states.
    /// </summary>
    /// <remarks>
    ///  Operations are appended but never replaced. A prefix and an exclusion context together
    ///  identify a saved result: a result pruned by later removals cannot satisfy an earlier
    ///  reference that needs those items. Working item storage remains independent of this history.
    /// </remarks>
    private sealed class ItemHistory
    {
        /// <summary>
        ///  The operations for one item type in declaration order.
        /// </summary>
        private readonly List<LazyItemOperation> _operations = [];

        /// <summary>
        ///  Prefixes that must be saved for a captured reference or current-state read.
        /// </summary>
        private HashSet<int>? _referencedPrefixes;

        /// <summary>
        ///  Saved item states keyed by prefix and the exclusions used to produce them.
        /// </summary>
        private Dictionary<(int Count, ImmutableHashSet<string> Exclusions), OrderedItemDataCollection>? _cache;

        /// <summary>
        ///  Gets the current exclusive end of the operation history.
        /// </summary>
        public int Count => _operations.Count;

        /// <summary>
        ///  Appends a fully constructed operation after its references have been captured.
        /// </summary>
        /// <param name="operation">The new operation.</param>
        public void Add(LazyItemOperation operation) => _operations.Add(operation);

        /// <summary>
        ///  Announces an earlier state that must survive later operations.
        /// </summary>
        /// <param name="count">The exclusive operation position identifying that state.</param>
        public void MarkAsReferenced(int count) => (_referencedPrefixes ??= []).Add(count);

        /// <summary>
        ///  Materializes condition-visible items at the requested prefix.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <returns>
        ///  The visible items in their insertion order.
        /// </returns>
        public ICollection<I> GetMatchedItems(int count)
        {
            var items = ImmutableList.CreateBuilder<I>();
            foreach (ItemData data in GetItemData(count, ImmutableHashSet<string>.Empty))
            {
                if (data.ConditionResult)
                {
                    items.Add(data.Item);
                }
            }
            return items.ToImmutable();
        }

        /// <summary>
        ///  Obtains a working view of a cached prefix or evaluates its uncached operations.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <param name="exclusions">Later removed globs that may prune this materialization.</param>
        /// <returns>
        ///  The ordered item state, including false-condition items.
        /// </returns>
        public OrderedItemDataCollection.Builder GetItemData(int count, ImmutableHashSet<string> exclusions)
        {
            if (TryGetCached(count, exclusions, out OrderedItemDataCollection? cached))
            {
                return cached!.ToBuilder();
            }

            MarkAsReferenced(count);
            return ComputeItems(count, exclusions);
        }

        /// <summary>
        ///  Finds a cached result without conflating differently pruned states.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <param name="exclusions">The applicable exclusion context.</param>
        /// <param name="items">The saved result, when found.</param>
        /// <returns>
        ///  Whether a compatible saved result exists.
        /// </returns>
        private bool TryGetCached(int count, ImmutableHashSet<string> exclusions, out OrderedItemDataCollection? items)
        {
            items = null;
            return _cache is not null && _cache.TryGetValue((count, exclusions), out items);
        }

        /// <summary>
        ///  Saves only a demanded prefix after all operations in that prefix have completed.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <param name="exclusions">The applicable exclusion context.</param>
        /// <param name="items">The working item state to save.</param>
        private void SaveResult(int count, ImmutableHashSet<string> exclusions, OrderedItemDataCollection.Builder items)
        {
            if (_referencedPrefixes?.Contains(count) == true)
            {
                (_cache ??= [])[(count, exclusions)] = items.ToImmutable();
            }
        }

        /// <summary>
        ///  Walks backwards to a compatible checkpoint, then applies the remaining operations forwards.
        /// </summary>
        /// <param name="count">The requested exclusive operation position.</param>
        /// <param name="exclusions">Later globs to suppress during earlier Includes.</param>
        /// <returns>
        ///  The materialized ordered item state.
        /// </returns>
        private OrderedItemDataCollection.Builder ComputeItems(int count, ImmutableHashSet<string> exclusions)
        {
            int start = 0;
            OrderedItemDataCollection.Builder? items = null;
            Stack<ImmutableHashSet<string>>? exclusionStack = null;

            for (int index = count - 1; index >= 0; index--)
            {
                ImmutableHashSet<string> current = exclusionStack?.Peek() ?? exclusions;
                if (TryGetCached(index + 1, current, out OrderedItemDataCollection? cached))
                {
                    items = cached!.ToBuilder();
                    start = index + 1;
                    break;
                }

                if (_operations[index] is RemoveOperation remove)
                {
                    var removedGlobs = remove.GetRemovedGlobs();
                    removedGlobs.UnionWith(current);
                    (exclusionStack ??= new()).Push(removedGlobs.ToImmutable());
                }
            }

            items ??= OrderedItemDataCollection.CreateBuilder();
            ImmutableHashSet<string> currentExclusions = exclusionStack?.Peek() ?? exclusions;
            var literalUpdates = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);

            for (int index = start; index < count; index++)
            {
                LazyItemOperation operation = _operations[index];
                if (operation is UpdateOperation update && TryAddToBatch(update, literalUpdates))
                {
                    if (_referencedPrefixes?.Contains(index + 1) == true)
                    {
                        ApplyBatch(literalUpdates, items);
                        SaveResult(index + 1, currentExclusions, items);
                    }
                    continue;
                }

                ApplyBatch(literalUpdates, items);
                if (operation is RemoveOperation)
                {
                    exclusionStack!.Pop();
                    currentExclusions = exclusionStack.Count == 0 ? exclusions : exclusionStack.Peek();
                }

                operation.Apply(items, currentExclusions);
                SaveResult(index + 1, currentExclusions, items);
            }

            ApplyBatch(literalUpdates, items);
            return items;
        }

        /// <summary>
        ///  Adds disjoint literal fragments to a batch, rolling back normalized keys on rejection.
        /// </summary>
        /// <param name="operation">The candidate Update.</param>
        /// <param name="batch">The pending updates keyed by normalized path.</param>
        /// <returns>
        ///  Whether the entire operation can join the batch.
        /// </returns>
        private static bool TryAddToBatch(UpdateOperation operation, Dictionary<string, UpdateOperation> batch)
        {
            int index;
            for (index = 0; index < operation.Spec.Fragments.Count; index++)
            {
                ItemSpecFragment fragment = operation.Spec.Fragments[index];
                if (MSBuildConstants.CharactersForExpansion.Any(fragment.TextFragment.Contains))
                {
                    break;
                }
                string key = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                if (batch.ContainsKey(key))
                {
                    break;
                }
                batch.Add(key, operation);
            }

            if (index == operation.Spec.Fragments.Count)
            {
                return true;
            }
            for (int added = 0; added < index; added++)
            {
                ItemSpecFragment fragment = operation.Spec.Fragments[added];
                batch.Remove(FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory));
            }
            return false;
        }

        /// <summary>
        ///  Applies disjoint literal updates with one item scan, then clears the batch.
        /// </summary>
        /// <param name="batch">The pending updates keyed by normalized path.</param>
        /// <param name="items">The working ordered item state.</param>
        private static void ApplyBatch(Dictionary<string, UpdateOperation> batch, OrderedItemDataCollection.Builder items)
        {
            if (batch.Count == 0)
            {
                return;
            }
            for (int index = 0; index < items.Count; index++)
            {
                ItemData item = items[index];
                string key = FileUtilities.NormalizePathForComparisonNoThrow(item.Item.EvaluatedInclude, item.Item.ProjectDirectory);
                if (batch.TryGetValue(key, out UpdateOperation? operation))
                {
                    items[index] = operation.UpdateItem(item);
                }
            }
            batch.Clear();
        }
    }
}
