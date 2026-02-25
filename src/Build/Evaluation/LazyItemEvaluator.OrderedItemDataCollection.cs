// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  A collection of ItemData that maintains insertion order and internally optimizes
    ///  some access patterns, e.g. bulk removal based on normalized item values.
    /// </summary>
    private sealed class OrderedItemDataCollection
    {
        /// <summary>
        ///  The list of items in the collection. Defines the enumeration order.
        ///  Not modified after construction.
        /// </summary>
        private readonly List<ItemData> _list;

        private OrderedItemDataCollection(List<ItemData> list)
        {
            _list = list;
        }

        /// <summary>
        ///  Creates a new mutable collection.
        /// </summary>
        public static Builder CreateBuilder()
            => new([]);

        /// <summary>
        ///  Creates a mutable copy of this collection. Changes made to the returned builder are
        ///  not reflected in this collection.
        /// </summary>
        public Builder ToBuilder()
            => new([.. _list]);

        /// <summary>
        ///  A mutable and enumerable version of <see cref="OrderedItemDataCollection"/>.
        /// </summary>
        internal sealed class Builder : IEnumerable<ItemData>
        {
            /// <summary>
            ///  The list of items in the collection. Defines the enumeration order.
            /// </summary>
            private readonly List<ItemData> _list;

            /// <summary>
            ///  A dictionary of items keyed by their normalized value.
            /// </summary>
            private Dictionary<string, ItemDataCollectionValue<I>>? _dictionaryBuilder;

            internal Builder(List<ItemData> list)
            {
                _list = list;
            }

            IEnumerator<ItemData> IEnumerable<ItemData>.GetEnumerator()
                => _list.GetEnumerator();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
                => _list.GetEnumerator();

            public int Count => _list.Count;

            public ItemData this[int index]
            {
                get => _list[index];

                set
                {
                    // Update the dictionary if it exists.
                    if (_dictionaryBuilder is { } dictionaryBuilder)
                    {
                        ItemData oldItemData = _list[index];
                        string oldNormalizedValue = oldItemData.NormalizedItemValue;
                        string newNormalizedValue = value.NormalizedItemValue;
                        if (!string.Equals(oldNormalizedValue, newNormalizedValue, StringComparison.OrdinalIgnoreCase))
                        {
                            // Normalized values are different - delete from the old entry and add to the new entry.
                            ItemDataCollectionValue<I> oldDictionaryEntry = dictionaryBuilder[oldNormalizedValue];
                            oldDictionaryEntry.Delete(oldItemData.Item);
                            if (oldDictionaryEntry.IsEmpty)
                            {
                                dictionaryBuilder.Remove(oldNormalizedValue);
                            }
                            else
                            {
                                dictionaryBuilder[oldNormalizedValue] = oldDictionaryEntry;
                            }

                            ItemDataCollectionValue<I> newDictionaryEntry = dictionaryBuilder[newNormalizedValue];
                            newDictionaryEntry.Add(value.Item);
                            dictionaryBuilder[newNormalizedValue] = newDictionaryEntry;
                        }
                        else
                        {
                            // Normalized values are the same - replace the item in the entry.
                            ItemDataCollectionValue<I> dictionaryEntry = dictionaryBuilder[newNormalizedValue];
                            dictionaryEntry.Replace(oldItemData.Item, value.Item);
                            dictionaryBuilder[newNormalizedValue] = dictionaryEntry;
                        }
                    }

                    _list[index] = value;
                }
            }

            /// <summary>
            ///  Gets or creates a dictionary keyed by normalized values.
            /// </summary>
            public Dictionary<string, ItemDataCollectionValue<I>> Dictionary
                => _dictionaryBuilder ??= CreateAndInitializeDictionary(_list);

            private Dictionary<string, ItemDataCollectionValue<I>> CreateAndInitializeDictionary(List<ItemData> list)
            {
                var result = new Dictionary<string, ItemDataCollectionValue<I>>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < list.Count; i++)
                {
                    ItemData itemData = list[i];
                    AddToDictionary(result, ref itemData);
                    list[i] = itemData;
                }

                return result;
            }

            public void Add(ItemData data)
            {
                AddToDictionary(ref data);
                _list.Add(data);
            }

            public void Clear()
            {
                _list.Clear();
                _dictionaryBuilder?.Clear();
            }

            /// <summary>
            /// Removes all items passed in a collection.
            /// </summary>
            public void RemoveAll(ICollection<I> itemsToRemove)
            {
                _list.RemoveAll(item => itemsToRemove.Contains(item.Item));

                // This is a rare operation, don't bother updating the dictionary for now. It will be recreated as needed.
                _dictionaryBuilder = null;
            }

            /// <summary>
            /// Removes all items whose normalized path is passed in a collection.
            /// </summary>
            public void RemoveAll(ICollection<string> itemPathsToRemove)
            {
                var dictionary = Dictionary;
                HashSet<I>? itemsToRemove = null;
                foreach (string itemValue in itemPathsToRemove)
                {
                    if (dictionary.TryGetValue(itemValue, out var multiItem))
                    {
                        foreach (I item in multiItem)
                        {
                            itemsToRemove ??= new HashSet<I>();
                            itemsToRemove.Add(item);
                        }

                        _dictionaryBuilder?.Remove(itemValue);
                    }
                }

                if (itemsToRemove is not null)
                {
                    _list.RemoveAll(item => itemsToRemove.Contains(item.Item));
                }
            }

            /// <summary>
            /// Creates a snapshot of this collection. Changes made to this builder are not reflected in the snapshot.
            /// </summary>
            public OrderedItemDataCollection ToImmutable()
                => new([.. _list]);

            private void AddToDictionary(ref ItemData itemData)
            {
                if (_dictionaryBuilder is null)
                {
                    return;
                }

                AddToDictionary(_dictionaryBuilder, ref itemData);
            }

            private static void AddToDictionary(Dictionary<string, ItemDataCollectionValue<I>> dictionaryBuilder, ref ItemData itemData)
            {
                string key = itemData.NormalizedItemValue;

                if (!dictionaryBuilder.TryGetValue(key, out var dictionaryValue))
                {
                    dictionaryValue = new ItemDataCollectionValue<I>(itemData.Item);
                }
                else
                {
                    dictionaryValue.Add(itemData.Item);
                }

                dictionaryBuilder[key] = dictionaryValue;
            }
        }
    }
}
