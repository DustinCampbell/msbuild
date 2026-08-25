// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Collections;

internal readonly partial struct OneOrMany<T>
{
    /// <summary>
    ///  Builds a <see cref="OneOrMany{T}"/> while storing the first value inline.
    /// </summary>
    public ref struct Builder
    {
        private T? _firstItem;
        private RefArrayBuilder<T> _builder;
        private int _count;

        /// <summary>
        ///  Gets the number of values added to the builder.
        /// </summary>
        public readonly int Count
            => _count;

        /// <summary>
        ///  Gets a value indicating whether the builder contains no values.
        /// </summary>
        public readonly bool IsEmpty
            => _count == 0;

        /// <summary>
        ///  Adds a value to the builder.
        /// </summary>
        /// <param name="item">The value to add.</param>
        public void Add(T item)
        {
            if (_count == 0)
            {
                _firstItem = item;
            }
            else
            {
                if (_count == 1)
                {
                    _builder.Add(_firstItem!);
                }

                _builder.Add(item);
            }

            _count++;
        }

        /// <summary>
        ///  Creates a <see cref="OneOrMany{T}"/> containing the added values.
        /// </summary>
        /// <returns>
        ///  The built collection.
        /// </returns>
        public readonly OneOrMany<T> ToOneOrMany()
            => _count switch
            {
                0 => default,
                1 => new OneOrMany<T>(_firstItem!),
                _ => OneOrMany.Create(_builder.AsSpan()),
            };

        /// <summary>
        ///  Releases any pooled storage owned by the builder.
        /// </summary>
        public void Dispose()
            => _builder.Dispose();
    }
}
