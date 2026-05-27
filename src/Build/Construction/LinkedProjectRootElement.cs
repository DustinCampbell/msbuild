// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectRootElement"/>.
/// </summary>
internal sealed class LinkedProjectRootElement : ProjectRootElement
{
    internal LinkedProjectRootElement(ProjectRootElementLink link)
        : base(link)
    {
    }
}
