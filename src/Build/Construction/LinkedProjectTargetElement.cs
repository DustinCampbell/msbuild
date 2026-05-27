// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectTargetElement"/>.
/// This subclass owns the <see cref="ProjectTargetElementLink"/>.
/// </summary>
internal sealed class LinkedProjectTargetElement : ProjectTargetElement
{
    internal LinkedProjectTargetElement(ProjectTargetElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override string Name
    {
        get => TargetLink!.Name;

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            string unescapedValue = EscapingUtilities.UnescapeAll(value);

            int indexOfSpecialCharacter = unescapedValue.AsSpan().IndexOfAny(XMakeElements.InvalidTargetNameCharacters);
            if (indexOfSpecialCharacter >= 0)
            {
                ErrorUtilities.ThrowArgument("OM_NameInvalid", unescapedValue, unescapedValue[indexOfSpecialCharacter]);
            }

            TargetLink!.Name = value;
        }
    }

    /// <inheritdoc />
    public override string? Returns
    {
        get => base.Returns;
        set => TargetLink!.Returns = value;
    }
}
