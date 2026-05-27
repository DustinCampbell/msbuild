// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectItemDefinitionGroupElement represents the ItemGroup element in the MSBuild project.
/// </summary>
[DebuggerDisplay("#ItemDefinitions={Count} Condition={Condition} Label={Label}")]
public class ProjectItemDefinitionGroupElement : ProjectElementContainer
{
    internal ProjectItemDefinitionGroupElement(ProjectItemDefinitionGroupElementLink link)
        : base(link)
    {
    }

    internal ProjectItemDefinitionGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectItemDefinitionGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Get a list of child item definitions.
    /// </summary>
    public ICollection<ProjectItemDefinitionElement> ItemDefinitions
        => GetChildrenOfType<ProjectItemDefinitionElement>();

    /// <summary>
    /// Convenience method that picks a location based on a heuristic:
    /// Adds a new item definition after the last child.
    /// </summary>
    public ProjectItemDefinitionElement AddItemDefinition(string itemType)
    {
        ProjectItemDefinitionElement itemDefinition = ContainingProject.CreateItemDefinitionElement(itemType);

        AppendChild(itemDefinition);

        return itemDefinition;
    }

    /// <summary>
    /// Creates an unparented ProjectItemDefinitionGroupElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectItemDefinitionGroupElement CreateDisconnected(ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.itemDefinitionGroup);

        return new(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectRootElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateItemDefinitionGroupElement();
}
