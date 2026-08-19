// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionShredder
{
    /// <summary>
    ///  Builds an immutable array of item transforms while avoiding a builder allocation for zero or one transform.
    /// </summary>
    private ref struct TransformsBuilder
    {
        private ItemTransform _firstTransform;
        private bool _hasTransform;
        private ImmutableArray<ItemTransform>.Builder? _builder;

        /// <summary>
        ///  Adds a transform.
        /// </summary>
        /// <param name="transform">The transform to add.</param>
        public void Add(ItemTransform transform)
        {
            if (!_hasTransform)
            {
                _firstTransform = transform;
                _hasTransform = true;
                return;
            }

            if (_builder is null)
            {
                _builder = ImmutableArray.CreateBuilder<ItemTransform>(2);
                _builder.Add(_firstTransform);
            }

            _builder.Add(transform);
        }

        /// <summary>
        ///  Drains the added transforms to an immutable array.
        /// </summary>
        /// <returns>
        ///  The added transforms.
        /// </returns>
        public readonly ImmutableArray<ItemTransform> DrainToImmutable()
            => _builder?.DrainToImmutable() ?? (_hasTransform ? [_firstTransform] : []);
    }
}
