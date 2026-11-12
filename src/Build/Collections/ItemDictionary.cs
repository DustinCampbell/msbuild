// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Collections;

/// <summary>
///  Provides collection-expression construction for item dictionaries.
/// </summary>
internal static class ItemDictionary
{
    /// <summary>
    ///  Creates an <see cref="ItemDictionary{T}"/> containing the specified items.
    /// </summary>
    /// <typeparam name="T">The type of item to store.</typeparam>
    /// <param name="items">The items to add to the dictionary.</param>
    /// <returns>
    ///  An item dictionary containing <paramref name="items"/>.
    /// </returns>
    internal static ItemDictionary<T> Create<T>(ReadOnlySpan<T> items)
        where T : class, IKeyed, IItem
    {
        ItemDictionary<T> result = new();

        if (items.Length > 0)
        {
            result.ImportItems(items);
        }

        return result;
    }
}
