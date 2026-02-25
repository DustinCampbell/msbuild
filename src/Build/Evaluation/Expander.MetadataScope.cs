// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    ///  Set the <see cref="IMetadataTable"/> this <see cref="Expander{P, I}"/> uses.
    /// </summary>
    /// <param name="metadata">The <see cref="IMetadataTable"/> to use.</param>
    /// <returns>
    ///  Returns a <see cref="MetadataScope"/>. Call <see cref="MetadataScope.Dispose"/>
    ///  to reset the original <see cref="IMetadataTable"/>.
    /// </returns>
    public MetadataScope OpenMetadataScope(IMetadataTable metadata)
        => new(this, metadata);

    public ref struct MetadataScope
    {
        private readonly Expander<P, I> _expander;
        private readonly IMetadataTable _original;

        public MetadataScope(Expander<P, I> expander, IMetadataTable metadata)
        {
            _expander = expander;
            _original = expander._metadata;
            expander._metadata = metadata;
        }

        public void Update(IMetadataTable metadata)
        {
            _expander._metadata = metadata;
        }

        public void Dispose()
        {
            _expander._metadata = _original;
        }
    }
}
