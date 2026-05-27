// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectWhenElement represents the When element in the MSBuild project.
/// </summary>
[DebuggerDisplay("#Children={Count} Condition={Condition}")]
public class ProjectWhenElement : ProjectElementContainer
{
    internal ProjectWhenElement(ProjectWhenElementLink link)
        : base(link)
    {
    }

    internal ProjectWhenElement(XmlElementWithLocation xmlElement, ProjectChooseElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectWhenElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Get an enumerator over any child chooses.
    /// </summary>
    public ICollection<ProjectChooseElement> ChooseElements
        => GetChildrenOfType<ProjectChooseElement>();

    /// <summary>
    /// Get an enumerator over any child item groups.
    /// </summary>
    public ICollection<ProjectItemGroupElement> ItemGroups
        => GetChildrenOfType<ProjectItemGroupElement>();

    /// <summary>
    /// Get an enumerator over any child property groups.
    /// </summary>
    public ICollection<ProjectPropertyGroupElement> PropertyGroups
        => GetChildrenOfType<ProjectPropertyGroupElement>();

    /// <summary>
    /// Creates an unparented ProjectPropertyGroupElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent.
    /// </summary>
    internal static ProjectWhenElement CreateDisconnected(string condition, ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.when);

        return new(element, containingProject)
        {
            Condition = condition,
        };
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectChooseElement;

    internal override void VerifySiblings(ProjectElement? previousSibling, ProjectElement? nextSibling)
        => ErrorUtilities.VerifyThrowInvalidOperation(
            previousSibling is not ProjectOtherwiseElement,
            "OM_NoOtherwiseBeforeWhenOrOtherwise");

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateWhenElement(Condition ?? string.Empty);
}
