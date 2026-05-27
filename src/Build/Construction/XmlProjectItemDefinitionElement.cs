// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectItemDefinitionElement"/>.
/// </summary>
internal sealed class XmlProjectItemDefinitionElement : ProjectItemDefinitionElement
{
    internal XmlProjectItemDefinitionElement(XmlElementWithLocation xmlElement, ProjectItemDefinitionGroupElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectItemDefinitionElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }
}
