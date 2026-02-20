// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build.Collections;

/// <summary>
/// A read-only wrapper over an empty collection.
/// </summary>
/// <typeparam name="T">Type of element in the collection.</typeparam>
internal sealed class ReadOnlyEmptyCollection<T> : ReadOnlyCollectionBase<T>
{
    public static ReadOnlyEmptyCollection<T> Instance => field ??= new();

    private ReadOnlyEmptyCollection()
    {
    }

    public override int Count => 0;

    public override bool Contains(T item)
        => false;

    public override void CopyTo(T[] array, int arrayIndex)
    {
    }

    protected override void CopyTo(Array array, int index)
    {
    }

    public override IEnumerator<T> GetEnumerator()
        => Enumerator.Instance;

    private sealed class Enumerator : IEnumerator<T>
    {
        public static readonly Enumerator Instance = new();

        private Enumerator()
        {
        }

        public T Current
        {
            get
            {
                InvalidOperationException.Throw();
                return default;
            }
        }

        object IEnumerator.Current => Current!;

        public void Dispose()
        {
        }

        public bool MoveNext()
            => false;

        public void Reset()
        {
        }
    }
}
