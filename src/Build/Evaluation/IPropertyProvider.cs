// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Provides access to properties during expansion.
/// </summary>
/// <typeparam name="T">The type of property provided.</typeparam>
internal interface IPropertyProvider<out T>
    where T : class
{
    /// <summary>
    ///  Gets the property with the specified name.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <returns>
    ///  The property, or <see langword="null"/> if it was not found.
    /// </returns>
    T? GetProperty(string name);

    /// <summary>
    ///  Gets the property whose name is the specified segment of <paramref name="name"/>.
    /// </summary>
    /// <param name="name">The string containing the property name.</param>
    /// <param name="startIndex">The zero-based index at which the property name begins.</param>
    /// <param name="endIndex">The zero-based, inclusive index at which the property name ends.</param>
    /// <returns>
    ///  The property, or <see langword="null"/> if it was not found.
    /// </returns>
    T? GetProperty(string name, int startIndex, int endIndex);
}
