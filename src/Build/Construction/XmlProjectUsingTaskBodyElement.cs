// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectUsingTaskBodyElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectUsingTaskBodyElement : ProjectUsingTaskBodyElement
{
    internal XmlProjectUsingTaskBodyElement(XmlElementWithLocation xmlElement, ProjectUsingTaskElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectUsingTaskBodyElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    public override string TaskBody
    {
        get => Internal.Utilities.GetXmlNodeInnerContents(XmlElement);

        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(TaskBody));
            Internal.Utilities.SetXmlNodeInnerContents(XmlElement, value);
            MarkDirty("Set usingtask body {0}", value);
        }
    }
}
