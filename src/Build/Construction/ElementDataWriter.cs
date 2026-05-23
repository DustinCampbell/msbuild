// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// Serializes an ElementData-backed project tree directly to XML without materializing a DOM.
    /// This avoids the cost of building an <see cref="System.Xml.XmlDocument"/> just to call Save on it.
    /// </summary>
    internal sealed class ElementDataWriter
    {
        private readonly ProjectWriter _writer;
        private readonly bool _preserveFormatting;
        private readonly Encoding? _encoding;
        private int _depth;

        /// <summary>
        /// Creates a new writer that will serialize to the given <paramref name="textWriter"/>.
        /// </summary>
        /// <param name="textWriter">The destination for the XML output.</param>
        /// <param name="encoding">The encoding to declare in the XML declaration, or null for default UTF-8.</param>
        /// <param name="preserveFormatting">
        /// When true, uses stored whitespace/trivia from ElementData for faithful round-trip.
        /// When false, auto-indents with 2-space indentation.
        /// </param>
        internal ElementDataWriter(TextWriter textWriter, Encoding? encoding, bool preserveFormatting)
        {
            _writer = new ProjectWriter(textWriter);
            _preserveFormatting = preserveFormatting;
            _encoding = encoding;
            _depth = 0;

            if (!preserveFormatting)
            {
                _writer.Formatting = Formatting.Indented;
                _writer.Indentation = 2;
                _writer.IndentChar = ' ';
            }
            else
            {
                _writer.Formatting = Formatting.None;
            }
        }

        /// <summary>
        /// Serializes the entire project to the writer.
        /// </summary>
        internal void Write(ProjectRootElement project)
        {
            // Match the DOM path behavior: write XML declaration if the source had one,
            // or if the writer's encoding is non-UTF8 (e.g., StringWriter uses UTF-16).
            bool writeDeclaration = project.HasXmlDeclarationNode ||
                _encoding?.IsUtf8Encoding() == false;
            if (writeDeclaration)
            {
                string encodingName = _encoding?.WebName ?? "utf-8";
                _writer.WriteProcessingInstruction("xml", $"version=\"1.0\" encoding=\"{encodingName}\"");
            }

            WriteElement(project);

            _writer.Flush();
        }

        /// <summary>
        /// Serializes a single element (and its subtree) to the writer.
        /// Used by <see cref="ProjectElement.OuterElement"/>.
        /// </summary>
        internal void WriteElement(ProjectElement element)
        {
            var data = element.DataSource;
            if (data is null)
            {
                // Shouldn't happen for ElementData-backed elements, but fallback gracefully.
                return;
            }

            WriteLeadingTrivia(data);

            if (_preserveFormatting && data.LeadingWhitespace is not null)
            {
                _writer.WriteRaw(data.LeadingWhitespace);
            }
            else if (!_preserveFormatting && _depth > 0)
            {
                // Auto-indent is handled by XmlTextWriter when Formatting.Indented is set.
            }

            // Start element
            if (data.NamespaceURI.Length > 0)
            {
                _writer.WriteStartElement(data.Name, data.NamespaceURI);
            }
            else
            {
                _writer.WriteStartElement(data.Name);
            }

            // Write attributes
            WriteAttributes(data);

            // Write children or text content
            if (element is ProjectElementContainer container && HasSerializableChildren(container))
            {
                _depth++;
                WriteChildren(container);
                _depth--;

                // Trailing trivia (before closing tag)
                WriteTrailingTrivia(data);

                if (_preserveFormatting && data.LeadingWhitespace is not null && !data.IsSelfClosing)
                {
                    // Write indentation before closing tag — same as opening tag's indentation
                    _writer.WriteRaw(data.LeadingWhitespace);
                }

                _writer.WriteEndElement();
            }
            else if (data.TextContent is not null && data.TextContent.Length > 0)
            {
                // Use ProjectWriter.WriteString for item-vector-transform handling
                _writer.WriteString(data.TextContent);
                _writer.WriteFullEndElement();
            }
            else if (data.IsSelfClosing)
            {
                // WriteStartElement + WriteEndElement produces <Element /> for empty elements
                _writer.WriteEndElement();
            }
            else
            {
                // Empty element that was not self-closing: <Element></Element>
                _writer.WriteFullEndElement();
            }
        }

        private void WriteAttributes(ElementData data)
        {
            int count = data.AttributeCount;
            if (count == 0)
            {
                return;
            }

            var attributes = data.AttributeArray;
            if (attributes is null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                ref readonly var attr = ref attributes[i];
                _writer.WriteAttributeString(attr.Name, attr.Value);
            }
        }

        private static bool HasSerializableChildren(ProjectElementContainer container)
        {
            for (var child = container.FirstChild; child is not null; child = child.NextSibling)
            {
                if (child is ProjectMetadataElement { ExpressedAsAttribute: true })
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private void WriteChildren(ProjectElementContainer container)
        {
            for (var child = container.FirstChild; child is not null; child = child.NextSibling)
            {
                // Skip metadata expressed as attributes — they're already in the parent's attribute list.
                if (child is ProjectMetadataElement { ExpressedAsAttribute: true })
                {
                    continue;
                }

                WriteElement(child);
            }
        }

        private void WriteLeadingTrivia(ElementData data)
        {
            if (data.LeadingTrivia is null)
            {
                return;
            }

            foreach (ref readonly var trivia in data.LeadingTrivia.AsSpan())
            {
                switch (trivia.Kind)
                {
                    case XmlTriviaKind.Comment:
                        if (_preserveFormatting)
                        {
                            _writer.WriteRaw("<!--");
                            _writer.WriteRaw(trivia.Text);
                            _writer.WriteRaw("-->");
                        }
                        else
                        {
                            _writer.WriteComment(trivia.Text);
                        }
                        break;

                    case XmlTriviaKind.Whitespace:
                        if (_preserveFormatting)
                        {
                            _writer.WriteRaw(trivia.Text);
                        }
                        break;
                }
            }
        }

        private void WriteTrailingTrivia(ElementData data)
        {
            if (data.TrailingTrivia is null)
            {
                return;
            }

            foreach (ref readonly var trivia in data.TrailingTrivia.AsSpan())
            {
                switch (trivia.Kind)
                {
                    case XmlTriviaKind.Comment:
                        if (_preserveFormatting)
                        {
                            _writer.WriteRaw("<!--");
                            _writer.WriteRaw(trivia.Text);
                            _writer.WriteRaw("-->");
                        }
                        else
                        {
                            _writer.WriteComment(trivia.Text);
                        }
                        break;

                    case XmlTriviaKind.Whitespace:
                        if (_preserveFormatting)
                        {
                            _writer.WriteRaw(trivia.Text);
                        }
                        break;
                }
            }
        }
    }
}
