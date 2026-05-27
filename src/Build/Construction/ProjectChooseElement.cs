// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectChooseElement represents the Choose element in the MSBuild project.
/// Currently it does not allow a Condition.
/// </summary>
[DebuggerDisplay("ProjectChooseElement (#Children={Count} HasOtherwise={OtherwiseElement != null})")]
public class ProjectChooseElement : ProjectElementContainer
{
    internal ProjectChooseElement(ProjectChooseElementLink link)
        : base(link)
    {
    }

    internal ProjectChooseElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectChooseElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
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
    /// Get the When children.
    /// Will contain at least one entry.
    /// </summary>
    public ICollection<ProjectWhenElement> WhenElements => GetChildrenOfType<ProjectWhenElement>();

    /// <summary>
    /// Get any Otherwise child.
    /// </summary>
    public ProjectOtherwiseElement? OtherwiseElement
        => LastChild as ProjectOtherwiseElement;

    /// <summary>
    /// This does not allow conditions, so it should not be called.
    /// </summary>
    public override ElementLocation ConditionLocation
        => Assumed.Unreachable<ElementLocation>("Should not evaluate this");

    /// <summary>
    /// Creates an unparented ProjectChooseElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectChooseElement CreateDisconnected(ProjectRootElement containingProject)
    {
        Assumed.False(containingProject.IsLink, "Attempt to edit a document that is not backed by a local xml is disallowed.");

        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.choose);
        return new(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
    {
        if (newParent is not (ProjectRootElement or ProjectWhenElement or ProjectOtherwiseElement))
        {
            return false;
        }

        int nestingDepth = 0;
        ProjectElementContainer? current = newParent;

        while (current != null)
        {
            current = current.Parent;

            nestingDepth++;

            // This should really be an OM error, with no error number. But it's so obscure, it's not worth a new string.
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                nestingDepth <= ProjectParser.MaximumChooseNesting,
                newParent.Location,
                "ChooseOverflow",
                ProjectParser.MaximumChooseNesting);
        }

        return true;
    }

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateChooseElement();
}
