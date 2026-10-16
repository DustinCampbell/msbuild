// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Provides items to an <see cref="Expander{P, I}"/>.
/// </summary>
/// <typeparam name="T">The type of item provided.</typeparam>
internal interface IItemProvider<out T>
    where T : class, IItem
{
    /// <summary>
    ///  Gets the items with the specified item type.
    /// </summary>
    /// <param name="itemType">The item type.</param>
    /// <returns>
    ///  A read-only collection of matching items, or an empty collection if no items have the specified type.
    /// </returns>
    IReadOnlyCollection<T> GetItems(string itemType);
}
