// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectMetadataElement class represents a Metadata element in the MSBuild project.
/// </summary>
[DebuggerDisplay("{Name} Value={Value} Condition={Condition}")]
public class ProjectMetadataElement : ProjectElement
{
    internal ProjectMetadataElementLink? MetadataLink => (ProjectMetadataElementLink?)Link;

    [MemberNotNullWhen(true, nameof(MetadataLink))]
    internal override bool IsLink => base.IsLink;

    private protected ProjectMetadataElement(ProjectMetadataElementLink link)
        : base(link)
    {
    }

    private protected ProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private protected ProjectMetadataElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Gets or sets the metadata's type.
    /// </summary>
    public string Name
    {
        get => ElementName;
        set => ChangeName(value);
    }

    // Add a new property with the same name here because this attribute should be public for ProjectMetadataElement,
    //  but internal for ProjectElement, because we don't want it to be settable for arbitrary elements.
    /// <summary>
    /// Gets or sets whether this piece of metadata is expressed as an attribute.
    /// </summary>
    /// <remarks>
    /// If true, then the metadata will be expressed as an attribute instead of a child element, for example
    /// &lt;Reference Include="Libary.dll" HintPath="..\lib\Library.dll" Private="True" /&gt;
    /// </remarks>
    public new bool ExpressedAsAttribute
    {
        get => base.ExpressedAsAttribute;
        set
        {
            if (value)
            {
                ValidateValidMetadataAsAttributeName(Name, Parent?.ElementName ?? "null", Parent?.Location ?? ElementLocation.EmptyLocation);
            }

            base.ExpressedAsAttribute = value;
        }
    }

    /// <summary>
    /// Gets or sets the unevaluated value.
    /// Returns empty string if it is not present.
    /// </summary>
    public virtual string Value
    {
        get => IsLink ? MetadataLink.Value : Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

        set
        {
            if (IsLink)
            {
                MetadataLink.Value = value;
                return;
            }

            ArgumentNullException.ThrowIfNull(value);
            Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
            Parent?.UpdateElementValue(this);
            MarkDirty("Set metadata Value {0}", value);
        }
    }

    /// <summary>
    /// Creates an unparented ProjectMetadataElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent.
    /// </summary>
    internal static ProjectMetadataElement CreateDisconnected(string name, ProjectRootElement containingProject, ElementLocation? location = null)
    {
        XmlUtilities.VerifyThrowArgumentValidElementName(name);
        ErrorUtilities.VerifyThrowArgument(!ItemSpecModifiers.IsItemSpecModifier(name), "ItemSpecModifierCannotBeCustomMetadata", name);
        ErrorUtilities.VerifyThrowInvalidOperation(!XMakeElements.ReservedItemNames.Contains(name), "CannotModifyReservedItemMetadata", name);

        XmlElementWithLocation element = containingProject.CreateElement(name, location);

        return new XmlProjectMetadataElement(element, containingProject);
    }

    /// <summary>
    /// Changes the name.
    /// </summary>
    /// <remarks>
    /// The implementation has to actually replace the element to do this.
    /// </remarks>
    internal virtual void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);
        XmlUtilities.VerifyThrowArgumentValidElementName(newName);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedItemMetadata", newName);

        if (IsLink)
        {
            MetadataLink.ChangeName(newName);
            return;
        }

        Assumed.NotNull(Parent);

        if (ExpressedAsAttribute)
        {
            ValidateValidMetadataAsAttributeName(newName, Parent.ElementName, Parent.Location);
        }

        string oldName = XmlElement.Name;

        // Because the element was created from our special XmlDocument, we know it's
        // an XmlElementWithLocation.
        XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newName, XmlElement.NamespaceURI);

        ReplaceElement(newElement);
        Parent.UpdateElementName(this, oldName);
    }

    internal static void ValidateValidMetadataAsAttributeName(string name, string parentName, IElementLocation parentLocation)
    {
        if (!AttributeNameIsValidMetadataName(name))
        {
            ProjectErrorUtilities.ThrowInvalidProject(parentLocation, "InvalidMetadataAsAttribute", name, parentName);
        }
    }

    internal static bool AttributeNameIsValidMetadataName(string name)
    {
        ProjectParser.CheckMetadataAsAttributeName(name, out bool isReservedAttributeName, out bool isValidMetadataNameInAttribute);

        return !isReservedAttributeName && isValidMetadataNameInAttribute;
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectItemElement or ProjectItemDefinitionElement;

    /// <inheritdoc />
    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateMetadataElement(Name);
}
