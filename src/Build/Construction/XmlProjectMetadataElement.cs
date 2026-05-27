// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectMetadataElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectMetadataElement : ProjectMetadataElement
{
    internal XmlProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    public override string Value
    {
        get => Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
            Parent?.UpdateElementValue(this);
            MarkDirty("Set metadata Value {0}", value);
        }
    }

    /// <inheritdoc />
    internal override void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);
        XmlUtilities.VerifyThrowArgumentValidElementName(newName);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedItemMetadata", newName);

        Assumed.NotNull(Parent);

        if (ExpressedAsAttribute)
        {
            IElementLocation parentLocation = Parent.Location;
            ValidateValidMetadataAsAttributeName(newName, Parent.ElementName, parentLocation);
        }

        string oldName = XmlElement.Name;

        // Because the element was created from our special XmlDocument, we know it's
        // an XmlElementWithLocation.
        XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

        ReplaceElement(newElement);
        Parent!.UpdateElementName(this, oldName);
    }
}
