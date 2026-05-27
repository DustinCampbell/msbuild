// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectUsingTaskParameterElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectUsingTaskParameterElement : ProjectUsingTaskParameterElement
{
    internal XmlProjectUsingTaskParameterElement(XmlElementWithLocation xmlElement, UsingTaskParameterGroupElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectUsingTaskParameterElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    public override string Name
    {
        get => ElementName;

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value, nameof(Name));

            XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement!, value, XmlElement!.NamespaceURI);
            ReplaceElement(newElement);
        }
    }
}
