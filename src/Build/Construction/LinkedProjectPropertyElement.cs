// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectPropertyElement"/>.
/// This subclass owns the <see cref="ProjectPropertyElementLink"/>.
/// </summary>
internal sealed class LinkedProjectPropertyElement : ProjectPropertyElement
{
    internal LinkedProjectPropertyElement(ProjectPropertyElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override string Value
    {
        get => PropertyLink!.Value;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            PropertyLink!.Value = value;
        }
    }

    /// <inheritdoc />
    internal override void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrEmpty(newName);
        XmlUtilities.VerifyThrowArgumentValidElementName(newName);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newName), "CannotModifyReservedProperty", newName);

        PropertyLink!.ChangeName(newName);
    }
}
