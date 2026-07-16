// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
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
    ///  The new value referenced by <paramref name="target"/>. Note that this is nearly always
    ///  more useful than the usual return from <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>
    ///  because it saves another read to <paramref name="target"/>.
    /// </returns>
    public static T Initialize<T>([NotNull] ref T? target, T value)
        where T : class
    {
        Assumed.NotNull(value);
        return GetOrStore(ref target, value);
    }

    private static T GetOrStore<T>([NotNull] ref T? target, T value)
        where T : class
        => Interlocked.CompareExchange(ref target, value, null) ?? value;
}
