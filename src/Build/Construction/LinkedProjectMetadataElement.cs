// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectMetadataElement"/>.
/// This subclass owns the <see cref="ProjectMetadataElementLink"/>.
/// </summary>
internal sealed class LinkedProjectMetadataElement : ProjectMetadataElement
{
    internal LinkedProjectMetadataElement(ProjectMetadataElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override string Value
    {
        get => MetadataLink!.Value;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            MetadataLink!.Value = value;
        }
    }

    /// <inheritdoc />
    internal override void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);
        XmlUtilities.VerifyThrowArgumentValidElementName(newName);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedItemMetadata", newName);

        MetadataLink!.ChangeName(newName);
    }
}
