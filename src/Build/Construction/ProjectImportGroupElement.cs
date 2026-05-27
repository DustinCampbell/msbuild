// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectImportGroupElement represents the ImportGroup element in the MSBuild project.
/// </summary>
[DebuggerDisplay("#Imports={Count} Condition={Condition} Label={Label}")]
public class ProjectImportGroupElement : ProjectElementContainer
{
    internal ProjectImportGroupElement(ProjectImportGroupElementLink link)
        : base(link)
    {
    }

    internal ProjectImportGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectImportGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Get any contained properties.
    /// </summary>
    public ICollection<ProjectImportElement> Imports
        => GetChildrenOfType<ProjectImportElement>();

    /// <summary>
    /// Convenience method that picks a location based on a heuristic:
    /// Adds a new import after the last import in this import group.
    /// </summary>
    public ProjectImportElement AddImport(string project)
    {
        ArgumentException.ThrowIfNullOrEmpty(project);

        ProjectImportElement newImport = ContainingProject.CreateImportElement(project);
        AppendChild(newImport);

        return newImport;
    }

    /// <summary>
    /// Creates an unparented ProjectImportGroupElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectImportGroupElement CreateDisconnected(ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.importGroup);

        return new(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectRootElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateImportGroupElement();
}
