// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectExtensionsElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectExtensionsElement : ProjectExtensionsElement
{
    internal XmlProjectExtensionsElement(XmlElementWithLocation xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectExtensionsElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    public override string Content
    {
        get => XmlElement!.InnerXml;

        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Content));

            XmlElement!.InnerXml = value;
            MarkDirty("Set ProjectExtensions raw {0}", value);
        }
    }

    /// <inheritdoc />
    public override string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            XmlElement? idElement = XmlElement![name];

            return idElement != null
                ? Internal.Utilities.RemoveXmlNamespace(idElement.InnerXml)
                : string.Empty;
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);

            XmlElement? idElement = XmlElement![name];

            if (idElement == null)
            {
                if (value.Length == 0)
                {
                    return;
                }

                idElement = XmlDocument!.CreateElement(name, XmlElement!.NamespaceURI);
                XmlElement!.AppendChild(idElement);
            }

            if (idElement.InnerXml != value &&
                idElement.InnerXml.Replace(ProjectRootElement.EmptyProjectFileXmlNamespace, string.Empty) != value)
            {
                if (value.Length == 0)
                {
                    XmlElement!.RemoveChild(idElement);
                }
                else
                {
                    idElement.InnerXml = value;
                }

                MarkDirty("Set ProjectExtensions content {0}", value);
            }
        }
    }
}
