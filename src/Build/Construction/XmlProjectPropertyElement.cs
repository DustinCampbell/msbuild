// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectPropertyElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectPropertyElement : ProjectPropertyElement
{
    internal XmlProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectPropertyGroupElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
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

            // Visual Studio has a tendency to set properties to their existing value.
            if (Value != value)
            {
                Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
                MarkDirty("Set property Value {0}", value);
            }
        }
    }

    /// <inheritdoc />
    internal override void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);
        XmlUtilities.VerifyThrowArgumentValidElementName(newName);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedProperty", newName);

        // Because the element was created from our special XmlDocument, we know it's
        // an XmlElementWithLocation.
        XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

        ReplaceElement(newElement);
    }
}
