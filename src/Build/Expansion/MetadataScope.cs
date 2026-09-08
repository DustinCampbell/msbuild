// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;

namespace Microsoft.Build.Expansion;

/// <summary>
///  Restores an expander's previous metadata table when disposed.
/// </summary>
/// <param name="owner">The expander that owns the metadata state.</param>
/// <param name="previousMetadata">The metadata table to restore when the scope is disposed.</param>
/// <param name="depth">The nesting depth of this scope.</param>
/// <remarks>
///  This scope is stack-only and must be disposed in the reverse order in which it was created. Do not dispose
///  copies of a scope or use the owning expander concurrently while a scope is active.
/// </remarks>
internal ref struct MetadataScope(IMetadataScopeOwner owner, IMetadataTable? previousMetadata, int depth)
{
    private IMetadataScopeOwner? _owner = owner;

    /// <summary>
    ///  Restores the metadata table that was active before this scope was created.
    /// </summary>
    public void Dispose()
    {
        IMetadataScopeOwner? owner = _owner;
        Assumed.NotNull(owner, "Metadata scope was already disposed.");

        owner.LeaveMetadataScope(depth, previousMetadata);
        _owner = null;
    }
}
