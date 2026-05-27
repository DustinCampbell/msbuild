// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// Linked (remote) implementation of <see cref="ProjectTaskElement"/>.
/// This subclass owns the <see cref="ProjectTaskElementLink"/>.
/// </summary>
internal sealed class LinkedProjectTaskElement : ProjectTaskElement
{
    internal LinkedProjectTaskElement(ProjectTaskElementLink link)
        : base(link)
    {
    }

    /// <inheritdoc />
    public override IDictionary<string, string> Parameters => TaskLink!.Parameters;

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, ElementLocation>> ParameterLocations => TaskLink!.ParameterLocations;

    /// <inheritdoc />
    public override string GetParameter(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return TaskLink!.GetParameter(name);
    }

    /// <inheritdoc />
    public override void SetParameter(string name, string unevaluatedValue)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(unevaluatedValue);
        ErrorUtilities.VerifyThrowArgument(!XMakeAttributes.IsSpecialTaskAttribute(name), "CannotAccessKnownAttributes", name);

        TaskLink!.SetParameter(name, unevaluatedValue);
    }

    /// <inheritdoc />
    public override void RemoveParameter(string name)
        => TaskLink!.RemoveParameter(name);

    /// <inheritdoc />
    public override void RemoveAllParameters()
        => TaskLink!.RemoveAllParameters();
}
