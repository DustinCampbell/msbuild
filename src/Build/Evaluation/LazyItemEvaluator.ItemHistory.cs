// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Identifies later removed globs as a bounded range in an append-only pattern list.
    /// </summary>
    private readonly struct GlobExclusions
    {
        /// <summary>
        ///  The history's append-only glob patterns.
        /// </summary>
        private readonly List<string>? _patterns;

        /// <summary>
        ///  The first applicable later removal.
        /// </summary>
        private readonly int _start;

        /// <summary>
        ///  Initializes a range without copying or constructing a persistent set.
        /// </summary>
        /// <param name="patterns">The append-only pattern list.</param>
        /// <param name="start">The first applicable pattern.</param>
        /// <param name="count">The number of applicable patterns.</param>
        public GlobExclusions(List<string> patterns, int start, int count)
        {
            _patterns = patterns;
            _start = start;
            Count = count;
        }

        /// <summary>
        ///  Gets the number of applicable patterns.
        /// </summary>
        public int Count { get; }

        /// <summary>
        ///  Adds applicable patterns to an Include's privately owned deduplication set.
        /// </summary>
        /// <param name="patterns">The Include's combined exclusion set.</param>
        public void AddTo(HashSet<string> patterns)
        {
            for (int index = 0; index < Count; index++)
            {
                patterns.Add(_patterns![_start + index]);
            }
        }
    }

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
        ///  True-condition removed glob patterns in operation order.
        /// </summary>
        private readonly List<string> _removedGlobs = [];

        /// <summary>
        ///  The exclusive removed-pattern end after each operation.
        /// </summary>
        private readonly List<int> _globEnds = [];

        /// <summary>
        ///  Prefixes that must be saved for a captured reference or current-state read.
        /// </summary>
        private HashSet<int>? _referencedPrefixes;

        /// <summary>
        ///  Prefixes retained by recorded expressions rather than only a completed current-state read.
        /// </summary>
        private HashSet<int>? _capturedPrefixes;

        /// <summary>
        ///  The most recent current-state checkpoint, kept to resume after additional operations.
        /// </summary>
        private int _latestCurrentPrefix;

        /// <summary>
        ///  Saved item states keyed by prefix and the exclusions used to produce them.
        /// </summary>
        private Dictionary<(int Count, int ExclusionEnd), OrderedItemDataCollection>? _cache;

        /// <summary>
        ///  Gets the current exclusive end of the operation history.
        /// </summary>
        public int Count => _operations.Count;

        /// <summary>
        ///  Appends a fully constructed operation after its references have been captured.
        /// </summary>
        /// <param name="operation">The new operation.</param>
        public void Add(LazyItemOperation operation)
        {
            _operations.Add(operation);
            if (operation is RemoveOperation remove)
            {
                remove.AppendRemovedGlobs(_removedGlobs);
            }
            _globEnds.Add(_removedGlobs.Count);
        }

        /// <summary>
        ///  Announces an earlier state that must survive later operations.
        /// </summary>
        /// <param name="count">The exclusive operation position identifying that state.</param>
        public void MarkAsReferenced(int count)
        {
            (_referencedPrefixes ??= []).Add(count);
            (_capturedPrefixes ??= []).Add(count);
        }

        /// <summary>
        ///  Materializes condition-visible items at the requested prefix.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <returns>
        ///  The visible items in their insertion order.
        /// </returns>
        public ICollection<I> GetMatchedItems(int count)
        {
            if (!TryGetCached(count, 0, out OrderedItemDataCollection? items))
            {
                GetItemData(count);
                items = _cache![(count, 0)];
            }
            RetirePreviousCurrentRead(count);
            return items!;
        }

        /// <summary>
        ///  Obtains a working view of a cached prefix or evaluates its uncached operations.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <returns>
        ///  The ordered item state, including false-condition items.
        /// </returns>
        public OrderedItemDataCollection.Builder GetItemData(int count)
        {
            OrderedItemDataCollection.Builder result;
            if (TryGetCached(count, 0, out OrderedItemDataCollection? cached))
            {
                result = cached!.ToBuilder();
            }
            else
            {
                (_referencedPrefixes ??= []).Add(count);
                result = ComputeItems(count);
            }
            RetirePreviousCurrentRead(count);
            return result;
        }

        /// <summary>
        ///  Releases an obsolete current-read checkpoint only after it has served as a resume point.
        /// </summary>
        /// <param name="count">The newly requested operation boundary.</param>
        /// <remarks>
        ///  Recorded references still retain their captured prefixes. Condition-only reads need
        ///  the latest resume checkpoint, not every historical copy of a repeatedly updated list.
        /// </remarks>
        private void RetirePreviousCurrentRead(int count)
        {
            if (count != Count || count == _latestCurrentPrefix)
            {
                return;
            }
            if (_latestCurrentPrefix > 0 && _capturedPrefixes?.Contains(_latestCurrentPrefix) != true)
            {
                _cache?.Remove((_latestCurrentPrefix, 0));
                _referencedPrefixes?.Remove(_latestCurrentPrefix);
            }
            _latestCurrentPrefix = count;
        }

        /// <summary>
        ///  Finds a cached result without conflating differently pruned states.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <param name="exclusionEnd">The later removal end, or zero for an unpruned saved prefix.</param>
        /// <param name="items">The saved result, when found.</param>
        /// <returns>
        ///  Whether a compatible saved result exists.
        /// </returns>
        private bool TryGetCached(int count, int exclusionEnd, out OrderedItemDataCollection? items)
        {
            items = null;
            return _cache is not null && _cache.TryGetValue((count, exclusionEnd), out items);
        }

        /// <summary>
        ///  Saves only a demanded prefix after all operations in that prefix have completed.
        /// </summary>
        /// <param name="count">The exclusive operation position.</param>
        /// <param name="exclusionEnd">The later removal end, or zero for an unpruned saved prefix.</param>
        /// <param name="items">The working item state to save.</param>
        private void SaveResult(int count, int exclusionEnd, OrderedItemDataCollection.Builder items)
        {
            if (_referencedPrefixes?.Contains(count) == true)
            {
                (_cache ??= [])[(count, exclusionEnd)] = items.ToImmutable();
            }
        }

        /// <summary>
        ///  Walks backwards to a compatible checkpoint, then applies the remaining operations forwards.
        /// </summary>
        /// <param name="count">The requested exclusive operation position.</param>
        /// <returns>
        ///  The materialized ordered item state.
        /// </returns>
        private OrderedItemDataCollection.Builder ComputeItems(int count)
        {
            int start = 0;
            int globEnd = GetGlobEnd(count);
            OrderedItemDataCollection.Builder? items = null;

            for (int index = count - 1; index >= 0; index--)
            {
                int exclusionKey = GetExclusionKey(index + 1, globEnd);
                if (TryGetCached(index + 1, exclusionKey, out OrderedItemDataCollection? cached))
                {
                    items = cached!.ToBuilder();
                    start = index + 1;
                    break;
                }
            }

            items ??= OrderedItemDataCollection.CreateBuilder();
            var literalUpdates = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);

            for (int index = start; index < count; index++)
            {
                LazyItemOperation operation = _operations[index];
                int currentGlobStart = GetGlobEnd(index + 1);
                int exclusionKey = GetExclusionKey(index + 1, globEnd);
                if (operation is UpdateOperation update && TryAddToBatch(update, literalUpdates))
                {
                    if (_referencedPrefixes?.Contains(index + 1) == true)
                    {
                        ApplyBatch(literalUpdates, items);
                        SaveResult(index + 1, exclusionKey, items);
                    }
                    continue;
                }

                ApplyBatch(literalUpdates, items);
                operation.Apply(items, new GlobExclusions(_removedGlobs, currentGlobStart, globEnd - currentGlobStart));
                SaveResult(index + 1, exclusionKey, items);
            }

            ApplyBatch(literalUpdates, items);
            SaveResult(count, 0, items);
            return items;
        }

        /// <summary>
        ///  Gets the removal-pattern position at an exclusive operation boundary.
        /// </summary>
        /// <param name="count">The exclusive operation boundary.</param>
        /// <returns>
        ///  The exclusive removed-pattern position.
        /// </returns>
        private int GetGlobEnd(int count) => count == 0 ? 0 : _globEnds[count - 1];

        /// <summary>
        ///  Canonicalizes the no-later-removal context while distinguishing pruned earlier prefixes.
        /// </summary>
        /// <param name="count">The saved operation boundary.</param>
        /// <param name="globEnd">The target materialization's removal-pattern end.</param>
        /// <returns>
        ///  Zero for an unpruned prefix, otherwise the stable removal-pattern end.
        /// </returns>
        private int GetExclusionKey(int count, int globEnd) => GetGlobEnd(count) == globEnd ? 0 : globEnd;

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
            string[]? keys = operation.GetLiteralKeys();
            if (keys is null)
            {
                return false;
            }
            foreach (string key in keys)
            {
                if (batch.ContainsKey(key))
                {
                    return false;
                }
            }
            foreach (string key in keys)
            {
                batch.Add(key, operation);
            }
            return true;
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
                string key = items.GetNormalizedValue(index);
                if (batch.TryGetValue(key, out UpdateOperation? operation))
                {
                    items[index] = operation.UpdateItem(item);
                }
            }
            batch.Clear();
        }
    }
}
