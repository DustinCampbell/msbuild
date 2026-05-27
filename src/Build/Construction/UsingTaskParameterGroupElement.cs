// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Internal;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// UsingTaskParameterGroupElement represents a ParameterGroup under the using task.
/// </summary>
[DebuggerDisplay("#Parameters={Count}")]
public class UsingTaskParameterGroupElement : ProjectElementContainer
{
    internal UsingTaskParameterGroupElement(UsingTaskParameterGroupElementLink link)
        : base(link)
    {
    }

    internal UsingTaskParameterGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
        VerifyParent(parent);
    }

    private UsingTaskParameterGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, null, containingProject)
    {
    }

    /// <summary>
    /// Condition should never be set, but the getter returns null instead of throwing
    /// because a nonexistent condition is implicitly true.
    /// </summary>
    public override string? Condition
    {
        get => null;
        set => ErrorUtilities.ThrowInvalidOperation("OM_CannotGetSetCondition");
    }

    /// <summary>
    /// Get any contained parameters.
    /// </summary>
    public ICollection<ProjectUsingTaskParameterElement> Parameters
        => GetChildrenOfType<ProjectUsingTaskParameterElement>();

    /// <summary>
    /// This does not allow conditions, so it should not be called.
    /// </summary>
    public override ElementLocation ConditionLocation
        => Assumed.Unreachable<ElementLocation>("Should not evaluate this");

    /// <summary>
    /// Convenience method that picks a location based on a heuristic.
    /// </summary>
    public ProjectUsingTaskParameterElement AddParameter(string name, string output, string required, string parameterType)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        ProjectUsingTaskParameterElement newParameter = ContainingProject.CreateUsingTaskParameterElement(name, output, required, parameterType);
        AppendChild(newParameter);

        return newParameter;
    }

    /// <summary>
    /// Convenience method that picks a location based on a heuristic.
    /// </summary>
    public ProjectUsingTaskParameterElement AddParameter(string name)
        => AddParameter(name, string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// Creates an unparented UsingTaskParameterGroupElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent.
    /// </summary>
    internal static UsingTaskParameterGroupElement CreateDisconnected(ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.usingTaskParameterGroup);

        return new(element, containingProject);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
    {
        // Verify the parent is a usingTaskElement and that the taskFactory attribute is set.
        if (newParent is not ProjectUsingTaskElement parentUsingTask)
        {
            return false;
        }

        // Since there is not going to be a TaskElement on the using task we need to validate
        // and make sure there is a TaskFactory attribute on the parent element and it is not empty
        if (parentUsingTask.TaskFactory.Length == 0)
        {
            Assumed.False(newParent.IsLink, nameof(parentUsingTask.TaskFactory));
            ProjectXmlUtilities.VerifyThrowProjectRequiredAttribute(newParent.XmlElement, nameof(parentUsingTask.TaskFactory));
        }

        // UNDONE: Do check to make sure the task body is the last child
        return true;
    }

    /// <inheritdoc />
    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateUsingTaskParameterGroupElement();
}
