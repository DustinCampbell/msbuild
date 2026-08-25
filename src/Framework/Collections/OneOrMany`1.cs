// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Collections;

/// <summary>
///  Stores the first value inline and any additional values in an <see cref="ImmutableArray{T}"/>.
/// </summary>
/// <typeparam name="T">The type of value to store.</typeparam>
[CollectionBuilder(typeof(OneOrMany), nameof(OneOrMany.Create))]
internal readonly partial struct OneOrMany<T>
{
    private readonly T? _item;

    // default = zero, initialized empty = one inline item, non-empty = inline first item plus additional items.
    private readonly ImmutableArray<T> _items;

    /// <summary>
    ///  Initializes a collection containing one value.
    /// </summary>
    /// <param name="item">The value to store.</param>
    public OneOrMany(T item)
    {
        _item = item;
        _items = [];
    }

    /// <summary>
    ///  Initializes a collection from an immutable array.
    /// </summary>
    /// <remarks>
    ///  Arrays containing zero or one value are stored in their inline forms.
    /// </remarks>
    /// <param name="items">The values to store.</param>
    public OneOrMany(ImmutableArray<T> items)
    {
        if (items.IsDefaultOrEmpty)
        {
            _item = default;
            _items = default;
        }
        else if (items.Length == 1)
        {
            _item = items[0];
            _items = [];
        }
        else
        {
            _item = items[0];
            _items = ImmutableArray.Create(items.AsSpan(1, items.Length - 1));
        }
    }

    public OneOrMany(T firstItem, ReadOnlySpan<T> additionalItems)
    {
        _item = firstItem;
        _items = ImmutableArray.Create(additionalItems);
    }

    /// <summary>
    ///  Gets the number of stored values.
    /// </summary>
    public int Count
        => _items.IsDefault ? 0 : _items.Length + 1;

    /// <summary>
    ///  Gets a value indicating whether the collection is empty.
    /// </summary>
    public bool IsEmpty
        => _items.IsDefault;

    /// <summary>
    ///  Gets the value at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the value to get.</param>
    /// <returns>
    ///  The value at <paramref name="index"/>.
    /// </returns>
    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

            return index == 0 ? _item! : _items[index - 1];
        }
    }

    /// <summary>
    ///  Gets a read-only reference to the value at a caller-validated index.
    /// </summary>
    /// <param name="values">The collection containing the value.</param>
    /// <param name="index">The zero-based index of the value to get.</param>
    /// <returns>
    ///  A read-only reference to the value at <paramref name="index"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T ItemRef(in OneOrMany<T> values, int index)
        => ref index == 0
            ? ref values._item!
            : ref values._items.ItemRef(index - 1);

    /// <summary>
    ///  Returns an allocation-free enumerator over the stored values.
    /// </summary>
    public Enumerator GetEnumerator()
        => new(this);

    /// <summary>
    ///  Enumerates the stored values.
    /// </summary>
    public struct Enumerator(OneOrMany<T> values)
    {
        private readonly OneOrMany<T> _values = values;
        private int _index = -1;

        /// <summary>
        ///  Gets the current value.
        /// </summary>
        public readonly T Current
            => _values[_index];

        /// <summary>
        ///  Advances to the next value.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> when another value is available; otherwise, <see langword="false"/>.
        /// </returns>
        public bool MoveNext()
        {
            int nextIndex = _index + 1;
            if (nextIndex >= _values.Count)
            {
                return false;
            }

            _index = nextIndex;
            return true;
        }
    }
}
