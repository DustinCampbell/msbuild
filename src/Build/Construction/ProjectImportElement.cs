// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// Initializes a ProjectImportElement instance.
/// </summary>
[DebuggerDisplay("Project={Project} Condition={Condition}")]
public class ProjectImportElement : ProjectElement
{
    internal ProjectImportElementLink? ImportLink => (ProjectImportElementLink?)Link;

    [MemberNotNullWhen(true, nameof(ImportLink))]
    internal override bool IsLink => base.IsLink;

    private protected ImplicitImportLocation _implicitImportLocation;
    private protected ProjectElement? _originalElement;

    private protected ProjectImportElement(ProjectImportElementLink link)
        : base(link)
    {
    }

    private protected ProjectImportElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject, SdkReference? sdkReference)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
        SdkReference = sdkReference;
    }

    private protected ProjectImportElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Gets or sets the Project value.
    /// </summary>
    public string Project
    {
        get => FileUtilities.FixFilePath(GetAttributeValueOrEmpty(XMakeAttributes.project));
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value, XMakeAttributes.project);

            SetOrRemoveAttribute(XMakeAttributes.project, value, "Set Import Project {0}", value);
        }
    }

    /// <summary>
    /// Location of the project attribute.
    /// </summary>
    public ElementLocation ProjectLocation
        => GetAttributeLocation(XMakeAttributes.project);

    /// <summary>
    /// Gets or sets the SDK that contains the import.
    /// </summary>
    public string Sdk
    {
        get => FileUtilities.FixFilePath(GetAttributeValueOrEmpty(XMakeAttributes.sdk));
        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value, XMakeAttributes.sdk);

            SdkReference? sdkReference = SdkReference;
            if (UpdateSdkReference(name: value, sdkReference?.Version, sdkReference?.MinimumVersion))
            {
                SetOrRemoveAttribute(XMakeAttributes.sdk, value, "Set Import Sdk {0}", value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the version associated with this SDK import.
    /// </summary>
    public string Version
    {
        get => GetAttributeValueOrEmpty(XMakeAttributes.sdkVersion);
        set
        {
            SdkReference? sdkReference = SdkReference;
            if (UpdateSdkReference(sdkReference?.Name, version: value, sdkReference?.MinimumVersion))
            {
                SetOrRemoveAttribute(XMakeAttributes.sdkVersion, value, "Set Import Version {0}", value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the minimum SDK version required by this import.
    /// </summary>
    public string MinimumVersion
    {
        get => GetAttributeValueOrEmpty(XMakeAttributes.sdkMinimumVersion);
        set
        {
            SdkReference? sdkReference = SdkReference;
            if (UpdateSdkReference(sdkReference?.Name, sdkReference?.Version, minimumVersion: value))
            {
                SetOrRemoveAttribute(XMakeAttributes.sdkMinimumVersion, value, "Set Import Minimum Version {0}", value);
            }
        }
    }

    /// <summary>
    /// Location of the Sdk attribute.
    /// </summary>
    public ElementLocation SdkLocation
        => GetAttributeLocation(XMakeAttributes.sdk);

    /// <summary>
    /// Gets the <see cref="ImplicitImportLocation"/> of the import.  This indicates if the import was implicitly
    /// added because of the <see cref="ProjectRootElement.Sdk"/> attribute and the location where the project was
    /// imported.
    /// </summary>
    public ImplicitImportLocation ImplicitImportLocation
    {
        get => GetImplicitImportLocation();
        private protected set => _implicitImportLocation = value;
    }

    /// <summary>
    /// If the import is an implicit one (<see cref="ImplicitImportLocation"/> != None) then this element points
    /// to the original element which generated this implicit import.
    /// </summary>
    public ProjectElement? OriginalElement
    {
        get => GetOriginalElement();
        private protected set => _originalElement = value;
    }

    /// <summary>
    /// <see cref="Framework.SdkReference"/> if applicable to this import element.
    /// </summary>
    internal SdkReference? SdkReference { get; private set; }

    /// <summary>
    /// Creates an unparented ProjectImportElement, wrapping an unparented XmlElement.
    /// Validates the project value.
    /// Caller should then ensure the element is added to a parent
    /// </summary>
    internal static ProjectImportElement CreateDisconnected(string project, ProjectRootElement containingProject)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);

        return new XmlProjectImportElement(element, containingProject)
        {
            Project = project,
        };
    }

    /// <summary>
    /// Creates an implicit ProjectImportElement as if it was in the project.
    /// </summary>
    /// <returns></returns>
    internal static ProjectImportElement CreateImplicit(
        string project,
        ProjectRootElement containingProject,
        ImplicitImportLocation implicitImportLocation,
        SdkReference sdkReference,
        ProjectElement originalElement)
    {
        XmlElementWithLocation element = containingProject.CreateElement(XMakeElements.import);

        return new XmlProjectImportElement(element, containingProject)
        {
            Project = project,
            Sdk = sdkReference.ToString(),
            ImplicitImportLocation = implicitImportLocation,
            SdkReference = sdkReference,
            OriginalElement = originalElement,
        };
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
        => newParent is ProjectRootElement or ProjectImportGroupElement;

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateImportElement(Project);

    internal virtual ImplicitImportLocation GetImplicitImportLocation()
        => IsLink ? ImportLink!.ImplicitImportLocation : _implicitImportLocation;

    internal virtual ProjectElement? GetOriginalElement()
        => IsLink ? ImportLink!.OriginalElement : _originalElement;

    /// <summary>
    /// Helper method to update the <see cref="SdkReference" /> property if necessary (update only when changed).
    /// </summary>
    /// <returns>True if the <see cref="SdkReference" /> property was updated, otherwise false (no update necessary).</returns>
    [MemberNotNullWhen(true, nameof(SdkReference))]
    private bool UpdateSdkReference(string? name, string? version, string? minimumVersion)
    {
        var sdk = new SdkReference(name, version, minimumVersion);

        if (sdk.Equals(SdkReference))
        {
            return false;
        }

        SdkReference = sdk;

        return true;
    }
}
