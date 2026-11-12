// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Collections;

/// <summary>
///  Provides collection-expression construction for property dictionaries.
/// </summary>
internal static class PropertyDictionary
{
    /// <summary>
    ///  Creates a <see cref="PropertyDictionary{T}"/> containing the specified properties.
    /// </summary>
    /// <typeparam name="T">The type of property or metadata value to store.</typeparam>
    /// <param name="properties">The properties to add to the dictionary.</param>
    /// <returns>
    ///  A property dictionary containing <paramref name="properties"/>.
    /// </returns>
    internal static PropertyDictionary<T> Create<T>(ReadOnlySpan<T> properties)
        where T : class, IKeyed, IValued, IEquatable<T>
    {
        PropertyDictionary<T> result = new(capacity: properties.Length);

        if (properties.Length > 0)
        {
            foreach (T property in properties)
            {
                result.Set(property);
            }
        }

        return result;
    }
}
