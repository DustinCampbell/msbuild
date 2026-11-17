// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Collections;

internal static class PropertyDictionary
{
    /// <summary>
    /// Creates a new property dictionary from an enumerator of properties.
    /// </summary>
    /// <typeparam name="T">The type of property to store.</typeparam>
    /// <param name="properties">The enumerator of properties.</param>
    /// <returns>A new property dictionary containing the specified properties.</returns>
    public static PropertyDictionary<T> Create<T>(ReadOnlySpan<T> properties)
        where T : class, IKeyed, IValued, IEquatable<T>
    {
        PropertyDictionary<T> result = new(properties.Length);

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
