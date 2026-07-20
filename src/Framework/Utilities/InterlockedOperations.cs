// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Note: This code is leveraged from Roslyn here: https://github.com/dotnet/roslyn/blob/598a77aabb325b4c3ba5e0e8e8fbbdd9b1b19ed1/src/Compilers/Core/Portable/InternalUtilities/InterlockedOperations.cs

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Build.Framework.Utilities;

internal static class InterlockedOperations
{
    /// <summary>
    ///  Initialize the value referenced by <paramref name="target"/> in a thread-safe manner.
    ///  The value is changed to <paramref name="value"/> only if the current value is null.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <param name="target">Reference to the target location.</param>
    /// <param name="value">The value to use if the target is currently null.</param>
    /// <returns>
    ///  The new value referenced by <paramref name="target"/>. Note that this is
    ///  nearly always more useful than the usual return from <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>
    ///  because it saves another read to <paramref name="target"/>.
    /// </returns>
    public static T Initialize<T>([NotNull] ref T? target, T value)
        where T : class
    {
        Assumed.NotNull(value);
        return GetOrStore(ref target, value);
    }

    /// <summary>
    ///  Initialize the reference-type value referenced by <paramref name="target"/> in a thread-safe manner, using a
    ///  factory delegate that is only invoked when the value has not yet been initialized. A <see langword="null"/>
    ///  <paramref name="target"/> is considered uninitialized.
    /// </summary>
    /// <typeparam name="T">The reference type of the target value.</typeparam>
    /// <typeparam name="TState">The type of state passed to <paramref name="valueFactory"/>.</typeparam>
    /// <param name="target">Reference to the target location.</param>
    /// <param name="state">State passed to <paramref name="valueFactory"/>.</param>
    /// <param name="valueFactory">
    ///  A factory delegate to create a new instance of the target value. Note that this delegate may be called
    ///  more than once by multiple threads, but only one of those values will successfully be written to the target.
    /// </param>
    /// <returns>
    ///  The value referenced by <paramref name="target"/>.
    /// </returns>
    /// <remarks>
    ///  Prefer passing a <see langword="static"/> lambda for <paramref name="valueFactory"/> and supplying any
    ///  captured data via <paramref name="state"/> so that no closure is allocated on the already-initialized fast path.
    /// </remarks>
    public static T Initialize<T, TState>([NotNull] ref T? target, TState state, Func<TState, T> valueFactory)
        where T : class
        => Volatile.Read(ref target!) ?? GetOrStore(ref target, valueFactory(state));

    /// <summary>
    ///  Ensure that the given target value is initialized in a thread-safe manner. This overload supports the
    ///  initialization of value types, and reference type fields where <see langword="null"/> is considered an
    ///  initialized value.
    /// </summary>
    /// <typeparam name="T">The type of the target value.</typeparam>
    /// <param name="target">A target value box to initialize.</param>
    /// <param name="valueFactory">
    ///  A factory delegate to create a new instance of the target value. Note that this delegate may be called
    ///  more than once by multiple threads, but only one of those values will successfully be written to the target.
    /// </param>
    /// <returns>
    ///  The target value.
    /// </returns>
    public static T? Initialize<T>([NotNull] ref StrongBox<T?>? target, Func<T?> valueFactory)
    {
        var box = Volatile.Read(ref target!) ?? GetOrStore(ref target, new StrongBox<T?>(valueFactory()));
        return box.Value;
    }

    /// <summary>
    ///  Ensure that the given target value is initialized in a thread-safe manner. This overload supports the
    ///  initialization of value types, and reference type fields where <see langword="null"/> is considered an
    ///  initialized value.
    /// </summary>
    /// <typeparam name="T">The type of the target value.</typeparam>
    /// <typeparam name="TState">The type of state passed to <paramref name="valueFactory"/>.</typeparam>
    /// <param name="target">A target value box to initialize.</param>
    /// <param name="state">State passed to <paramref name="valueFactory"/>.</param>
    /// <param name="valueFactory">
    ///  A factory delegate to create a new instance of the target value. Note that this delegate may be called
    ///  more than once by multiple threads, but only one of those values will successfully be written to the target.
    /// </param>
    /// <returns>
    ///  The target value.
    /// </returns>
    /// <remarks>
    ///  Prefer passing a <see langword="static"/> lambda for <paramref name="valueFactory"/> and supplying any
    ///  captured data via <paramref name="state"/> so that no closure is allocated on the already-initialized fast path.
    /// </remarks>
    public static T? Initialize<T, TState>([NotNull] ref StrongBox<T?>? target, TState state, Func<TState, T?> valueFactory)
    {
        var box = Volatile.Read(ref target!) ?? GetOrStore(ref target, new StrongBox<T?>(valueFactory(state)));
        return box.Value;
    }

    private static T GetOrStore<T>([NotNull] ref T? target, T value)
        where T : class
        => Interlocked.CompareExchange(ref target, value, null) ?? value;
}
