// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Table of metadata useable to expand expressions
    /// </summary>
    internal interface IMetadataTable
    {
        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified.
        /// If no value is available, returns empty string.
        /// </summary>
        string GetEscapedValue(string name);

        /// <summary>
        ///  Retrieves the value for the specified metadata name segment.
        /// </summary>
        /// <param name="name">The metadata name segment.</param>
        /// <returns>The escaped metadata value, or <see cref="string.Empty"/> when none is available.</returns>
        string GetEscapedValue(StringSegment name);

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If item type is null, it is ignored.
        /// If no value is available, returns empty string.
        /// </summary>
        string GetEscapedValue(string? itemType, string name);

        /// <summary>
        ///  Retrieves the value for the specified item type and metadata name segments.
        /// </summary>
        /// <param name="itemType">The optional item type segment.</param>
        /// <param name="name">The metadata name segment.</param>
        /// <returns>The escaped metadata value, or <see cref="string.Empty"/> when none is available.</returns>
        string GetEscapedValue(StringSegment itemType, StringSegment name);

        /// <summary>
        /// Returns the value if it exists, null otherwise.
        /// If item type is null, it is ignored.
        /// </summary>
        string? GetEscapedValueIfPresent(string? itemType, string name);

        /// <summary>
        ///  Retrieves the value for the specified item type and metadata name segments when present.
        /// </summary>
        /// <param name="itemType">The optional item type segment.</param>
        /// <param name="name">The metadata name segment.</param>
        /// <returns>The escaped metadata value, or <see langword="null"/> when none is available.</returns>
        string? GetEscapedValueIfPresent(StringSegment itemType, StringSegment name);
    }
}
