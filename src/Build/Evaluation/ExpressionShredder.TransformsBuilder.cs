// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionShredder
{
    /// <summary>
    ///  Builds an immutable transform array while retaining the common single transform inline.
    /// </summary>
    private ref struct TransformsBuilder
    {
        private ItemTransform _firstTransform;
        private ImmutableArray<ItemTransform>.Builder? _builder;
        private bool _hasFirstTransform;

        /// <summary>
        ///  Adds a transform.
        /// </summary>
        /// <param name="transform">The transform to add.</param>
        public void Add(ItemTransform transform)
        {
            if (!_hasFirstTransform)
            {
                _firstTransform = transform;
                _hasFirstTransform = true;
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
        ///  Moves the accumulated transforms into an immutable array.
        /// </summary>
        /// <returns>
        ///  A non-default immutable array containing the accumulated transforms.
        /// </returns>
        public ImmutableArray<ItemTransform> ToImmutable()
            => _builder?.DrainToImmutable() ?? (_hasFirstTransform ? [_firstTransform] : []);
    }
}
