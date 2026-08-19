// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a set that stores its first two values inline and promotes to a <see cref="HashSet{T}"/>
///  only when that capacity is exceeded.
/// </summary>
/// <remarks>
///  This is a mutable value type. Mutating operations must be performed on a variable rather than a copy.
/// </remarks>
/// <typeparam name="T">The type of value stored in the set.</typeparam>
internal struct InlineSet<T> : IEnumerable<T>
    where T : notnull
{
    private readonly IEqualityComparer<T>? _comparer;
    private T _first;
    private T _second;
    private bool _hasFirst;
    private bool _hasSecond;
    private HashSet<T>? _many;

    /// <summary>
    ///  Initializes an empty set.
    /// </summary>
    /// <param name="comparer">The comparer used to identify duplicate values.</param>
    public InlineSet(IEqualityComparer<T> comparer)
    {
        _comparer = comparer;
        _first = default!;
        _second = default!;
        _hasFirst = false;
        _hasSecond = false;
        _many = null;
    }

    /// <summary>
    ///  Gets the number of distinct values.
    /// </summary>
    public readonly int Count => _many?.Count ?? (_hasSecond ? 2 : _hasFirst ? 1 : 0);

    /// <summary>
    ///  Adds a value if it is not already present.
    /// </summary>
    /// <param name="value">The value to add.</param>
    /// <returns>
    ///  <see langword="true"/> when the value was added; otherwise, <see langword="false"/>.
    /// </returns>
    public bool Add(T value)
    {
        if (!_hasFirst)
        {
            _first = value;
            _hasFirst = true;
            return true;
        }

        if (_many is not null)
        {
            return _many.Add(value);
        }

        IEqualityComparer<T> comparer = _comparer ?? EqualityComparer<T>.Default;
        if (comparer.Equals(_first, value))
        {
            return false;
        }

        if (!_hasSecond)
        {
            _second = value;
            _hasSecond = true;
            return true;
        }

        if (comparer.Equals(_second, value))
        {
            return false;
        }

        _many = new HashSet<T>(capacity: 3, comparer: comparer)
        {
            _first,
            _second,
            value,
        };

        return true;
    }

    /// <summary>
    ///  Returns an enumerator over the values.
    /// </summary>
    public readonly Enumerator GetEnumerator()
        => new(this);

    readonly IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => GetEnumerator();

    readonly IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    /// <summary>
    ///  Enumerates the inline values or the promoted hash set.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly InlineSet<T> _set;
        private HashSet<T>.Enumerator _manyEnumerator;
        private bool _yieldedFirst;
        private bool _yieldedSecond;

        internal Enumerator(InlineSet<T> set)
        {
            _set = set;
            _manyEnumerator = set._many?.GetEnumerator() ?? default;
            _yieldedFirst = false;
            _yieldedSecond = false;
            Current = default!;
        }

        /// <inheritdoc />
        public T Current { get; private set; }

        readonly object IEnumerator.Current => Current;

        /// <inheritdoc />
        public bool MoveNext()
        {
            if (_set._many is not null)
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
