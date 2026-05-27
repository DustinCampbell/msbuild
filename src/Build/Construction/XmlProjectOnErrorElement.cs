// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectOnErrorElement"/>.
/// </summary>
internal sealed class XmlProjectOnErrorElement : ProjectOnErrorElement
{
    internal XmlProjectOnErrorElement(XmlElementWithLocation xmlElement, ProjectTargetElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectOnErrorElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }
}
