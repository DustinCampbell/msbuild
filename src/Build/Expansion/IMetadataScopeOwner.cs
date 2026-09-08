// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Expansion;

/// <summary>
///  Restores metadata state for <see cref="MetadataScope"/>.
/// </summary>
internal interface IMetadataScopeOwner
{
    /// <summary>
    ///  Leaves the active metadata scope.
    /// </summary>
    /// <param name="depth">The nesting depth of the scope being left.</param>
    /// <param name="previousMetadata">The metadata table to restore.</param>
    void LeaveMetadataScope(int depth, IMetadataTable? previousMetadata);
}
