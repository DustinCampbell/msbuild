// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

internal abstract partial class CompactDictionary<TKey, TValue>
{
    public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly TKey[] _keys;
        private readonly TValue[] _values;
        private int _index;

        internal Enumerator(TKey[] keys, TValue[] values)
        {
            _keys = keys;
            _values = values;
            _index = -1;
        }

        /// <inheritdoc cref="IEnumerator.MoveNext" />
        public bool MoveNext()
        {
            _index++;
            if ((uint)_index < (uint)_keys.Length)
            {
                return true;
            }

            _index = _keys.Length;
            return false;
        }

        /// <inheritdoc cref="IEnumerator{T}.Current" />
        public readonly KeyValuePair<TKey, TValue> Current
        {
            get
            {
                if ((uint)_index >= (uint)_keys.Length)
                {
                    throw new InvalidOperationException();
                }

                return new KeyValuePair<TKey, TValue>(_keys[_index], _values[_index]);
            }
        }

        /// <inheritdoc />
        readonly object IEnumerator.Current => Current;

        /// <inheritdoc />
        void IEnumerator.Reset() => _index = -1;

        /// <inheritdoc />
        readonly void IDisposable.Dispose()
        {
        }
    }
}
