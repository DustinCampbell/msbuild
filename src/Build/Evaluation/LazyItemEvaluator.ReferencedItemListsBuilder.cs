// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
    where P : class, IProperty, IEquatable<P>, IValued
    where I : class, IItem<M>, IMetadataTable
    where M : class, IMetadatum
    where D : class, IItemDefinition<M>
{
    private ref struct ReferencedItemListsBuilder
    {
        private readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator;
        private Dictionary<string, LazyItemList>? _overflow;

        private string? _key0;
        private LazyItemList? _value0;
        private string? _key1;
        private LazyItemList? _value1;
        private int _inlineCount;

        public ReferencedItemListsBuilder(LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            _lazyEvaluator = lazyEvaluator;
        }

        private static StringComparer ItemNameComparer
            => Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;

        public void Add(string itemType)
        {
            if (_lazyEvaluator._itemLists.TryGetValue(itemType, out LazyItemList? itemList))
            {
                itemList.MarkAsReferenced();
                AddEntry(itemType, itemList);
            }
        }

        private void AddEntry(string itemType, LazyItemList itemList)
        {
            if (_overflow != null)
            {
                _overflow[itemType] = itemList;
                return;
            }

            var comparer = ItemNameComparer;

            // Check for existing inline entries.
            if (_inlineCount > 0 && comparer.Equals(_key0, itemType))
            {
                _value0 = itemList;
                return;
            }

            if (_inlineCount > 1 && comparer.Equals(_key1, itemType))
            {
                _value1 = itemList;
                return;
            }

            switch (_inlineCount)
            {
                case 0:
                    _key0 = itemType;
                    _value0 = itemList;
                    _inlineCount = 1;
                    return;

                case 1:
                    _key1 = itemType;
                    _value1 = itemList;
                    _inlineCount = 2;
                    return;

                default:
                    // Spill to dictionary.
                    _overflow = new Dictionary<string, LazyItemList>(capacity: 4, ItemNameComparer)
                    {
                        [_key0!] = _value0!,
                        [_key1!] = _value1!,
                        [itemType] = itemList
                    };

                    _key0 = null;
                    _value0 = null;
                    _key1 = null;
                    _value1 = null;
                    return;
            }
        }

        public void Add(ExpressionShredder.ItemExpressionCapture match)
        {
            if (match.ItemType is { } itemType)
            {
                Add(itemType);
            }

            if (match.Captures is { } subMatches)
            {
                foreach (var subMatch in subMatches)
                {
                    Add(subMatch);
                }
            }
        }

        public void Add(ItemSpec<P, I> itemSpec)
        {
            foreach (ItemSpecFragment fragment in itemSpec.Fragments)
            {
                if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemExpression)
                {
                    Add(itemExpression.Capture);
                }
            }
        }

        public void Add(string expression, IElementLocation elementLocation)
        {
            if (expression.Length > 0 &&
                Expander<P, I>.TryExpandSingleItemVectorExpressionIntoExpressionCapture(
                    expression, ExpanderOptions.ExpandItems, elementLocation, out var match))
            {
                Add(match);
            }
        }

        public IReadOnlyDictionary<string, LazyItemList> Build()
        {
            if (_overflow != null)
            {
                return _overflow;
            }

            return _inlineCount switch
            {
                0 => FrozenDictionary<string, LazyItemList>.Empty,
                1 => new SmallReadOnlyDictionary([_key0!], [_value0!], ItemNameComparer),
                2 => new SmallReadOnlyDictionary([_key0!, _key1!], [_value0!, _value1!], ItemNameComparer),
                _ => throw new InvalidOperationException(),
            };
        }

        private sealed class SmallReadOnlyDictionary : IReadOnlyDictionary<string, LazyItemList>
        {
            private readonly string[] _keys;
            private readonly LazyItemList[] _values;
            private readonly StringComparer _comparer;

            public SmallReadOnlyDictionary(string[] keys, LazyItemList[] values, StringComparer comparer)
            {
                _keys = keys;
                _values = values;
                _comparer = comparer;
            }

            public int Count => _keys.Length;

            public LazyItemList this[string key] =>
                TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();

            public IEnumerable<string> Keys => _keys;

            public IEnumerable<LazyItemList> Values => _values;

            public bool ContainsKey(string key) => TryGetValue(key, out _);

            public bool TryGetValue(string key, out LazyItemList value)
            {
                for (int i = 0; i < _keys.Length; i++)
                {
                    if (_comparer.Equals(key, _keys[i]))
                    {
                        value = _values[i];
                        return true;
                    }
                }

                value = null!;
                return false;
            }

            public IEnumerator<KeyValuePair<string, LazyItemList>> GetEnumerator()
            {
                for (int i = 0; i < _keys.Length; i++)
                {
                    yield return new KeyValuePair<string, LazyItemList>(_keys[i], _values[i]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();
        }
    }
}
