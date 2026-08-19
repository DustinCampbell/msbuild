// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Text;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wraps a table of metadata values in which keys
    /// may be qualified ("itemtype.name") or unqualified ("name").
    /// </summary>
    internal class StringMetadataTable : IMetadataTable
    {
        /// <summary>
        /// Table of metadata values.
        /// Each key may be qualified ("itemtype.name") or unqualified ("name").
        /// Unqualified are considered to apply to all item types.
        /// May be null, if empty.
        /// </summary>
        private Dictionary<string, string> _metadata;
#if NET
        private readonly Dictionary<string, string>.AlternateLookup<StringSegment> _metadataSegments;
        private readonly bool _hasSegmentLookup;
#endif

        /// <summary>
        /// Constructor taking a table of metadata in which keys
        /// may be a mixture of qualified ("itemtype.name") and unqualified ("name").
        /// Unqualified keys are considered to apply to all item types.
        /// Metadata may be null, indicating it is empty.
        /// </summary>
        internal StringMetadataTable(Dictionary<string, string> metadata)
        {
            _metadata = metadata;
#if NET
            if (metadata?.Comparer is IAlternateEqualityComparer<StringSegment, string>)
            {
                _metadataSegments = metadata.GetAlternateLookup<StringSegment>();
                _hasSegmentLookup = true;
            }
#endif
        }

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified.
        /// If no value is available, returns empty string.
        /// </summary>
        public string GetEscapedValue(string name)
        {
            return GetEscapedValue(null, name);
        }

        /// <inheritdoc />
        public string GetEscapedValue(StringSegment name)
            => GetEscapedValue(default, name);

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If no value is available, returns empty string.
        /// </summary>
        public string GetEscapedValue(string itemType, string name)
        {
            return GetEscapedValueIfPresent(itemType, name) ?? String.Empty;
        }

        /// <inheritdoc />
        public string GetEscapedValue(StringSegment itemType, StringSegment name)
            => GetEscapedValueIfPresent(itemType, name) ?? string.Empty;

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If no value is available, returns null.
        /// </summary>
        public string GetEscapedValueIfPresent(string itemType, string name)
        {
            if (_metadata == null)
            {
                return null;
            }

            string key;
            if (itemType == null)
            {
                key = name;
            }
            else
            {
                key = $"{itemType}.{name}";
            }

            string value;
            _metadata.TryGetValue(key, out value);

            return value;
        }

        /// <inheritdoc />
        public string GetEscapedValueIfPresent(StringSegment itemType, StringSegment name)
        {
            if (_metadata == null)
            {
                return null;
            }

#if NET
            if (!itemType.HasValue &&
                _hasSegmentLookup &&
                _metadataSegments.TryGetValue(name, out string value))
            {
                return value;
            }
#endif

            // Bucket metadata tables are intentionally small. Comparing their existing keys avoids
            // allocating a temporary "ItemType.Name" key and preserves live views over mutable tables.
            foreach (KeyValuePair<string, string> pair in _metadata)
            {
                ReadOnlySpan<char> key = pair.Key.AsSpan();
                if (!itemType.HasValue)
                {
                    if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    {
                        return pair.Value;
                    }
                }
                else if (key.Length == itemType.Length + name.Length + 1 &&
                         key[itemType.Length] == '.' &&
                         itemType.Equals(key[..itemType.Length], StringComparison.OrdinalIgnoreCase) &&
                         name.Equals(key[(itemType.Length + 1)..], StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }
    }
}
