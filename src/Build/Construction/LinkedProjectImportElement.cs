// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectImportElement"/>.
/// This subclass owns the <see cref="ProjectImportElementLink"/>.
/// </summary>
internal sealed class LinkedProjectImportElement : ProjectImportElement
{
    internal LinkedProjectImportElement(ProjectImportElementLink link)
        : base(link)
    {
        ArgumentNullException.ThrowIfNull(link);
    }

    /// <inheritdoc />
    internal override ImplicitImportLocation GetImplicitImportLocation()
        => ImportLink!.ImplicitImportLocation;

    /// <inheritdoc />
    internal override ProjectElement? GetOriginalElement()
        => ImportLink!.OriginalElement;
}
