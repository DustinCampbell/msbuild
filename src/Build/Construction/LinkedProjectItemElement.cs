// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectItemElement"/>.
/// This subclass owns the <see cref="ProjectItemElementLink"/>.
/// </summary>
internal sealed class LinkedProjectItemElement : ProjectItemElement
{
    internal LinkedProjectItemElement(ProjectItemElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    internal override void ChangeItemType(string newItemType)
    {
        ArgumentException.ThrowIfNullOrEmpty(newItemType);
        XmlUtilities.VerifyThrowArgumentValidElementName(newItemType);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newItemType), "CannotModifyReservedItem", newItemType);

        ItemLink!.ChangeItemType(newItemType);
    }
}
