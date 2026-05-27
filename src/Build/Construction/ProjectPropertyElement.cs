// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Execution;
using Microsoft.Build.Internal;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectPropertyElement class represents the Property element in the MSBuild project.
/// </summary>
/// <remarks>
/// We do not need to use or set the PropertyType enumeration in the CM.
/// The CM does not know about Environment or Global properties, and does not create Output properties.
/// We can just verify that we haven't read a PropertyType.Reserved property ourselves.
/// So the CM only represents Normal properties.
/// </remarks>
[DebuggerDisplay("{Name} Value={Value} Condition={Condition}")]
public class ProjectPropertyElement : ProjectElement, IPropertyElementWithLocation
{
    internal ProjectPropertyElementLink? PropertyLink => (ProjectPropertyElementLink?)Link;

    [MemberNotNullWhen(true, nameof(PropertyLink))]
    internal override bool IsLink => base.IsLink;

    internal ProjectPropertyElement(ProjectPropertyElementLink link)
        : base(link)
    {
    }

    internal ProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectPropertyGroupElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectPropertyElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Gets or sets the property name.
    /// </summary>
    public string Name
    {
        get => ElementName;
        set => ChangeName(value);
    }

    /// <summary>
    /// Gets or sets the unevaluated value.
    /// Returns empty string if it is not present.
    /// </summary>
    public string Value
    {
        get => IsLink ? PropertyLink.Value : Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (IsLink)
            {
                PropertyLink.Value = value;
                return;
            }

            // Visual Studio has a tendency to set properties to their existing value.
            if (Value != value)
            {
                Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
                MarkDirty("Set property Value {0}", value);
            }
        }
    }

    /// <summary>
    /// Creates an unparented ProjectPropertyElement, wrapping an unparented XmlElement.
    /// Validates name.
    /// Caller should then ensure the element is added to the XmlDocument in the appropriate location.
    /// </summary>
    internal static ProjectPropertyElement CreateDisconnected(string name, ProjectRootElement containingProject)
    {
        XmlUtilities.VerifyThrowArgumentValidElementName(name);

        ErrorUtilities.VerifyThrowInvalidOperation(
            !XMakeElements.ReservedItemNames.Contains(name) && !ReservedPropertyNames.IsReservedProperty(name),
            "OM_CannotCreateReservedProperty",
            name);

        XmlElementWithLocation element = containingProject.CreateElement(name);

        return new(element, containingProject);
    }

    /// <summary>
    /// Changes the name.
    /// </summary>
    /// <remarks>
    /// The implementation has to actually replace the element to do this.
    /// </remarks>
    internal void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);
        XmlUtilities.VerifyThrowArgumentValidElementName(newName);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedProperty", newName);

        if (IsLink)
        {
            PropertyLink.ChangeName(newName);
            return;
        }

        // Because the element was created from our special XmlDocument, we know it's
        // an XmlElementWithLocation.
        XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

        ReplaceElement(newElement);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectPropertyGroupElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreatePropertyElement(Name);
}
