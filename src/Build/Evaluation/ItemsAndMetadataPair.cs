// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Accumulates the distinct item types and metadata references discovered while shredding expressions.
/// </summary>
internal struct ItemsAndMetadataPair
{
    private InlineSet<StringSegment> _itemTypes;
    private InlineSet<MetadataReference> _metadata;

    /// <summary>
    ///  Initializes empty item-type and metadata sets.
    /// </summary>
    public ItemsAndMetadataPair()
    {
        _itemTypes = new InlineSet<StringSegment>(MSBuildNameIgnoreCaseComparer.Default);
        _metadata = new InlineSet<MetadataReference>(EqualityComparer<MetadataReference>.Default);
    }

    /// <summary>
    ///  Gets the distinct referenced item types.
    /// </summary>
    public readonly InlineSet<StringSegment> ItemTypes => _itemTypes;

    /// <summary>
    ///  Gets the distinct metadata references, which may be qualified by an item type.
    /// </summary>
    public readonly InlineSet<MetadataReference> Metadata => _metadata;

    /// <summary>
    ///  Adds an item type if it has not already been recorded.
    /// </summary>
    /// <param name="itemType">The item type to add.</param>
    public void AddItemType(StringSegment itemType)
        => _itemTypes.Add(itemType);

    /// <summary>
    ///  Adds a metadata reference if it has not already been recorded.
    /// </summary>
    /// <param name="metadata">The metadata reference to add.</param>
    public void AddMetadata(MetadataReference metadata)
        => _metadata.Add(metadata);
}
