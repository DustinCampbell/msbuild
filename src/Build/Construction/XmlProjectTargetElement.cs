// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectTargetElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectTargetElement : ProjectTargetElement
{
    internal XmlProjectTargetElement(XmlElementWithLocation xmlElement, ProjectRootElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectTargetElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    public override string Name
    {
        get
        {
            if (_name != null)
            {
                return _name;
            }

            string unescapedValue = EscapingUtilities.UnescapeAll(GetAttributeValueOrEmpty(XMakeAttributes.name));
            return _name = unescapedValue;
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            string unescapedValue = EscapingUtilities.UnescapeAll(value);

            int indexOfSpecialCharacter = unescapedValue.AsSpan().IndexOfAny(XMakeElements.InvalidTargetNameCharacters);
            if (indexOfSpecialCharacter >= 0)
            {
                ErrorUtilities.ThrowArgument("OM_NameInvalid", unescapedValue, unescapedValue[indexOfSpecialCharacter]);
            }

            SetOrRemoveAttribute(XMakeAttributes.name, unescapedValue, "Set target Name {0}", value);
            _name = unescapedValue;
        }
    }

    /// <inheritdoc />
    public override string? Returns
    {
        get => GetAttributeValueOrNull(XMakeAttributes.returns);

        set
        {
            XmlAttributeWithLocation? returnsAttribute = ProjectXmlUtilities.SetOrRemoveAttribute(
                XmlElement!,
                XMakeAttributes.returns,
                value,
                allowSettingEmptyAttributes: true);

            if (returnsAttribute != null)
            {
                (Parent as ProjectRootElement)?.ContainsTargetsWithReturnsAttribute = true;
            }

            MarkDirty("Set target Returns {0}", value);
        }
    }
}
