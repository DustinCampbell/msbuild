// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    /// <summary>
    ///  Restores an expander's previous metadata when disposed.
    /// </summary>
    public readonly ref struct MetadataScope
    {
        private readonly Expander<P, I> _expander;
        private readonly IMetadataTable? _previousMetadata;

        /// <summary>
        ///  Replaces the metadata used by <paramref name="expander"/> until this scope is disposed.
        /// </summary>
        /// <param name="expander">The expander whose metadata is replaced.</param>
        /// <param name="metadata">The temporary metadata, or <see langword="null"/>.</param>
        internal MetadataScope(Expander<P, I> expander, IMetadataTable? metadata)
        {
            _expander = expander;
            _previousMetadata = expander._metadata;
            expander._metadata = metadata;
        }

        /// <summary>
        ///  Restores the metadata that was active before this scope was created.
        /// </summary>
        public void Dispose()
        {
            _expander._metadata = _previousMetadata;
        }
    }
}
