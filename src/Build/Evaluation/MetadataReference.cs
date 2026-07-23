// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation;

/// <summary>
///  This struct represents a reference to a piece of item metadata.  For example,
///  %(EmbeddedResource.Culture) or %(Culture) in the project file.  In this case,
///  "EmbeddedResource" is the item name, and "Culture" is the metadata name.
///  The item name is optional.
/// </summary>
internal readonly struct MetadataReference(string? itemName, string metadataName)
{
    // Could be null if the %(...) is not qualified with an item name.
    internal readonly string? ItemName = itemName;

    internal readonly string MetadataName = metadataName;
}
