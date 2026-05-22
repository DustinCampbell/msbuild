// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
#if NET
using System.Runtime.InteropServices;
#endif

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Stores the data for a single attribute on an XML element: its name, value, and source location.
    /// </summary>
    [DebuggerDisplay("{Name}={Value} @{Location}")]
    internal sealed class AttributeData
    {
        /// <summary>
        /// The local name of the attribute.
        /// </summary>
        internal string Name { get; }

        /// <summary>
        /// The value of the attribute.
        /// </summary>
        internal string Value { get; set; }

        /// <summary>
        /// The source location of the attribute in the original file.
        /// </summary>
        internal ElementLocation Location { get; }

        internal AttributeData(string name, string value, ElementLocation location)
        {
            Name = name;
            Value = value;
            Location = location;
        }
    }

    /// <summary>
    /// Self-contained data storage for an MSBuild XML element, replacing the need for a backing <see cref="System.Xml.XmlElement"/>.
    /// Stores the element name, namespace, attributes (with locations), text content, and element location.
    /// </summary>
    /// <remarks>
    /// This is the core data structure for Phase 1 of the "Eliminate XML DOM" refactoring.
    /// It is designed to hold all information needed by <see cref="ProjectElement"/> without
    /// requiring an <see cref="System.Xml.XmlDocument"/> backing store.
    /// </remarks>
    [DebuggerDisplay("{Name} Attributes={_attributes.Count}")]
    internal sealed class ElementData
    {
        private readonly List<AttributeData> _attributes;

        /// <summary>
        /// The local name of this XML element (e.g., "PropertyGroup", "Compile", "Project").
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// The namespace URI of this element, or empty string if none.
        /// </summary>
        internal string NamespaceURI { get; }

        /// <summary>
        /// The source location of the element's opening tag.
        /// </summary>
        internal ElementLocation Location { get; }

        /// <summary>
        /// The text content of this element (inner text), or null if the element has child elements instead.
        /// For elements like &lt;MyProp&gt;value&lt;/MyProp&gt;, this is "value".
        /// </summary>
        internal string? TextContent { get; set; }

        /// <summary>
        /// Leading whitespace/indentation before this element in the original file.
        /// Used for formatting preservation during serialization.
        /// </summary>
        internal string? LeadingWhitespace { get; set; }

        /// <summary>
        /// Whether this element was self-closing in the original file (e.g., &lt;Compile Include="foo.cs" /&gt;).
        /// </summary>
        internal bool IsSelfClosing { get; set; }

        /// <summary>
        /// Creates a new <see cref="ElementData"/> with the specified element name, namespace, and location.
        /// </summary>
        internal ElementData(string name, string namespaceURI, ElementLocation location)
        {
            Name = name;
            NamespaceURI = namespaceURI ?? string.Empty;
            Location = location;
            _attributes = [];
        }

        /// <summary>
        /// Gets the number of attributes on this element.
        /// </summary>
        internal int AttributeCount => _attributes.Count;

        /// <summary>
        /// Gets the value of the attribute with the specified name, or null if not found.
        /// </summary>
        internal string? GetAttributeValue(string attributeName)
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return _attributes[i].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the location of the attribute with the specified name, or null if not found.
        /// </summary>
        internal ElementLocation? GetAttributeLocation(string attributeName)
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return _attributes[i].Location;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the <see cref="AttributeData"/> for the attribute with the specified name, or null if not found.
        /// </summary>
        internal AttributeData? GetAttribute(string attributeName)
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return _attributes[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Sets or adds an attribute. If an attribute with the same name already exists, its value is updated.
        /// If it doesn't exist, a new attribute is added with the specified location (or a default empty location).
        /// </summary>
        internal void SetAttribute(string attributeName, string value, ElementLocation? location = null)
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    _attributes[i].Value = value;
                    return;
                }
            }

            _attributes.Add(new AttributeData(attributeName, value, location ?? ElementLocation.EmptyLocation));
        }

        /// <summary>
        /// Removes the attribute with the specified name. Returns true if it was found and removed.
        /// </summary>
        internal bool RemoveAttribute(string attributeName)
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    _attributes.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds an attribute. Does not check for duplicates — use during parsing when attributes are known to be unique.
        /// </summary>
        internal void AddAttribute(AttributeData attribute)
        {
            _attributes.Add(attribute);
        }

        /// <summary>
        /// Gets all attributes as a read-only span for enumeration (available on .NET Core only).
        /// </summary>
#if NET
        internal ReadOnlySpan<AttributeData> Attributes => CollectionsMarshal.AsSpan(_attributes);
#else
        internal IReadOnlyList<AttributeData> Attributes => _attributes;
#endif

        /// <summary>
        /// Gets all attributes as an enumerable list.
        /// </summary>
        internal IReadOnlyList<AttributeData> AttributeList => _attributes;

        /// <summary>
        /// Returns true if this element has an attribute with the specified name.
        /// </summary>
        internal bool HasAttribute(string attributeName)
        {
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes all attributes from this element.
        /// </summary>
        internal void ClearAttributes()
        {
            _attributes.Clear();
        }
    }
}
