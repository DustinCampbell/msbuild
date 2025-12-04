// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Collections;

/// <summary>
///  A ref struct builder for arrays that uses pooled memory for efficient allocation.
///  This builder automatically grows as needed and returns memory to the pool when disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
internal ref struct RefArrayBuilder<T>
{
    private T[]? _arrayFromPool;
    private Span<T> _span;
    private int _count;

    /// <summary>
    ///  Initializes a new instance of the <see cref="RefArrayBuilder{T}"/> with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the builder.</param>
    public RefArrayBuilder(int initialCapacity)
    {
        Grow(initialCapacity);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RefArrayBuilder{T}"/> with the specified initial buffer.
    /// </summary>
    /// <param name="scratchBuffer">The initial buffer to use for storing elements.</param>
    public RefArrayBuilder(Span<T> scratchBuffer)
    {
        _span = scratchBuffer;
    }

    /// <summary>
    ///  Releases the pooled array back to the shared <see cref="ArrayPool{T}"/>.
    ///  This method can be called multiple times safely.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        T[]? toReturn = _arrayFromPool;

        if (toReturn != null)
        {
            _arrayFromPool = null;

            ReturnToPool(toReturn, _count);
        }
    }

    public readonly bool IsEmpty => _count == 0;

    /// <summary>
    ///  Gets or sets the number of elements in the builder.
    /// </summary>
    public int Count
    {
        readonly get => _count;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _span.Length);

            _count = value;
        }
    }

    /// <summary>
    ///  Gets a reference to the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to get.</param>
    /// <returns>A reference to the element at the specified index.</returns>
    public ref T this[int index]
    {
        get
        {
            Debug.Assert(index < _count);

            return ref _span[index];
        }
    }

    /// <summary>
    /// Returns a <see cref="Memory{T}"/> view of the elements in the builder.
    /// </summary>
    /// <returns>A memory view of the elements.</returns>
    public readonly Memory<T> AsMemory()
        => _arrayFromPool.AsMemory(0, _count);

    /// <summary>
    ///  Returns a <see cref="ReadOnlySpan{T}"/> view of the elements in the builder.
    /// </summary>
    /// <returns>A read-only span view of the elements.</returns>
    public readonly Span<T> AsSpan()
        => _span.Slice(0, _count);

    /// <summary>
    ///  Adds an item to the end of the builder. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="item">The item to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        int count = _count;
        Span<T> span = _span;

        if ((uint)count < (uint)span.Length)
        {
            span[count] = item;
            _count = count + 1;
        }
        else
        {
            AddWithResize(item);
        }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        Debug.Assert(_count == _span.Length);

        int count = _count;

        Grow(1);

        _span[count] = item;
        _count = count + 1;
    }

    /// <summary>
    ///  Adds a range of elements to the end of the builder. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="source">The span of elements to add.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(scoped ReadOnlySpan<T> source)
    {
        int count = _count;
        Span<T> span = _span;

        if (source.Length == 1 && (uint)count < (uint)span.Length)
        {
            span[count] = source[0];
            _count = count + 1;
        }
        else
        {
            AddRangeCore(source);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AddRangeCore(scoped ReadOnlySpan<T> source)
    {
        int count = _count;
        Span<T> span = _span;

        if ((uint)(count + source.Length) > (uint)span.Length)
        {
            Grow(span.Length - count + source.Length);
        }

        source.CopyTo(span.Slice(start: count));
        _count = count + source.Length;
    }

    /// <summary>
    ///  Inserts an item at the specified index, shifting subsequent elements. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the item.</param>
    /// <param name="item">The item to insert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Insert(int index, T item)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index <= _count);

        int count = _count;
        Span<T> span = _span;

        if ((uint)index < (uint)span.Length)
        {
            // Shift existing items
            int toCopy = count - index;
            span.Slice(index, toCopy).CopyTo(span.Slice(index + 1, toCopy));

            span[index] = item;
            _count = count + 1;
        }
        else
        {
            InsertWithResize(index, item);
        }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InsertWithResize(int index, T item)
    {
        Debug.Assert(_count == _span.Length);

        Grow(size: 1, startIndex: index);

        _span[index] = item;
        _count += 1;
    }

    /// <summary>
    ///  Inserts a range of elements at the specified index, shifting subsequent elements. The builder will automatically grow if needed.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the elements.</param>
    /// <param name="source">The span of elements to insert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InsertRange(int index, scoped ReadOnlySpan<T> source)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index <= _count);

        int count = _count;
        Span<T> span = _span;

        if ((uint)(index + source.Length) < (uint)span.Length)
        {
            // Shift existing items
            int toCopy = count - index;
            span.Slice(index, toCopy).CopyTo(span.Slice(index + source.Length, toCopy));

            source.CopyTo(span.Slice(index));
            _count = count + source.Length;
        }
        else
        {
            InsertRangeCore(index, source);
        }
    }

    // Hide uncommon path
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InsertRangeCore(int index, scoped ReadOnlySpan<T> source)
    {
        int count = _count;
        Span<T> span = _span;

        if ((uint)(index + source.Length) > (uint)span.Length)
        {
            Grow(size: span.Length - count + source.Length, startIndex: index);
        }

        source.CopyTo(span.Slice(index, source.Length));
        _count = count + source.Length;
    }

    /// <summary>
    ///  Removes the element at the specified index, shifting subsequent elements.
    /// </summary>
    /// <param name="index">The zero-based index of the element to remove.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RemoveAt(int index)
    {
        Debug.Assert(index >= 0);
        Debug.Assert(index < _count);

        int count = _count;
        Span<T> span = _span;

        // Shift subsequent elements down by one
        int toCopy = count - index - 1;
        if (toCopy > 0)
        {
            span.Slice(index + 1, toCopy).CopyTo(span.Slice(index, toCopy));
        }

        // Clear the last element if it contains references
#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            span[count - 1] = default!;
        }
#else
        if (!typeof(T).IsPrimitive)
        {
            span[count - 1] = default!;
        }
#endif

        _count = count - 1;
    }

    private void Grow(int size = 1, int startIndex = -1)
    {
        Debug.Assert(startIndex >= -1);
        Debug.Assert(startIndex <= _count);

        const int ArrayMaxLength = 0x7FFFFFC7; // Same as Array.MaxLength;

        // Double the size of the span.  If it's currently empty, default to size 4,
        // although it'll be increased in Rent to the pool's minimum bucket size.
        int nextCapacity = Math.Max(
            val1: _span.Length != 0 ? _span.Length * 2 : 4,
            val2: _span.Length + size);

        // If the computed doubled capacity exceeds the possible length of an array, then we
        // want to downgrade to either the maximum array length if that's large enough to hold
        // an additional item, or the current length + 1 if it's larger than the max length, in
        // which case it'll result in an OOM when calling Rent below.  In the exceedingly rare
        // case where _span.Length is already int.MaxValue (in which case it couldn't be a managed
        // array), just use that same value again and let it OOM in Rent as well.
        if ((uint)nextCapacity > ArrayMaxLength)
        {
            nextCapacity = Math.Max(Math.Max(_span.Length + 1, ArrayMaxLength), _span.Length);
        }

        T[] newArray = ArrayPool<T>.Shared.Rent(nextCapacity);

        if (startIndex == -1)
        {
            _span.CopyTo(newArray);
        }
        else
        {
            var destination = newArray.AsSpan();

            if (startIndex > 0)
            {
                _span.Slice(0, startIndex).CopyTo(destination);
            }

            _span.Slice(startIndex).CopyTo(destination.Slice(startIndex + size));
        }

        T[]? toReturn = _arrayFromPool;
        _span = newArray;
        _arrayFromPool = newArray;

        if (toReturn != null)
        {
            ReturnToPool(toReturn, _count);
        }
    }

    private static void ReturnToPool(T[] toReturn, int count)
    {
#if NET
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            toReturn.AsSpan(0, count).Clear();
        }
#else
        if (!typeof(T).IsPrimitive)
        {
            Array.Clear(toReturn, 0, count);
        }
#endif

        ArrayPool<T>.Shared.Return(toReturn);
    }

    /// <summary>
    ///  Creates an <see cref="ImmutableArray{T}"/> containing a copy of the elements in the builder.
    /// </summary>
    /// <returns>An immutable array containing the elements.</returns>
    public readonly ImmutableArray<T> ToImmutable()
        => ImmutableArray.Create(AsSpan());
}
