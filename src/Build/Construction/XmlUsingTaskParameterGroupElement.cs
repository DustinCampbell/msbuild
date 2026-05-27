// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="UsingTaskParameterGroupElement"/>.
/// </summary>
internal sealed class XmlUsingTaskParameterGroupElement : UsingTaskParameterGroupElement
{
    internal XmlUsingTaskParameterGroupElement(XmlElementWithLocation xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlUsingTaskParameterGroupElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }
}
