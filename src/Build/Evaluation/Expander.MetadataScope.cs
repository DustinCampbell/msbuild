// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Opens a scope that temporarily sets the expander's metadata table. When the returned
    ///  <see cref="MetadataScope"/> is disposed, the previous metadata table is restored.
    /// </summary>
    /// <param name="metadata">
    ///  The metadata table to set, or <see langword="null"/> to open the scope without
    ///  changing the current table. The table can be set or changed later via
    ///  <see cref="MetadataScope.Update"/>.
    /// </param>
    /// <returns>
    ///  A <see cref="MetadataScope"/> that must be disposed to restore the previous metadata table.
    /// </returns>
    public MetadataScope OpenMetadataScope(IMetadataTable? metadata = null)
        => new(this, metadata);

    /// <summary>
    ///  A disposable, stack-only scope that manages the lifetime of an expander's metadata table.
    ///  On construction it captures the current metadata table; on disposal it restores it. Use
    ///  <see cref="Update"/> to change the metadata table within the scope.
    /// </summary>
    public readonly ref struct MetadataScope
    {
        private readonly Expander<P, I> _expander;
        private readonly IMetadataTable _previousMetadata;

        /// <summary>
        ///  Creates a new <see cref="MetadataScope"/> for the given <paramref name="expander"/>,
        ///  capturing its current metadata table. If <paramref name="metadata"/> is not
        ///  <see langword="null"/>, the expander's metadata table is immediately updated.
        /// </summary>
        /// <param name="expander">The expander whose metadata table is being scoped.</param>
        /// <param name="metadata">
        ///  The metadata table to set, or <see langword="null"/> to leave the current table unchanged.
        /// </param>
        public MetadataScope(Expander<P, I> expander, IMetadataTable? metadata)
        {
            _expander = expander;
            _previousMetadata = expander._metadata;

            if (metadata is not null)
            {
                Update(metadata);
            }
        }

        /// <summary>
        ///  Replaces the expander's current metadata table with <paramref name="metadata"/>.
        ///  This may be called multiple times within a single scope.
        /// </summary>
        /// <param name="metadata">The new metadata table. Must not be <see langword="null"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="metadata"/> is <see langword="null"/>.</exception>
        public void Update(IMetadataTable metadata)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            _expander._metadata = metadata;
        }

        /// <summary>
        ///  Restores the expander's metadata table to the value it had when this scope was opened.
        /// </summary>
        public void Dispose()
        {
            _expander._metadata = _previousMetadata;
        }
    }
}
