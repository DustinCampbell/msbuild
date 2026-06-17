// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class CompactDictionary<TKey, TValue>
{
    /// <summary>
    ///  A ref struct builder for constructing a <see cref="CompactDictionary{TKey, TValue}"/>.
    /// </summary>
    public ref struct Builder(IEqualityComparer<TKey> comparer)
    {
        private TKey? _firstKey;
        private TValue? _firstValue;
        private TKey? _secondKey;
        private TValue? _secondValue;
        private Dictionary<TKey, TValue>? _dictionary;
        private int _count;

        public void Add(TKey key, TValue value)
        {
            switch (_count)
            {
                case 0:
                    _firstKey = key;
                    _firstValue = value;
                    _count = 1;
                    break;

                case 1:
                    _secondKey = key;
                    _secondValue = value;
                    _count = 2;
                    break;

                default:
                    if (_dictionary is null)
                    {
                        _dictionary = new Dictionary<TKey, TValue>(4, comparer)
                        {
                            { _firstKey!, _firstValue! },
                            { _secondKey!, _secondValue! },
                            { key, value },
                        };
                    }
                    else
                    {
                        _dictionary[key] = value;
                    }

                    _count = _dictionary.Count;
                    break;
            }
        }

        /// <summary>
        ///  Sets the value for the given key, adding or replacing as needed.
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            switch (_count)
            {
                case 0:
                    _firstKey = key;
                    _firstValue = value;
                    _count = 1;
                    break;

                case 1:
                    if (comparer.Equals(_firstKey!, key))
                    {
                        _firstValue = value;
                    }
                    else
                    {
                        _secondKey = key;
                        _secondValue = value;
                        _count = 2;
                    }

                    break;

                case 2:
                    if (comparer.Equals(_firstKey!, key))
                    {
                        _firstValue = value;
                    }
                    else if (comparer.Equals(_secondKey!, key))
                    {
                        _secondValue = value;
                    }
                    else
                    {
                        _dictionary = new Dictionary<TKey, TValue>(4, comparer)
                        {
                            { _firstKey!, _firstValue! },
                            { _secondKey!, _secondValue! },
                            { key, value },
                        };

                        _count = 3;
                    }

                    break;

                default:
                    _dictionary![key] = value;
                    _count = _dictionary.Count;
                    break;
            }
        }

        public readonly CompactDictionary<TKey, TValue> Build()
            => _count switch
            {
                0 => Empty,
                1 => new SingleEntry(_firstKey!, _firstValue!, comparer),
                2 => new TwoEntries(_firstKey!, _firstValue!, _secondKey!, _secondValue!, comparer),
                _ => new ThreeOrMoreEntries(_dictionary!)
            };
    }
}
