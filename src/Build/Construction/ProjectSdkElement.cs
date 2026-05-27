// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
///     ProjectSdkElement represents the Sdk element within the MSBuild project.
/// </summary>
public class ProjectSdkElement : ProjectElementContainer
{
    internal ProjectSdkElement(ProjectElementContainerLink link)
        : base(link)
    {
    }

    internal ProjectSdkElement(XmlElementWithLocation xmlElement, ProjectRootElement parent,
        ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectSdkElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Gets or sets the name of the SDK.
    /// </summary>
    public string Name
    {
        get => GetAttributeValueOrEmpty(XMakeAttributes.sdkName);
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value, XMakeAttributes.sdkName);
            SetOrRemoveAttribute(XMakeAttributes.sdkName, value, $"Set SDK Name to {value}", XMakeAttributes.sdkName);
        }
    }

    /// <summary>
    /// Gets or sets the version of the SDK.
    /// </summary>
    public string Version
    {
        get => GetAttributeValueOrEmpty(XMakeAttributes.sdkVersion);
        set => SetOrRemoveAttribute(XMakeAttributes.sdkVersion, value, $"Set SDK Version to {value}", XMakeAttributes.sdkVersion);
    }

    /// <summary>
    /// Gets or sets the minimum version of the SDK required to build the project.
    /// </summary>
    public string MinimumVersion
    {
        get => GetAttributeValueOrEmpty(XMakeAttributes.sdkMinimumVersion);
        set => SetOrRemoveAttribute(XMakeAttributes.sdkMinimumVersion, value, $"Set SDK MinimumVersion to {value}", XMakeAttributes.sdkMinimumVersion);
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectRootElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateProjectSdkElement(Name, Version);

    /// <summary>
    ///     Creates a non-parented ProjectSdkElement, wrapping an non-parented XmlElement.
    ///     Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectSdkElement CreateDisconnected(string sdkName, string sdkVersion, ProjectRootElement containingProject)
    {
        var element = containingProject.CreateElement(XMakeElements.sdk);

        return new ProjectSdkElement(element, containingProject)
        {
            Name = sdkName,
            Version = sdkVersion,
        };
    }
}
