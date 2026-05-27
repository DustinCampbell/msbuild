// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectUsingTaskParameterElement"/>.
/// This subclass owns the <see cref="ProjectUsingTaskParameterElementLink"/>.
/// </summary>
internal sealed class LinkedProjectUsingTaskParameterElement : ProjectUsingTaskParameterElement
{
    internal LinkedProjectUsingTaskParameterElement(ProjectUsingTaskParameterElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override string Name
    {
        get => TaskParameterLink!.Name;

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value, nameof(Name));
            TaskParameterLink!.Name = value;
        }
    }
}
