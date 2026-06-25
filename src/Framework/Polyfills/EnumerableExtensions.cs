// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Build;

internal static class EnumerableExtensions
{
    /// <summary>
    ///  Attempts to determine the number of elements in a sequence without forcing an enumeration.
    /// </summary>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
    /// <param name="source">A sequence that contains elements to be counted.</param>
    /// <param name="count">
    ///  When this method returns, contains the number of elements in <paramref name="source"/>,
    ///  or 0 if the count couldn't be determined without enumeration.</param>
    /// <returns>
    ///  <see langword="true"/> if the number of elements in the sequence could be determined without
    ///  enumeration; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetCount<TSource>(this IEnumerable<TSource> source, out int count)
    {
#if NET6_0_OR_GREATER
        // Note: TryGetNonEnumeratedCount doesn't test for IReadOnlyCollection<T>.
        // So, it returns false for IReadOnlyList<T>.
        if (source is IReadOnlyCollection<TSource> collection)
        {
            count = collection.Count;
            return true;
        }

        return System.Linq.Enumerable.TryGetNonEnumeratedCount(source, out count);
#else
        return TryGetCount<TSource>((IEnumerable)source, out count);
#endif
    }

    /// <inheritdoc cref="TryGetCount{TSource}(IEnumerable{TSource}, out int)"/>
    public static bool TryGetCount<TSource>(this IEnumerable source, out int count)
    {
        switch (source)
        {
            case ICollection collection:
                count = collection.Count;
                return true;

            case ICollection<TSource> collection:
                count = collection.Count;
                return true;

            case IReadOnlyCollection<TSource> collection:
                count = collection.Count;
                return true;
        }

        count = 0;
        return false;
    }
}
