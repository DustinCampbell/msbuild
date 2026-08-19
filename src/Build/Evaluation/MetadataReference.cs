// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// This struct represents a reference to a piece of item metadata.  For example,
    /// %(EmbeddedResource.Culture) or %(Culture) in the project file.  In this case,
    /// "EmbeddedResource" is the item name, and "Culture" is the metadata name.
    /// The item name is optional.
    /// </summary>
    internal readonly struct MetadataReference : IEquatable<MetadataReference>
    {
        /// <summary>
        ///  Initializes a metadata reference.
        /// </summary>
        /// <param name="itemName">
        ///  The optional item type that qualifies the metadata name.
        /// </param>
        /// <param name="metadataName">The metadata name.</param>
        public MetadataReference(StringSegment itemName, StringSegment metadataName)
        {
            ItemName = itemName;
            MetadataName = metadataName;
        }

        /// <summary>
        ///  Gets the optional item type that qualifies the metadata name.
        /// </summary>
        public StringSegment ItemName { get; }

        /// <summary>
        ///  Gets the metadata name.
        /// </summary>
        public StringSegment MetadataName { get; }

        /// <summary>
        ///  Realizes the optionally qualified metadata name.
        /// </summary>
        /// <returns>
        ///  The metadata name, qualified by its item type when present.
        /// </returns>
        public override string ToString()
            => ItemName.HasValue
                ? StringSegment.Join('.', [ItemName, MetadataName])
                : MetadataName.ValueOrEmpty;

        /// <summary>
        ///  Determines whether this reference identifies the same item type and metadata name as
        ///  <paramref name="other"/>.
        /// </summary>
        /// <param name="other">The reference to compare.</param>
        /// <returns>
        ///  <see langword="true"/> when both names are equal using MSBuild's case-insensitive name
        ///  semantics; otherwise, <see langword="false"/>.
        /// </returns>
        public bool Equals(MetadataReference other)
            => ItemName.Equals(other.ItemName, StringComparison.OrdinalIgnoreCase)
                && MetadataName.Equals(other.MetadataName, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override bool Equals(object? obj)
            => obj is MetadataReference other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int itemNameHash = ItemName.HasValue
                ? MSBuildNameIgnoreCaseComparer.Default.GetHashCode(ItemName)
                : 0;

            return unchecked(
                (itemNameHash * 397) ^
                MSBuildNameIgnoreCaseComparer.Default.GetHashCode(MetadataName));
        }
    }
}
