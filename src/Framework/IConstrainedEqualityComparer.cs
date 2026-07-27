// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Text;

#nullable disable

namespace Microsoft.Build.Collections
{
    /// <summary>
    ///     Defines methods to support the comparison of objects for
    ///     equality over constrained inputs.
    /// </summary>
    internal interface IConstrainedEqualityComparer<in T> : IEqualityComparer<T>
    {
        /// <summary>
        /// Determines whether the specified objects are equal, factoring in the specified bounds when comparing <paramref name="y"/>.
        /// </summary>
        bool Equals(T x, T y, int indexY, int length);

        /// <summary>
        /// Returns a hash code for the specified object factoring in the specified bounds.
        /// </summary>
        int GetHashCode(T obj, int index, int length);

        /// <summary>
        /// Determines whether <paramref name="x"/> equals the text represented by <paramref name="y"/>.
        /// </summary>
        /// <remarks>
        /// A <see cref="StringSegment"/> encapsulates the (buffer, offset, length) triple used by
        /// <see cref="Equals(T, T, int, int)"/>, removing the inclusive/exclusive off-by-one foot-gun.
        /// </remarks>
        bool Equals(T x, StringSegment y);

        /// <summary>
        /// Returns a hash code for the text represented by <paramref name="obj"/>. This hash is
        /// consistent with <see cref="GetHashCode(T, int, int)"/> so a segment view of a name hashes
        /// identically to the same name looked up via the (buffer, offset, length) triple.
        /// </summary>
        int GetHashCode(StringSegment obj);
    }
}
