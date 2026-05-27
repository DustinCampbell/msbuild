// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// XML-backed implementation of <see cref="ProjectTaskElement"/>.
/// This subclass owns the <see cref="XmlElementWithLocation"/> directly.
/// </summary>
internal sealed class XmlProjectTaskElement : ProjectTaskElement
{
    internal XmlProjectTaskElement(XmlElementWithLocation xmlElement, ProjectTargetElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    internal XmlProjectTaskElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, containingProject)
    {
    }

    /// <inheritdoc />
    public override IDictionary<string, string> Parameters
    {
        get
        {
            lock (_locker)
            {
                EnsureParametersInitialized();

                var parameters = _parameters!;
                var parametersClone = new Dictionary<string, string>(parameters.Count, StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, (string, ElementLocation)> entry in parameters)
                {
                    parametersClone[entry.Key] = entry.Value.Item1;
                }

                return new ReadOnlyDictionary<string, string>(parametersClone);
            }
        }
    }

    /// <inheritdoc />
    public override IEnumerable<KeyValuePair<string, ElementLocation>> ParameterLocations
    {
        get
        {
            lock (_locker)
            {
                EnsureParametersInitialized();
                var parameterLocations = new List<KeyValuePair<string, ElementLocation>>();

                foreach (KeyValuePair<string, (string, ElementLocation)> entry in _parameters!)
                {
                    parameterLocations.Add(new KeyValuePair<string, ElementLocation>(entry.Key, entry.Value.Item2));
                }

                return parameterLocations;
            }
        }
    }

    /// <inheritdoc />
    public override string GetParameter(string name)
    {
        lock (_locker)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            EnsureParametersInitialized();

            return _parameters!.TryGetValue(name, out (string, ElementLocation) parameter)
                ? parameter.Item1
                : string.Empty;
        }
    }

    /// <inheritdoc />
    public override void SetParameter(string name, string unevaluatedValue)
    {
        lock (_locker)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(unevaluatedValue);
            ErrorUtilities.VerifyThrowArgument(!XMakeAttributes.IsSpecialTaskAttribute(name), "CannotAccessKnownAttributes", name);

            _parameters = null;
            XmlElement!.SetAttribute(name, unevaluatedValue);
            MarkDirty("Set task parameter {0}", name);
        }
    }

    /// <inheritdoc />
    public override void RemoveParameter(string name)
    {
        lock (_locker)
        {
            _parameters = null;
            XmlElement!.RemoveAttribute(name);
            MarkDirty("Remove task parameter {0}", name);
        }
    }

    /// <inheritdoc />
    public override void RemoveAllParameters()
    {
        lock (_locker)
        {
            _parameters = null;
            using RefArrayBuilder<XmlAttribute> toRemove = default;

            foreach (XmlAttribute attribute in XmlElement!.Attributes)
            {
                if (!XMakeAttributes.IsSpecialTaskAttribute(attribute.Name))
                {
                    toRemove.Add(attribute);
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (var attribute in toRemove.AsSpan())
                {
                    XmlElement!.RemoveAttributeNode(attribute);
                }

                MarkDirty("Remove all task parameters on {0}", Name);
            }
        }
    }
}
