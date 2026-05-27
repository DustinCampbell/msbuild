// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectItemElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectItemElement : ProjectItemElement
{
    internal XmlProjectItemElement(XmlElementWithLocation xmlElement, ProjectItemGroupElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectItemElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    internal override void ChangeItemType(string newItemType)
    {
        ArgumentException.ThrowIfNullOrEmpty(newItemType);
        XmlUtilities.VerifyThrowArgumentValidElementName(newItemType);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newItemType), "CannotModifyReservedItem", newItemType);

        // Because the element was created from our special XmlDocument, we know it's
        // an XmlElementWithLocation.
        XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newItemType, XmlElement.NamespaceURI);

        ReplaceElement(newElement);
    }
}
