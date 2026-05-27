// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectRootElement"/>.
/// </summary>
internal sealed class XmlProjectRootElement : ProjectRootElement
{
    internal XmlProjectRootElement(XmlReader xmlReader, ProjectRootElementCacheBase projectRootElementCache, bool isExplicitlyLoaded, bool preserveFormatting)
        : base(xmlReader, projectRootElementCache, isExplicitlyLoaded, preserveFormatting)
    {
    }

    internal XmlProjectRootElement(ProjectRootElementCacheBase projectRootElementCache, NewProjectFileOptions projectFileOptions, bool isEphemeral)
        : base(projectRootElementCache, projectFileOptions, isEphemeral)
    {
    }

    internal XmlProjectRootElement(ProjectRootElementCacheBase projectRootElementCache, NewProjectFileOptions projectFileOptions)
        : base(projectRootElementCache, projectFileOptions)
    {
    }

    internal XmlProjectRootElement(string path, ProjectRootElementCacheBase projectRootElementCache, bool preserveFormatting)
        : base(path, projectRootElementCache, preserveFormatting)
    {
    }

    internal XmlProjectRootElement(XmlDocumentWithLocation document, ProjectRootElementCacheBase projectRootElementCache)
        : base(document, projectRootElementCache)
    {
    }

    internal XmlProjectRootElement(XmlDocumentWithLocation document)
        : base(document)
    {
    }
}
