// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectOtherwiseElement represents the Otherwise element in the MSBuild project.
/// </summary>
[DebuggerDisplay("#Children={Count}")]
public class ProjectOtherwiseElement : ProjectElementContainer
{
    private protected ProjectOtherwiseElement(ProjectOtherwiseElementLink link)
        : base(link)
    {
    }

    private protected ProjectOtherwiseElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement project)
        : base(xmlElement, parent, project)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private protected ProjectOtherwiseElement(XmlElementWithLocation xmlElement, ProjectRootElement project)
        : base(xmlElement, parent: null, project)
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
    /// Get an enumerator over any child item groups
    /// </summary>
    public ICollection<ProjectItemGroupElement> ItemGroups
        => GetChildrenOfType<ProjectItemGroupElement>();

    /// <summary>
    /// Get an enumerator over any child property groups
    /// </summary>
    public ICollection<ProjectPropertyGroupElement> PropertyGroups
        => GetChildrenOfType<ProjectPropertyGroupElement>();

    /// <summary>
    /// Get an enumerator over any child chooses
    /// </summary>
    public ICollection<ProjectChooseElement> ChooseElements
        => GetChildrenOfType<ProjectChooseElement>();

    /// <summary>
    /// This does not allow conditions, so it should not be called.
    /// </summary>
    public override ElementLocation ConditionLocation
        => Assumed.Unreachable<ElementLocation>("Should not evaluate this");

    /// <summary>
    /// Creates an unparented ProjectOtherwiseElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent.
    /// </summary>
    internal static ProjectOtherwiseElement CreateDisconnected(ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.otherwise);

        return new XmlProjectOtherwiseElement(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectChooseElement;

    internal override void VerifySiblings(ProjectElement? previousSibling, ProjectElement? nextSibling)
        => ErrorUtilities.VerifyThrowInvalidOperation(
            nextSibling is not (ProjectWhenElement or ProjectOtherwiseElement) && previousSibling is not ProjectOtherwiseElement,
            "OM_NoOtherwiseBeforeWhenOrOtherwise");

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateOtherwiseElement();
}
