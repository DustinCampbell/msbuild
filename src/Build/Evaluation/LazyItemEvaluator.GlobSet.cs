// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        /// <summary>
        /// A lightweight, reference-typed wrapper around an <see cref="ImmutableArray{T}"/> of glob strings
        /// used to track globs that should be ignored during item evaluation. This replaces
        /// <see cref="ImmutableHashSet{T}"/> which was never used for hash-based lookups — only iteration,
        /// emptiness checks, and reference-equality caching.
        /// </summary>
        /// <remarks>
        /// Being a reference type is critical: <see cref="LazyItemList.TryGetFromCache"/> uses
        /// <see cref="object.ReferenceEquals"/> for its cache identity check.
        /// </remarks>
        private sealed class GlobSet
        {
            public static readonly GlobSet Empty = new([]);

            private readonly ImmutableArray<string> _globs;

            private GlobSet(ImmutableArray<string> globs) => _globs = globs;

            public bool IsEmpty => _globs.IsEmpty;

            public ImmutableArray<string> Globs => _globs;

            /// <summary>
            /// Creates a <see cref="GlobSet"/> from a <see cref="HashSet{T}"/> used during construction
            /// to deduplicate glob strings. Returns <see cref="Empty"/> when the set is empty.
            /// </summary>
            public static GlobSet Create(HashSet<string> globs)
            {
                return globs.Count == 0 ? Empty : new GlobSet([.. globs]);
            }
        }
    }
}
