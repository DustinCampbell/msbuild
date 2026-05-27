// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectItemGroupElement"/>.
/// </summary>
internal sealed class XmlProjectItemGroupElement : ProjectItemGroupElement
{
    internal XmlProjectItemGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectItemGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }
}
