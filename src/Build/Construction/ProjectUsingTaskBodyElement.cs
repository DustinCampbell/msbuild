// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Internal;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectUsingTaskBodyElement class represents the Task element under the using task element in the MSBuild project.
/// </summary>
[DebuggerDisplay("Evaluate={Evaluate} TaskBody={TaskBody}")]
public class ProjectUsingTaskBodyElement : ProjectElement
{
    internal ProjectUsingTaskBodyElementLink? UsingTaskBodyLink => (ProjectUsingTaskBodyElementLink?)Link;

    [MemberNotNullWhen(true, nameof(UsingTaskBodyLink))]
    internal override bool IsLink => base.IsLink;

    internal ProjectUsingTaskBodyElement(ProjectUsingTaskBodyElementLink link)
        : base(link)
    {
    }

    internal ProjectUsingTaskBodyElement(XmlElementWithLocation xmlElement, ProjectUsingTaskElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
        VerifyParent(parent);
    }

    private ProjectUsingTaskBodyElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
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
    /// Gets or sets the unevaluated value of the contents of the task xml
    /// Returns empty string if it is not present.
    /// </summary>
    public string TaskBody
    {
        get => IsLink ? UsingTaskBodyLink.TaskBody : Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

        set
        {
            if (IsLink)
            {
                UsingTaskBodyLink.TaskBody = value;
                return;
            }

            ArgumentNullException.ThrowIfNull(value, nameof(TaskBody));
            Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
            MarkDirty("Set usingtask body {0}", value);
        }
    }

    /// <summary>
    /// Gets the value of the Evaluate attribute.
    /// Returns true if it is not present.
    /// </summary>
    public string Evaluate
    {
        get
        {
            string evaluateAttribute = GetAttributeValueOrEmpty(XMakeAttributes.evaluate);

            return evaluateAttribute.Length == 0
                ? bool.TrueString
                : evaluateAttribute;
        }

        set => SetOrRemoveAttribute(XMakeAttributes.evaluate, value, "Set usingtask Evaluate {0}", value);
    }

    /// <summary>
    /// This does not allow conditions, so it should not be called.
    /// </summary>
    public override ElementLocation ConditionLocation
        => Assumed.Unreachable<ElementLocation>("Should not evaluate this");

    /// <summary>
    /// Location of the "Condition" attribute on this element, if any.
    /// If there is no such attribute, returns the location of the element,
    /// in lieu of the default value it uses for the attribute.
    /// </summary>
    public ElementLocation EvaluateLocation
        => GetAttributeLocation(XMakeAttributes.evaluate) ?? Location;

    /// <summary>
    /// Creates an unparented ProjectUsingTaskBodyElement, wrapping an unparented XmlElement.
    /// Validates name.
    /// Caller should then ensure the element is added to the XmlDocument in the appropriate location.
    /// </summary>
    internal static ProjectUsingTaskBodyElement CreateDisconnected(string evaluate, string body, ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.usingTaskBody);

        return new(element, containingProject)
        {
            Evaluate = evaluate,
            TaskBody = body
        };
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

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateUsingTaskBodyElement(Evaluate, TaskBody);
}
