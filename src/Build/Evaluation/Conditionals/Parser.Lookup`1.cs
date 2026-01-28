// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation;

internal ref partial struct Parser
{
    private readonly struct Lookup<T>(params IEnumerable<(string Key, T Value)> entries)
    {
        private readonly FrozenDictionary<string, T> _dictionary = entries
            .ToFrozenDictionary(
                keySelector: entry => entry.Key,
                elementSelector: entry => entry.Value,
                comparer: StringComparer.OrdinalIgnoreCase);

        public bool TryGetValue(ReadOnlySpan<char> span, [MaybeNullWhen(false)] out T value)
        {
#if NET
            return _dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(span, out value);
#else
            // The number of entries should be small enough that iterating through them
            // and comparing is more efficient than allocating a new string.
            foreach (KeyValuePair<string, T> pair in _dictionary)
            {
                if (span.Equals(pair.Key.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = default;
            return false;
#endif
        }
    }
}
