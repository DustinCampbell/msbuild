// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.ObjectModelRemoting;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// The kind of XML trivia (non-element content preserved for round-tripping).
    /// </summary>
    internal enum XmlTriviaKind : byte
    {
        /// <summary>An XML comment (&lt;!-- text --&gt;).</summary>
        Comment,

        /// <summary>Whitespace between elements.</summary>
        Whitespace,
    }

    /// <summary>
    /// A piece of XML trivia (comment or whitespace) that appears between elements.
    /// Stored as leading trivia on the next sibling or trailing trivia on the parent container.
    /// Modeled after Roslyn's SyntaxTrivia for minimal-allocation round-trip fidelity.
    /// </summary>
    [DebuggerDisplay("{Kind}: {Text}")]
    internal readonly struct XmlTrivia
    {
        /// <summary>The kind of trivia.</summary>
        internal XmlTriviaKind Kind { get; }

        /// <summary>The text content (comment body or whitespace characters).</summary>
        internal string Text { get; }

        /// <summary>Line number in the source file (1-based, 0 = unknown).</summary>
        internal int Line { get; }

        /// <summary>Column number in the source file (1-based, 0 = unknown).</summary>
        internal int Column { get; }

        internal XmlTrivia(XmlTriviaKind kind, string text, int line, int column)
        {
            Kind = kind;
            Text = text;
            Line = line;
            Column = column;
        }
    }
    /// <summary>
    /// Stores the data for a single attribute on an XML element: its name, value, and source location.
    /// This is a value type to avoid per-attribute heap allocations. Location is stored inline
    /// as line/column and the <see cref="ElementLocation"/> is created lazily on demand.
    /// </summary>
    [DebuggerDisplay("{Name}={Value} @({Line},{Column})")]
    internal struct AttributeData
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
        /// The line number of the attribute in the source file (1-based, 0 = unknown).
        /// </summary>
        internal int Line { get; }

        /// <summary>
        /// The column number of the attribute in the source file (1-based, 0 = unknown).
        /// </summary>
        internal int Column { get; }

        internal AttributeData(string name, string value, int line, int column)
        {
            Name = name;
            Value = value;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Creates an <see cref="ElementLocation"/> for this attribute using the given file path.
        /// This allocates — call only when the location is actually needed.
        /// </summary>
        internal ElementLocation GetLocation(string filePath) => ElementLocation.Create(filePath, Line, Column);
    }

    /// <summary>
    /// Self-contained data storage for an MSBuild XML element, replacing the need for a backing <see cref="System.Xml.XmlElement"/>.
    /// Stores the element name, namespace, attributes (with locations), text content, and element location.
    /// Location data is stored inline to avoid per-element <see cref="ElementLocation"/> allocations;
    /// the <see cref="Location"/> property creates one lazily on demand.
    /// </summary>
    /// <remarks>
    /// This is the core data structure for the "Eliminate XML DOM" refactoring.
    /// It is designed to hold all information needed by <see cref="ProjectElement"/> without
    /// requiring an <see cref="System.Xml.XmlDocument"/> backing store.
    /// Implements <see cref="ILinkedXml"/> so it can be stored in the single <c>_xmlSource</c> field
    /// of <see cref="ProjectElement"/>, alongside <see cref="XmlElementWithLocation"/> and
    /// <see cref="ProjectElementLink"/> as the three possible backing sources.
    /// </remarks>
    [DebuggerDisplay("{Name} Attributes={AttributeCount}")]
    internal sealed class ElementData : ILinkedXml
    {
        private AttributeData[]? _attributes;

        // ILinkedXml implementation — ElementData is a third variant alongside XmlElementWithLocation and ProjectElementLink.
        ProjectElementLink ILinkedXml.Link => null!;
        XmlElementWithLocation ILinkedXml.Xml => null!;

        /// <summary>
        /// The local name of this XML element (e.g., "PropertyGroup", "Compile", "Project").
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// The namespace URI of this element, or empty string if none.
        /// </summary>
        internal string NamespaceURI { get; }

        /// <summary>
        /// The file path for this element's source file.
        /// Shared with all attribute locations within this element to avoid redundant string references.
        /// </summary>
        internal string FilePath { get; }

        /// <summary>
        /// The line number of the element's opening tag (1-based, 0 = unknown).
        /// </summary>
        internal int Line { get; }

        /// <summary>
        /// The column number of the element's opening tag (1-based, 0 = unknown).
        /// </summary>
        internal int Column { get; }

        /// <summary>
        /// Gets the source location of the element's opening tag.
        /// Creates an <see cref="ElementLocation"/> on demand — avoid calling in tight loops.
        /// </summary>
        internal ElementLocation Location => ElementLocation.Create(FilePath, Line, Column);

        /// <summary>
        /// The text content of this element (inner text), or null if the element has child elements instead.
        /// For elements like &lt;MyProp&gt;value&lt;/MyProp&gt;, this is "value".
        /// When <see cref="IsInnerXml"/> is true, this contains raw XML markup (e.g., for UsingTaskBody, ProjectExtensions).
        /// </summary>
        internal string? TextContent { get; set; }

        /// <summary>
        /// When true, <see cref="TextContent"/> contains raw XML markup that should be set via
        /// <c>XmlElement.InnerXml</c> rather than added as a text node during DOM materialization.
        /// </summary>
        internal bool IsInnerXml { get; set; }

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
        /// Leading trivia (comments, whitespace) that appears before this element in document order.
        /// For the first child of a container, this is trivia between the container's open tag and the first child.
        /// For subsequent children, this is trivia between the previous sibling's end and this element's start.
        /// Null if no leading trivia exists.
        /// </summary>
        internal XmlTrivia[]? LeadingTrivia { get; set; }

        /// <summary>
        /// Trailing trivia (comments, whitespace) that appears after the last child element
        /// and before the container's closing tag. Only meaningful for container elements.
        /// Null if no trailing trivia exists.
        /// </summary>
        internal XmlTrivia[]? TrailingTrivia { get; set; }

        /// <summary>
        /// Creates a new <see cref="ElementData"/> with the specified element name, namespace, file path, and line/column.
        /// </summary>
        internal ElementData(string name, string namespaceURI, string filePath, int line, int column)
        {
            Name = name;
            NamespaceURI = namespaceURI ?? string.Empty;
            FilePath = filePath ?? string.Empty;
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Gets the number of attributes on this element.
        /// </summary>
        internal int AttributeCount => _attributes?.Length ?? 0;

        /// <summary>
        /// Gets the value of the attribute with the specified name, or null if not found.
        /// </summary>
        internal string? GetAttributeValue(string attributeName)
        {
            if (_attributes is null)
            {
                return null;
            }

            for (int i = 0; i < _attributes.Length; i++)
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
            return GetAttributeLocationWithPath(attributeName, FilePath);
        }

        /// <summary>
        /// Gets the location of the attribute with the specified name using an overridden file path.
        /// Used when ContainingProject.FullPath differs from the parse-time FilePath.
        /// </summary>
        internal ElementLocation? GetAttributeLocationWithPath(string attributeName, string filePath)
        {
            if (_attributes is null)
            {
                return null;
            }

            for (int i = 0; i < _attributes.Length; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return _attributes[i].GetLocation(filePath);
                }
            }

            return null;
        }

        /// <summary>
        /// Sets or adds an attribute. If an attribute with the same name already exists, its value is updated.
        /// If it doesn't exist, a new attribute is appended.
        /// </summary>
        internal void SetAttribute(string attributeName, string value)
        {
            if (_attributes is not null)
            {
                for (int i = 0; i < _attributes.Length; i++)
                {
                    if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                    {
                        _attributes[i].Value = value;
                        return;
                    }
                }
            }

            // Grow array by one
            var newArray = new AttributeData[(_attributes?.Length ?? 0) + 1];
            _attributes?.CopyTo(newArray, 0);
            newArray[newArray.Length - 1] = new AttributeData(attributeName, value, 0, 0);
            _attributes = newArray;
        }

        /// <summary>
        /// Removes the attribute with the specified name. Returns true if it was found and removed.
        /// </summary>
        internal bool RemoveAttribute(string attributeName)
        {
            if (_attributes is null)
            {
                return false;
            }

            for (int i = 0; i < _attributes.Length; i++)
            {
                if (string.Equals(_attributes[i].Name, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    if (_attributes.Length == 1)
                    {
                        _attributes = null;
                    }
                    else
                    {
                        var newArray = new AttributeData[_attributes.Length - 1];
                        Array.Copy(_attributes, 0, newArray, 0, i);
                        Array.Copy(_attributes, i + 1, newArray, i, _attributes.Length - i - 1);
                        _attributes = newArray;
                    }

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the attribute array directly. Used during parsing when all attributes are known at once.
        /// The array is stored by reference — the caller must not modify it afterward.
        /// </summary>
        internal void SetAttributeArray(AttributeData[]? attributes)
        {
            _attributes = attributes;
        }

        /// <summary>
        /// Gets all attributes as a span for enumeration.
        /// </summary>
        internal ReadOnlySpan<AttributeData> Attributes => _attributes.AsSpan();

        /// <summary>
        /// Gets all attributes as an array (may be null if no attributes).
        /// </summary>
        internal AttributeData[]? AttributeArray => _attributes;

        /// <summary>
        /// Returns true if this element has an attribute with the specified name.
        /// </summary>
        internal bool HasAttribute(string attributeName)
        {
            if (_attributes is null)
            {
                return false;
            }

            for (int i = 0; i < _attributes.Length; i++)
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
            _attributes = null;
        }

        /// <summary>
        /// Copies all data from the source <see cref="ElementData"/> into this instance,
        /// replacing existing attributes and text content.
        /// </summary>
        internal void CopyFrom(ElementData source)
        {
            Name = source.Name;
            TextContent = source.TextContent;

            if (source._attributes is null)
            {
                _attributes = null;
            }
            else
            {
                _attributes = new AttributeData[source._attributes.Length];
                Array.Copy(source._attributes, _attributes, source._attributes.Length);
            }
        }

        /// <summary>
        /// Creates an <see cref="ElementData"/> from an existing <see cref="ProjectElement"/>,
        /// reading its attributes and text content from whatever backing source it uses (DOM or Link).
        /// </summary>
        internal static ElementData FromProjectElement(ProjectElement element)
        {
            var location = element.Location;
            var data = new ElementData(element.ElementName, string.Empty, location.File, location.Line, location.Column);

            if (element.Link != null)
            {
                var remoteAttributes = element.Link.Attributes;
                var attrs = new AttributeData[remoteAttributes.Count];
                int index = 0;
                foreach (var remoteAttribute in remoteAttributes)
                {
                    attrs[index++] = new AttributeData(remoteAttribute.LocalName, remoteAttribute.Value, 0, 0);
                }

                data._attributes = attrs;
                data.TextContent = element.Link.PureText;
            }
            else
            {
                var xmlElement = element.XmlElement;
                if (xmlElement.Attributes.Count > 0)
                {
                    var attrs = new AttributeData[xmlElement.Attributes.Count];
                    for (int i = 0; i < xmlElement.Attributes.Count; i++)
                    {
                        var attribute = xmlElement.Attributes[i]!;
                        attrs[i] = new AttributeData(attribute.LocalName, attribute.Value, 0, 0);
                    }

                    data._attributes = attrs;
                }

                if (xmlElement.ChildNodes.Count == 1 && xmlElement.FirstChild!.NodeType == System.Xml.XmlNodeType.Text)
                {
                    data.TextContent = xmlElement.FirstChild.Value;
                }
            }

            return data;
        }
    }
}
