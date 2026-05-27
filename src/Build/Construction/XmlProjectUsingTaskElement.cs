// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectUsingTaskElement"/>.
/// </summary>
internal sealed class XmlProjectUsingTaskElement : ProjectUsingTaskElement
{
    internal XmlProjectUsingTaskElement(XmlElementWithLocation xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectUsingTaskElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }
}
