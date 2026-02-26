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
        private Dictionary<string, ItemListRef>? _overflow;

        private string? _key0;
        private ItemListRef _value0;
        private string? _key1;
        private ItemListRef _value1;
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
            if (_lazyEvaluator._itemLists.TryGetValue(itemType, out ItemOperationList? itemList))
            {
                int count = itemList.Count;
                itemList.MarkAsReferenced(count);
                AddEntry(itemType, new ItemListRef(itemList, count));
            }
        }

        private void AddEntry(string itemType, ItemListRef itemRef)
        {
            if (_overflow != null)
            {
                _overflow[itemType] = itemRef;
                return;
            }

            var comparer = ItemNameComparer;

            if (_inlineCount > 0 && comparer.Equals(_key0, itemType))
            {
                _value0 = itemRef;
                return;
            }

            if (_inlineCount > 1 && comparer.Equals(_key1, itemType))
            {
                _value1 = itemRef;
                return;
            }

            switch (_inlineCount)
            {
                case 0:
                    _key0 = itemType;
                    _value0 = itemRef;
                    _inlineCount = 1;
                    return;

                case 1:
                    _key1 = itemType;
                    _value1 = itemRef;
                    _inlineCount = 2;
                    return;

                default:
                    _overflow = new Dictionary<string, ItemListRef>(capacity: 4, ItemNameComparer)
                    {
                        [_key0!] = _value0,
                        [_key1!] = _value1,
                        [itemType] = itemRef
                    };

                    _key0 = null;
                    _value0 = default;
                    _key1 = null;
                    _value1 = default;
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

        public IReadOnlyDictionary<string, ItemListRef> Build()
        {
            if (_overflow != null)
            {
                return _overflow;
            }

            return _inlineCount switch
            {
                0 => FrozenDictionary<string, ItemListRef>.Empty,
                1 => new SmallReadOnlyDictionary([_key0!], [_value0], ItemNameComparer),
                2 => new SmallReadOnlyDictionary([_key0!, _key1!], [_value0, _value1], ItemNameComparer),
                _ => throw new InvalidOperationException(),
            };
        }

        private sealed class SmallReadOnlyDictionary : IReadOnlyDictionary<string, ItemListRef>
        {
            private readonly string[] _keys;
            private readonly ItemListRef[] _values;
            private readonly StringComparer _comparer;

            public SmallReadOnlyDictionary(string[] keys, ItemListRef[] values, StringComparer comparer)
            {
                _keys = keys;
                _values = values;
                _comparer = comparer;
            }

            public int Count => _keys.Length;

            public ItemListRef this[string key] =>
                TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();

            public IEnumerable<string> Keys => _keys;

            public IEnumerable<ItemListRef> Values => _values;

            public bool ContainsKey(string key) => TryGetValue(key, out _);

            public bool TryGetValue(string key, out ItemListRef value)
            {
                for (int i = 0; i < _keys.Length; i++)
                {
                    if (_comparer.Equals(key, _keys[i]))
                    {
                        value = _values[i];
                        return true;
                    }
                }

                value = default;
                return false;
            }

            public IEnumerator<KeyValuePair<string, ItemListRef>> GetEnumerator()
            {
                for (int i = 0; i < _keys.Length; i++)
                {
                    yield return new KeyValuePair<string, ItemListRef>(_keys[i], _values[i]);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
                => GetEnumerator();
        }
    }
}
