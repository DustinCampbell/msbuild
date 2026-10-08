// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Saves an ordered item prefix and supplies a read-only view of its condition-visible items.
    /// </summary>
    /// <remarks>
    ///  Appending items can share the backing list because saved views have fixed bounds. Replacing
    ///  or removing existing entries detaches the working prefix first. Item objects themselves must
    ///  still be cloned before metadata mutation; container ownership does not make items immutable.
    /// </remarks>
    internal sealed class OrderedItemDataCollection : ICollection<I>
    {
        /// <summary>
        ///  The backing list, which may acquire later items beyond this saved prefix.
        /// </summary>
        private readonly List<ItemData> _items;

        /// <summary>
        ///  The fixed number of entries belonging to this saved state.
        /// </summary>
        private readonly int _count;

        /// <summary>
        ///  Initializes a saved view of a working prefix.
        /// </summary>
        /// <param name="items">The shared backing list.</param>
        /// <param name="count">The fixed prefix length.</param>
        /// <param name="matchedCount">The number of condition-visible entries.</param>
        private OrderedItemDataCollection(List<ItemData> items, int count, int matchedCount)
        {
            _items = items;
            _count = count;
            Count = matchedCount;
        }

        /// <summary>
        ///  Gets the condition-visible item count without projecting or enumerating the prefix.
        /// </summary>
        public int Count { get; }

        /// <summary>
        ///  Gets whether this saved item view is read-only.
        /// </summary>
        public bool IsReadOnly => true;

        /// <summary>
        ///  Creates an empty working item collection.
        /// </summary>
        /// <returns>
        ///  The mutable collection.
        /// </returns>
        public static Builder CreateBuilder() => new([], 0, 0, shared: false);

        /// <summary>
        ///  Creates a working view that detaches before modifying entries in this saved prefix.
        /// </summary>
        /// <returns>
        ///  The mutable working view.
        /// </returns>
        public Builder ToBuilder() => new(_items, _count, Count, shared: true);

        /// <summary>
        ///  Tests membership among the condition-visible items.
        /// </summary>
        /// <param name="item">The item to find.</param>
        /// <returns>
        ///  Whether the item belongs to this view.
        /// </returns>
        public bool Contains(I item)
        {
            for (int index = 0; index < _count; index++)
            {
                ItemData data = _items[index];
                if (data.ConditionResult && EqualityComparer<I>.Default.Equals(data.Item, item))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        ///  Copies condition-visible items in their original order.
        /// </summary>
        /// <param name="array">The destination array.</param>
        /// <param name="arrayIndex">The first destination position.</param>
        public void CopyTo(I[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);
            if (arrayIndex < 0 || arrayIndex > array.Length - Count)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }
            for (int index = 0; index < _count; index++)
            {
                ItemData data = _items[index];
                if (data.ConditionResult)
                {
                    array[arrayIndex++] = data.Item;
                }
            }
        }

        /// <summary>
        ///  Enumerates condition-visible items within the saved bounds.
        /// </summary>
        /// <returns>
        ///  The ordered enumerator.
        /// </returns>
        public IEnumerator<I> GetEnumerator()
        {
            for (int index = 0; index < _count; index++)
            {
                ItemData data = _items[index];
                if (data.ConditionResult)
                {
                    yield return data.Item;
                }
            }
        }

        /// <summary>
        ///  Supplies the non-generic enumerator for the read-only view.
        /// </summary>
        /// <returns>
        ///  The ordered enumerator.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        ///  Rejects mutation through the read-only collection interface.
        /// </summary>
        /// <param name="item">The item that cannot be added.</param>
        void ICollection<I>.Add(I item) => throw new NotSupportedException();

        /// <summary>
        ///  Rejects clearing a saved view.
        /// </summary>
        void ICollection<I>.Clear() => throw new NotSupportedException();

        /// <summary>
        ///  Rejects removing an item from a saved view.
        /// </summary>
        /// <param name="item">The item that cannot be removed.</param>
        /// <returns>
        ///  This method always throws because the view is read-only.
        /// </returns>
        bool ICollection<I>.Remove(I item) => throw new NotSupportedException();

        /// <summary>
        ///  Maintains ordered mutable items and a lazily constructed normalized-value index.
        /// </summary>
        internal sealed class Builder : IEnumerable<ItemData>
        {
            /// <summary>
            ///  The backing list for the current working prefix.
            /// </summary>
            private List<ItemData> _items;

            /// <summary>
            ///  Whether existing entries are shared with a saved view or another working prefix.
            /// </summary>
            private bool _shared;

            /// <summary>
            ///  The number of entries with true originating conditions.
            /// </summary>
            private int _matchedCount;

            /// <summary>
            ///  The optional duplicate-aware normalized-value index.
            /// </summary>
            private Dictionary<string, ItemDataCollectionValue<I>>? _dictionary;

            /// <summary>
            ///  Initializes a working prefix with explicit ownership.
            /// </summary>
            /// <param name="items">The backing list.</param>
            /// <param name="count">The prefix length.</param>
            /// <param name="matchedCount">The number of condition-visible entries.</param>
            /// <param name="shared">Whether modifications must detach existing entries.</param>
            public Builder(List<ItemData> items, int count, int matchedCount, bool shared)
            {
                _items = items;
                Count = count;
                _matchedCount = matchedCount;
                _shared = shared;
            }

            /// <summary>
            ///  Gets the total number of entries, including false-condition items.
            /// </summary>
            public int Count { get; private set; }

            /// <summary>
            ///  Gets or replaces an entry without changing its ordered position.
            /// </summary>
            /// <param name="index">The entry position.</param>
            /// <returns>
            ///  The entry at that position.
            /// </returns>
            public ItemData this[int index]
            {
                get
                {
                    ValidateIndex(index);
                    return _items[index];
                }
                set
                {
                    ValidateIndex(index);
                    EnsureWritable();
                    ItemData previous = _items[index];
                    if (_dictionary is not null)
                    {
                        string oldKey = previous.NormalizedItemValue;
                        string newKey = value.NormalizedItemValue;
                        ItemDataCollectionValue<I> oldEntry = _dictionary[oldKey];
                        if (string.Equals(oldKey, newKey, StringComparison.OrdinalIgnoreCase))
                        {
                            oldEntry.Replace(previous.Item, value.Item);
                            _dictionary[oldKey] = oldEntry;
                        }
                        else
                        {
                            oldEntry.Delete(previous.Item);
                            if (oldEntry.IsEmpty)
                            {
                                _dictionary.Remove(oldKey);
                            }
                            else
                            {
                                _dictionary[oldKey] = oldEntry;
                            }
                            AddToDictionary(ref value);
                        }
                    }
                    _matchedCount += (value.ConditionResult ? 1 : 0) - (previous.ConditionResult ? 1 : 0);
                    _items[index] = value;
                }
            }

            /// <summary>
            ///  Gets a normalized path and memoizes it in the stored entry without semantic mutation.
            /// </summary>
            /// <param name="index">The entry position.</param>
            /// <returns>
            ///  The lazily normalized item value.
            /// </returns>
            public string GetNormalizedValue(int index)
            {
                ValidateIndex(index);
                ItemData data = _items[index];
                string key = data.NormalizedItemValue;
                _items[index] = data;
                return key;
            }

            /// <summary>
            ///  Gets or creates the normalized-value index and persists normalization in entries.
            /// </summary>
            public Dictionary<string, ItemDataCollectionValue<I>> Dictionary
            {
                get
                {
                    if (_dictionary is null)
                    {
                        _dictionary = new(StringComparer.OrdinalIgnoreCase);
                        for (int index = 0; index < Count; index++)
                        {
                            ItemData data = _items[index];
                            AddToDictionary(ref data);
                            // Only memoized normalization changes; saved semantic item data is unchanged.
                            _items[index] = data;
                        }
                    }
                    return _dictionary;
                }
            }

            /// <summary>
            ///  Appends an item, sharing earlier prefixes when their bounds remain unchanged.
            /// </summary>
            /// <param name="data">The new entry.</param>
            public void Add(ItemData data)
            {
                if (Count != _items.Count)
                {
                    EnsureWritable();
                }
                if (_dictionary is not null)
                {
                    AddToDictionary(ref data);
                }
                _items.Add(data);
                Count++;
                _matchedCount += data.ConditionResult ? 1 : 0;
            }

            /// <summary>
            ///  Clears the working collection without changing saved prefixes.
            /// </summary>
            public void Clear()
            {
                _items = [];
                Count = 0;
                _matchedCount = 0;
                _shared = false;
                _dictionary?.Clear();
            }

            /// <summary>
            ///  Removes matching items with stable compaction.
            /// </summary>
            /// <param name="itemsToRemove">The items to remove.</param>
            public void RemoveAll(ICollection<I> itemsToRemove)
            {
                EnsureWritable();
                int destination = 0;
                int matched = 0;
                for (int index = 0; index < Count; index++)
                {
                    ItemData data = _items[index];
                    if (!itemsToRemove.Contains(data.Item))
                    {
                        _items[destination++] = data;
                        matched += data.ConditionResult ? 1 : 0;
                    }
                }
                _items.RemoveRange(destination, Count - destination);
                Count = destination;
                _matchedCount = matched;
                _dictionary = null;
            }

            /// <summary>
            ///  Removes all duplicate items under the supplied normalized paths.
            /// </summary>
            /// <param name="paths">The normalized paths to remove.</param>
            public void RemoveAll(ICollection<string> paths)
            {
                Dictionary<string, ItemDataCollectionValue<I>> dictionary = Dictionary;
                HashSet<I>? removed = null;
                foreach (string path in paths)
                {
                    if (dictionary.TryGetValue(path, out ItemDataCollectionValue<I> entries))
                    {
                        foreach (I item in entries)
                        {
                            (removed ??= []).Add(item);
                        }
                    }
                }
                if (removed is not null)
                {
                    RemoveAll(removed);
                }
            }

            /// <summary>
            ///  Saves a bounded read-only view without copying an append-only item prefix.
            /// </summary>
            /// <returns>
            ///  The saved view.
            /// </returns>
            public OrderedItemDataCollection ToImmutable()
            {
                _shared = true;
                return new(_items, Count, _matchedCount);
            }

            /// <summary>
            ///  Enumerates all entries in the working prefix.
            /// </summary>
            /// <returns>
            ///  The ordered entry enumerator.
            /// </returns>
            public IEnumerator<ItemData> GetEnumerator()
            {
                for (int index = 0; index < Count; index++)
                {
                    yield return _items[index];
                }
            }

            /// <summary>
            ///  Supplies the non-generic working-prefix enumerator.
            /// </summary>
            /// <returns>
            ///  The ordered entry enumerator.
            /// </returns>
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            ///  Detaches a shared prefix before replacing or compacting its existing entries.
            /// </summary>
            private void EnsureWritable()
            {
                if (_shared)
                {
                    var items = new List<ItemData>(Count);
                    for (int index = 0; index < Count; index++)
                    {
                        items.Add(_items[index]);
                    }
                    _items = items;
                    _shared = false;
                }
            }

            /// <summary>
            ///  Rejects access beyond the working prefix even when the backing list has a longer tail.
            /// </summary>
            /// <param name="index">The requested position.</param>
            private void ValidateIndex(int index)
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }

            /// <summary>
            ///  Adds an item to the duplicate-aware normalized-value index.
            /// </summary>
            /// <param name="data">The entry whose normalized value is memoized.</param>
            private void AddToDictionary(ref ItemData data)
            {
                string key = data.NormalizedItemValue;
                if (_dictionary!.TryGetValue(key, out ItemDataCollectionValue<I> entry))
                {
                    entry.Add(data.Item);
                }
                else
                {
                    entry = new(data.Item);
                }
                _dictionary[key] = entry;
            }
        }
    }
}
