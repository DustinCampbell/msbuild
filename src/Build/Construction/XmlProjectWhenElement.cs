// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectWhenElement"/>.
/// </summary>
internal sealed class XmlProjectWhenElement : ProjectWhenElement
{
    internal XmlProjectWhenElement(XmlElementWithLocation xmlElement, ProjectChooseElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectWhenElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }
}
