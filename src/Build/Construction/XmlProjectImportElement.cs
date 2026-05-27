// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectImportElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectImportElement : ProjectImportElement
{
    internal XmlProjectImportElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject, SdkReference? sdkReference)
        : base(xmlElement, parent, containingProject, sdkReference)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    internal XmlProjectImportElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
        ArgumentNullException.ThrowIfNull(containingProject);
    }

    /// <inheritdoc />
    internal override ImplicitImportLocation GetImplicitImportLocation()
        => _implicitImportLocation;

    /// <inheritdoc />
    internal override ProjectElement? GetOriginalElement()
        => _originalElement;
}
