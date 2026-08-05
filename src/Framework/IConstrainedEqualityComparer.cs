// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Collections;

/// <summary>
///  Defines methods for comparing and hashing bounded regions of objects.
/// </summary>
/// <typeparam name="T">The type of object to compare.</typeparam>
internal interface IConstrainedEqualityComparer<in T> : IEqualityComparer<T>
{
    /// <summary>
    ///  Determines whether an object and a bounded region of another object are equal.
    /// </summary>
    /// <param name="x">The object to compare, or <see langword="null"/>.</param>
    /// <param name="y">The object containing the region to compare, or <see langword="null"/>.</param>
    /// <param name="indexY">The zero-based starting index of the region in <paramref name="y"/>.</param>
    /// <param name="length">The length of the region.</param>
    /// <returns>
    ///  <see langword="true"/> when <paramref name="x"/> equals the specified region of
    ///  <paramref name="y"/>; otherwise, <see langword="false"/>.
    /// </returns>
    bool Equals(T? x, T? y, int indexY, int length);

    /// <summary>
    ///  Returns a hash code for a bounded region of an object.
    /// </summary>
    /// <param name="obj">The object containing the region to hash. This value must not be <see langword="null"/>.</param>
    /// <param name="index">The zero-based starting index of the region in <paramref name="obj"/>.</param>
    /// <param name="length">The length of the region.</param>
    /// <returns>
    ///  A hash code for the specified region of <paramref name="obj"/>.
    /// </returns>
    int GetHashCode([DisallowNull] T obj, int index, int length);
}
