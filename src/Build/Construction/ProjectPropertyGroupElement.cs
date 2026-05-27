// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectPropertyGroupElement represents the PropertyGroup element in the MSBuild project.
/// </summary>
[DebuggerDisplay("#Properties={Count} Condition={Condition} Label={Label}")]
public class ProjectPropertyGroupElement : ProjectElementContainer
{
    private protected ProjectPropertyGroupElement(ProjectPropertyGroupElementLink link)
        : base(link)
    {
    }

    private protected ProjectPropertyGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private protected ProjectPropertyGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Get any contained properties.
    /// </summary>
    public ICollection<ProjectPropertyElement> Properties
        => GetChildrenOfType<ProjectPropertyElement>();

    /// <summary>
    /// Get any contained properties.
    /// </summary>
    public ICollection<ProjectPropertyElement> PropertiesReversed
        => GetChildrenReversedOfType<ProjectPropertyElement>();

    /// <summary>
    /// Convenience method that picks a location based on a heuristic:
    /// Adds a new property after the last property in this property group.
    /// </summary>
    public ProjectPropertyElement AddProperty(string name, string unevaluatedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(unevaluatedValue);

        ProjectPropertyElement newProperty = ContainingProject.CreatePropertyElement(name);
        newProperty.Value = unevaluatedValue;
        AppendChild(newProperty);

        return newProperty;
    }

    /// <summary>
    /// Convenience method that picks a location based on a heuristic:
    /// If there is an existing property with the same name and no condition,
    /// updates its value. Otherwise it adds a new property after the last property.
    /// </summary>
    public ProjectPropertyElement SetProperty(string name, string unevaluatedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(unevaluatedValue);

        foreach (ProjectPropertyElement property in Properties)
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Condition is null or [])
            {
                property.Value = unevaluatedValue;
                return property;
            }
        }

        return AddProperty(name, unevaluatedValue);
    }

    /// <summary>
    /// Creates an unparented ProjectPropertyGroupElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectPropertyGroupElement CreateDisconnected(ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.propertyGroup);

        return new XmlProjectPropertyGroupElement(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectRootElement or ProjectTargetElement or ProjectWhenElement or ProjectOtherwiseElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreatePropertyGroupElement();
}
