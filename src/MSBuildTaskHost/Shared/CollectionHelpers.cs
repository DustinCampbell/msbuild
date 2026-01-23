// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

#nullable disable

namespace Microsoft.Build.Shared;

/// <summary>
/// Utilities for collections.
/// </summary>
internal static class CollectionHelpers
{
    /// <summary>
    /// Extension method -- combines a TryGet with a check to see that the value is equal.
    /// </summary>
    internal static bool ContainsValueAndIsEqual(this Dictionary<string, string> dictionary, string key, string value, StringComparison comparer)
        => dictionary.TryGetValue(key, out string valueFromDictionary) &&
           string.Equals(value, valueFromDictionary, comparer);
}
