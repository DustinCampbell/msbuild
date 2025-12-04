// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

/// <summary>
///  A ref struct stack that uses pooled memory for efficient allocation.
///  This stack automatically grows as needed and returns memory to the pool when disposed.
/// </summary>
/// <typeparam name="T">The type of elements in the stack.</typeparam>
internal ref struct RefStack<T>
{
    private RefArrayBuilder<T> _builder;

    /// <summary>
    ///  Gets a value indicating whether the stack is empty.
    /// </summary>
    public readonly bool IsEmpty => _builder.Count == 0;

    /// <summary>
    ///  Initializes a new instance of the <see cref="RefStack{T}"/> with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the stack.</param>
    public RefStack(int initialCapacity)
    {
        _builder = new RefArrayBuilder<T>(initialCapacity);
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="RefStack{T}"/> with the specified scratch buffer.
    /// </summary>
    /// <param name="scratchBuffer">The initial buffer to use for storing elements.</param>
    public RefStack(Span<T> scratchBuffer)
    {
        _builder = new RefArrayBuilder<T>(scratchBuffer);
    }

    /// <summary>
    ///  Releases the pooled array back to the shared <see cref="ArrayPool{T}"/>.
    ///  This method can be called multiple times safely.
    /// </summary>
    public void Dispose()
    {
        _builder.Dispose();
    }

    /// <summary>
    ///  Pushes an item onto the top of the stack. The stack will automatically grow if needed.
    /// </summary>
    /// <param name="item">The item to push onto the stack.</param>
    public void Push(in T item)
    {
        _builder.Add(item);
    }

    /// <summary>
    ///  Removes and returns the item at the top of the stack.
    /// </summary>
    /// <returns>The item that was removed from the top of the stack.</returns>
    public T Pop()
    {
        int lastIndex = _builder.Count - 1;
        T item = _builder[lastIndex];
        _builder.RemoveAt(lastIndex);

        return item;
    }

    /// <summary>
    ///  Attempts to peek at the item at the top of the stack without removing it.
    /// </summary>
    /// <param name="item">
    ///  When this method returns, contains the item at the top of the stack if the
    ///  stack is not empty; otherwise, the default value for the type.
    /// </param>
    /// <returns><see langword="true"/> if the stack is not empty; otherwise, <see langword="false"/>.</returns>
    public readonly bool TryPeek([MaybeNullWhen(false)] out T item)
    {
        if (_builder.Count > 0)
        {
            item = _builder[_builder.Count - 1];
            return true;
        }

        item = default;
        return false;
    }
}
