// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectExtensionsElement"/>.
/// This subclass owns the <see cref="ProjectExtensionsElementLink"/>.
/// </summary>
internal sealed class LinkedProjectExtensionsElement : ProjectExtensionsElement
{
    internal LinkedProjectExtensionsElement(ProjectExtensionsElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override string Content
    {
        get => ExtensionLink!.Content;

        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(Content));
            ExtensionLink!.Content = value;
        }
    }

    /// <inheritdoc />
    public override string this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return ExtensionLink!.GetSubElement(name);
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);
            ExtensionLink!.SetSubElement(name, value);
        }
    }
}
