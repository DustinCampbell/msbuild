// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Internal;

/// <summary>
///  Provides display-string helpers for <see cref="Subcategory"/> values.
/// </summary>
internal static class SubcategoryExtensions
{
    /// <summary>
    ///  Gets the localized display string for the specified subcategory.
    /// </summary>
    /// <param name="subcategory">The subcategory whose display string should be returned.</param>
    /// <returns>
    ///  The localized display string, or <see langword="null"/> for <see cref="Subcategory.None"/>.
    /// </returns>
    internal static string? GetDisplayString(this Subcategory subcategory)
        => subcategory switch
        {
            Subcategory.None => null,
            Subcategory.SolutionFile => BuildSR.SubCategoryForSolutionParsingErrors,

            _ => Assumed.Unreachable<string?>($"Unexpected {nameof(Subcategory)} value: {subcategory}."),
        };
}
