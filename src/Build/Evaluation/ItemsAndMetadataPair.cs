// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Wrapper of two tables for a convenient method return value.
    /// </summary>
    internal struct ItemsAndMetadataPair
    {
        private HashSet<string>? _items;
        private Dictionary<string, MetadataReference>? _metadata;

        /// <summary>
        /// Gets the item set.
        /// </summary>
        public readonly HashSet<string>? Items => _items;

        /// <summary>
        /// Gets or sets the metadata dictionary
        /// The key is the possibly qualified metadata name, for example
        /// "EmbeddedResource.Culture" or "Culture".
        /// </summary>
        public Dictionary<string, MetadataReference>? Metadata => _metadata;

        [MemberNotNullWhen(true, nameof(_items), nameof(Items))]
        public readonly bool HasItems => _items?.Count > 0;

        [MemberNotNullWhen(true, nameof(_metadata), nameof(Metadata))]
        public readonly bool HasMetadata => _metadata?.Count > 0;

        public void AddItem(string value)
        {
            _items ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
            _items.Add(value);
        }

        public void AddMetadata(string key, MetadataReference metadata)
        {
            _metadata ??= new Dictionary<string, MetadataReference>(MSBuildNameIgnoreCaseComparer.Default);
            _metadata[key] = metadata;
        }
    }
}
