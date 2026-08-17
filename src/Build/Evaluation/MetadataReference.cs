// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a qualified or unqualified item metadata reference, such as
///  <c>%(EmbeddedResource.Culture)</c> or <c>%(Culture)</c>.
/// </summary>
internal readonly struct MetadataReference
{
    /// <summary>
    ///  The item type for a qualified reference, or <see langword="null"/> for an unqualified reference.
    /// </summary>
    public readonly string? ItemName;

    /// <summary>
    ///  The metadata name.
    /// </summary>
    public readonly string MetadataName;

    /// <summary>
    ///  Initializes a metadata reference.
    /// </summary>
    /// <param name="itemName">
    ///  The item type for a qualified reference, or <see langword="null"/> for an unqualified reference.
    /// </param>
    /// <param name="metadataName">The metadata name.</param>
    internal MetadataReference(string? itemName, string metadataName)
    {
        ItemName = itemName;
        MetadataName = metadataName;
    }
}
