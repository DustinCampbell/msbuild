// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Text;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wrapper of two tables for a convenient method return value.
    /// </summary>
    internal struct ItemsAndMetadataPair
    {
        /// <summary>
        /// The item set
        /// </summary>
        private ReferenceSet<StringSegment> _items;

        /// <summary>
        /// The metadata dictionary.
        /// The key is the possibly qualified metadata name, for example
        /// "EmbeddedResource.Culture" or "Culture"
        /// </summary>
        private ReferenceSet<MetadataReference> _metadata;

        /// <summary>
        /// Constructs a pair from an item set and metadata
        /// </summary>
        /// <param name="items">The item set</param>
        /// <param name="metadata">The metadata dictionary</param>
        internal ItemsAndMetadataPair(
            ReferenceSet<StringSegment> items,
            ReferenceSet<MetadataReference> metadata)
        {
            _items = items;
            _metadata = metadata;
        }

        /// <summary>
        /// Gets or sets the item set
        /// </summary>
        internal ReferenceSet<StringSegment> Items
        {
            readonly get
            {
                return _items;
            }

            set
            {
                _items = value;
            }
        }

        /// <summary>
        /// Gets or sets the metadata dictionary
        /// The key is the possibly qualified metadata name, for example
        /// "EmbeddedResource.Culture" or "Culture"
        /// </summary>
        internal ReferenceSet<MetadataReference> Metadata
        {
            readonly get
            {
                return _metadata;
            }

            set
            {
                _metadata = value;
            }
        }
    }

    /// <summary>
    ///  Stores up to two references inline and promotes to a hash set only when that inline
    ///  capacity is exceeded.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    internal sealed class ReferenceSet<T> : IEnumerable<T>
    {
        private readonly IEqualityComparer<T> _comparer;
        private T _first;
        private T _second;
        private bool _hasFirst;
        private bool _hasSecond;
        private HashSet<T> _many;

        /// <summary>
        ///  Initializes an empty set.
        /// </summary>
        /// <param name="comparer">The comparer used to identify duplicate references.</param>
        public ReferenceSet(IEqualityComparer<T> comparer)
        {
            _comparer = comparer;
        }

        /// <summary>
        ///  Gets the number of distinct references.
        /// </summary>
        public int Count => _many?.Count ?? (_hasSecond ? 2 : _hasFirst ? 1 : 0);

        /// <summary>
        ///  Adds a reference if it is not already present.
        /// </summary>
        /// <param name="value">The reference to add.</param>
        /// <returns>
        ///  <see langword="true"/> when the reference was added; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Add(T value)
        {
            if (!_hasFirst)
            {
                _first = value;
                _hasFirst = true;
                return true;
            }

            if (_many != null)
            {
                return _many.Add(value);
            }

            if (_comparer.Equals(_first, value))
            {
                return false;
            }

                    if (!_hasSecond)
                    {
                        _second = value;
                        _hasSecond = true;
                        return true;
                    }

                    if (_comparer.Equals(_second, value))
                    {
                        return false;
                    }

                    _many = new HashSet<T>(capacity: 3, comparer: _comparer)
                    {
                        _first,
                        _second,
                        value,
                    };

            return true;
        }

        /// <summary>
        ///  Returns an enumerator over the references.
        /// </summary>
        public Enumerator GetEnumerator()
            => new(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();

        /// <summary>
        ///  Enumerates either the inline reference or the promoted hash set.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly ReferenceSet<T> _set;
            private HashSet<T>.Enumerator _manyEnumerator;
            private bool _yieldedFirst;
            private bool _yieldedSecond;

            internal Enumerator(ReferenceSet<T> set)
            {
                _set = set;
                _manyEnumerator = set._many?.GetEnumerator() ?? default;
                _yieldedFirst = false;
                _yieldedSecond = false;
                Current = default;
            }

            /// <inheritdoc />
            public T Current { get; private set; }

            object System.Collections.IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_set._many != null)
                {
                    if (_manyEnumerator.MoveNext())
                    {
                        Current = _manyEnumerator.Current;
                        return true;
                    }

                    return false;
                }

                if (!_yieldedFirst && _set._hasFirst)
                {
                    _yieldedFirst = true;
                    Current = _set._first;
                    return true;
                }

                if (!_yieldedSecond && _set._hasSecond)
                {
                    _yieldedSecond = true;
                    Current = _set._second;
                    return true;
                }

                return false;
            }

            /// <inheritdoc />
            public void Reset()
                => throw new NotSupportedException();

            /// <inheritdoc />
            public void Dispose()
                => _manyEnumerator.Dispose();
        }
    }
}
