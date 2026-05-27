// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectExtensionsElement represents the ProjectExtensions element in the MSBuild project.
/// ProjectExtensions can contain arbitrary XML content.
/// The ProjectExtensions element is deprecated and provided only for backward compatibility.
/// Use a property instead. Properties can also contain XML content.
/// </summary>
public class ProjectExtensionsElement : ProjectElement
{
    internal ProjectExtensionsElementLink? ExtensionLink => (ProjectExtensionsElementLink?)Link;

    [MemberNotNullWhen(true, nameof(ExtensionLink))]
    internal override bool IsLink => base.IsLink;

    private protected ProjectExtensionsElement(ProjectExtensionsElementLink link)
        : base(link)
    {
    }

    private protected ProjectExtensionsElement(XmlElementWithLocation xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private protected ProjectExtensionsElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Condition should never be set, but the getter returns null instead of throwing
    /// because a nonexistent condition is implicitly true
    /// </summary>
    public override string? Condition
    {
        get => null;
        set => ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
    }

    /// <summary>
    /// Gets and sets the raw XML content
    /// </summary>
    public virtual string Content
    {
        [DebuggerStepThrough]
        get => IsLink ? ExtensionLink.Content : XmlElement.InnerXml;

        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Content));

            if (IsLink)
            {
                ExtensionLink.Content = value;
                return;
            }

            XmlElement.InnerXml = value;
            MarkDirty("Set ProjectExtensions raw {0}", value);
        }
    }

    /// <summary>
    /// This does not allow conditions, so it should not be called.
    /// </summary>
    public override ElementLocation ConditionLocation
        => Assumed.Unreachable<ElementLocation>("Should not evaluate this");

    /// <summary>
    /// Get or set the content of the first sub-element
    /// with the provided name.
    /// </summary>
    public virtual string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (IsLink)
            {
                return ExtensionLink.GetSubElement(name);
            }

            XmlElement? idElement = XmlElement[name];

            // remove the xmlns attribute, because the IDE's not expecting that
            return idElement != null
                ? Internal.Utilities.RemoveXmlNamespace(idElement.InnerXml)
                : string.Empty;
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);

            if (IsLink)
            {
                ExtensionLink.SetSubElement(name, value);
                return;
            }

            XmlElement? idElement = XmlElement[name];

            if (idElement == null)
            {
                if (value.Length == 0)
                {
                    return;
                }

                idElement = XmlDocument.CreateElement(name, XmlElement.NamespaceURI);
                XmlElement.AppendChild(idElement);
            }

            // The actual InnerXml may have the MSBuild namespace but be otherwise identical
            // to the setting, in which case the namespace was probably inherited from the
            // document and should be ignored.
            if (idElement.InnerXml != value &&
                idElement.InnerXml.Replace(ProjectRootElement.EmptyProjectFileXmlNamespace, string.Empty) != value)
            {
                if (value.Length == 0)
                {
                    XmlElement.RemoveChild(idElement);
                }
                else
                {
                    idElement.InnerXml = value;
                }

                MarkDirty("Set ProjectExtensions content {0}", value);
            }
        }
    }

    public override void CopyFrom(ProjectElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        ErrorUtilities.VerifyThrowArgument(GetType().IsEquivalentTo(element.GetType()), "CannotCopyFromElementOfThatType");

        if (this == element)
        {
            return;
        }

        Label = element.Label;

        var other = (ProjectExtensionsElement)element;
        Content = other.Content;

        MarkDirty("CopyFrom", null);
    }

    /// <summary>
    /// Creates an unparented ProjectExtensionsElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectExtensionsElement CreateDisconnected(ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.projectExtensions);

        return new XmlProjectExtensionsElement(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectRootElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateProjectExtensionsElement();
}
