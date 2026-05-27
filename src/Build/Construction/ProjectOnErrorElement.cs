// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectUsingTaskElement represents the Import element in the MSBuild project.
/// </summary>
[DebuggerDisplay("ExecuteTargetsAttribute={ExecuteTargetsAttribute}")]
public class ProjectOnErrorElement : ProjectElement
{
    internal ProjectOnErrorElement(ProjectOnErrorElementLink link)
        : base(link)
    {
    }

    internal ProjectOnErrorElement(XmlElementWithLocation xmlElement, ProjectTargetElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectOnErrorElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Gets and sets the value of the ExecuteTargets attribute.
    /// </summary>
    /// <remarks>
    /// 'Attribute' suffix is for clarity.
    /// </remarks>
    public string ExecuteTargetsAttribute
    {
        [DebuggerStepThrough]
        get => GetAttributeValueOrEmpty(XMakeAttributes.executeTargets);

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value, XMakeAttributes.executeTargets);
            SetOrRemoveAttribute(XMakeAttributes.executeTargets, value, "Set OnError ExecuteTargets {0}", value);
        }
    }

    /// <summary>
    /// Location of the "ExecuteTargets" attribute on this element, if any.
    /// If there is no such attribute, returns null;
    /// </summary>
    public ElementLocation ExecuteTargetsLocation
        => GetAttributeLocation(XMakeAttributes.executeTargets);

    /// <summary>
    /// Creates an unparented ProjectOnErrorElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent.
    /// </summary>
    internal static ProjectOnErrorElement CreateDisconnected(string executeTargets, ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.onError);

        return new(element, containingProject)
        {
            ExecuteTargetsAttribute = executeTargets
        };
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectTargetElement;

    /// <inheritdoc />
    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateOnErrorElement(ExecuteTargetsAttribute);
}
