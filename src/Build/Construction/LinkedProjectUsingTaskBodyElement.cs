// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectUsingTaskBodyElement"/>.
/// This subclass owns the <see cref="ProjectUsingTaskBodyElementLink"/>.
/// </summary>
internal sealed class LinkedProjectUsingTaskBodyElement : ProjectUsingTaskBodyElement
{
    internal LinkedProjectUsingTaskBodyElement(ProjectUsingTaskBodyElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override string TaskBody
    {
        get => UsingTaskBodyLink!.TaskBody;

        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(TaskBody));
            UsingTaskBodyLink!.TaskBody = value;
        }
    }
}
