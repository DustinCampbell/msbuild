// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml;
using Microsoft.Build.Internal;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Construction
{
    /// <summary>
    /// A container for project elements
    /// </summary>
    public abstract class ProjectElementContainer : ProjectElement
    {
        private const string DEFAULT_INDENT = "  ";

        private int _count;
        private ProjectElement _firstChild;
        private ProjectElement _lastChild;

        internal ProjectElementContainerLink ContainerLink => (ProjectElementContainerLink)Link;

        /// <summary>
        /// External projects support
        /// </summary>
        internal ProjectElementContainer(ProjectElementContainerLink link)
            : base(link)
        {
        }

        /// <summary>
        /// Constructor called by ProjectRootElement only.
        /// XmlElement is set directly after construction.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment>
        internal ProjectElementContainer()
        {
        }

        /// <summary>
        /// Constructor called by derived classes, except from ProjectRootElement.
        /// Parameters may not be null, except parent.
        /// </summary>
        /// <comment>
        /// Should ideally be protected+internal.
        /// </comment>
        internal ProjectElementContainer(XmlElement xmlElement, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(xmlElement, parent, containingProject)
        {
        }

        /// <summary>
        /// Constructor for elements backed by ElementData (no XML DOM).
        /// </summary>
        internal ProjectElementContainer(ElementData elementData, ProjectElementContainer parent, ProjectRootElement containingProject)
            : base(elementData, parent, containingProject)
        {
        }

        /// <summary>
        /// Get an enumerator over all descendants in a depth-first manner.
        /// </summary>
        public IEnumerable<ProjectElement> AllChildren => GetDescendants();

        internal IEnumerable<T> GetAllChildrenOfType<T>()
            where T : ProjectElement
            => FirstChild == null
                ? Array.Empty<T>()
                : GetDescendantsOfType<T>();

        /// <summary>
        /// Get enumerable over all the children
        /// </summary>
        public ICollection<ProjectElement> Children
        {
            [DebuggerStepThrough]
            get => FirstChild == null
                ? Array.Empty<ProjectElement>()
                : new Collections.ReadOnlyCollection<ProjectElement>(new ProjectElementSiblingEnumerable(FirstChild));
        }

#pragma warning disable RS0030 // The ref to the banned API is in a doc comment
        /// <summary>
        /// Use this instead of <see cref="Children"/> to avoid boxing.
        /// </summary>
#pragma warning restore RS0030
        internal ProjectElementSiblingEnumerable ChildrenEnumerable => new ProjectElementSiblingEnumerable(FirstChild);

        internal ProjectElementSiblingSubTypeCollection<T> GetChildrenOfType<T>()
            where T : ProjectElement
            => FirstChild == null
                ? ProjectElementSiblingSubTypeCollection<T>.Empty
                : new ProjectElementSiblingSubTypeCollection<T>(FirstChild);

        /// <summary>
        /// Get enumerable over all the children, starting from the last
        /// </summary>
        public ICollection<ProjectElement> ChildrenReversed
        {
            [DebuggerStepThrough]
            get => LastChild == null
                ? Array.Empty<ProjectElement>()
                : new Collections.ReadOnlyCollection<ProjectElement>(new ProjectElementSiblingEnumerable(LastChild, forwards: false));
        }

        internal ProjectElementSiblingSubTypeCollection<T> GetChildrenReversedOfType<T>()
            where T : ProjectElement
            => LastChild == null
                ? ProjectElementSiblingSubTypeCollection<T>.Empty
                : new ProjectElementSiblingSubTypeCollection<T>(LastChild, forwards: false);

        /// <summary>
        /// Number of children of any kind
        /// </summary>
        public int Count { get => Link != null ? ContainerLink.Count : _count; private set => _count = value; }

        /// <summary>
        /// First child, if any, otherwise null.
        /// Cannot be set directly; use <see cref="PrependChild">PrependChild()</see>.
        /// </summary>
        public ProjectElement FirstChild { get => Link != null ? ContainerLink.FirstChild : _firstChild; private set => _firstChild = value; }

        /// <summary>
        /// Last child, if any, otherwise null.
        /// Cannot be set directly; use <see cref="AppendChild">AppendChild()</see>.
        /// </summary>
        public ProjectElement LastChild { get => Link != null ? ContainerLink.LastChild : _lastChild; private set => _lastChild = value; }

        /// <summary>
        /// Insert the child after the reference child.
        /// Reference child if provided must be parented by this element.
        /// Reference child may be null, in which case this is equivalent to <see cref="PrependChild">PrependChild(child)</see>.
        /// Throws if the parent is not itself parented.
        /// Throws if the reference node does not have this node as its parent.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        /// <remarks>
        /// Semantics are those of XmlNode.InsertAfterChild.
        /// </remarks>
        public void InsertAfterChild(ProjectElement child, ProjectElement reference)
        {
            ArgumentNullException.ThrowIfNull(child);
            if (Link != null)
            {
                ContainerLink.InsertAfterChild(child, reference);
                return;
            }

            if (reference == null)
            {
                PrependChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, reference);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, reference, reference.NextSibling);

            child.Parent = this;

            if (LastChild == reference)
            {
                LastChild = child;
            }

            child.PreviousSibling = reference;
            child.NextSibling = reference.NextSibling;

            reference.NextSibling = child;

            if (child.NextSibling != null)
            {
                Assumed.Equal(child.NextSibling.PreviousSibling, reference, "Invalid structure");
                child.NextSibling.PreviousSibling = child;
            }

            AddToXml(child);

            Count++;
            MarkDirty("Insert element {0}", child.ElementName);
        }

        /// <summary>
        /// Insert the child before the reference child.
        /// Reference child if provided must be parented by this element.
        /// Reference child may be null, in which case this is equivalent to <see cref="AppendChild">AppendChild(child)</see>.
        /// Throws if the parent is not itself parented.
        /// Throws if the reference node does not have this node as its parent.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        /// <remarks>
        /// Semantics are those of XmlNode.InsertBeforeChild.
        /// </remarks>
        public void InsertBeforeChild(ProjectElement child, ProjectElement reference)
        {
            ArgumentNullException.ThrowIfNull(child);

            if (Link != null)
            {
                ContainerLink.InsertBeforeChild(child, reference);
                return;
            }

            if (reference == null)
            {
                AppendChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, reference);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, reference.PreviousSibling, reference);

            child.Parent = this;

            if (FirstChild == reference)
            {
                FirstChild = child;
            }

            child.PreviousSibling = reference.PreviousSibling;
            child.NextSibling = reference;

            reference.PreviousSibling = child;

            if (child.PreviousSibling != null)
            {
                Assumed.Equal(child.PreviousSibling.NextSibling, reference, "Invalid structure");
                child.PreviousSibling.NextSibling = child;
            }

            AddToXml(child);

            Count++;
            MarkDirty("Insert element {0}", child.ElementName);
        }

        /// <summary>
        /// Inserts the provided element as the last child.
        /// Throws if the parent is not itself parented.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        public void AppendChild(ProjectElement child)
        {
            if (LastChild == null)
            {
                AddInitialChild(child);
            }
            else
            {
                Assumed.NotNull(FirstChild, "Invalid structure");
                InsertAfterChild(child, LastChild);
            }
        }

        /// <summary>
        /// Inserts the provided element as the first child.
        /// Throws if the parent is not itself parented.
        /// Throws if the node to add is already parented.
        /// Throws if the node to add was created from a different project than this node.
        /// </summary>
        public void PrependChild(ProjectElement child)
        {
            if (FirstChild == null)
            {
                AddInitialChild(child);
            }
            else
            {
                Assumed.NotNull(LastChild, "Invalid structure");
                InsertBeforeChild(child, FirstChild);
            }
        }

        /// <summary>
        /// Removes the specified child.
        /// Throws if the child is not currently parented by this object.
        /// This is O(1).
        /// May be safely called during enumeration of the children.
        /// </summary>
        /// <remarks>
        /// This is actually safe to call during enumeration of children, because it
        /// doesn't bother to clear the child's NextSibling (or PreviousSibling) pointers.
        /// To determine whether a child is unattached, check whether its parent is null,
        /// or whether its NextSibling and PreviousSibling point back at it.
        /// DO NOT BREAK THIS VERY USEFUL SAFETY CONTRACT.
        /// </remarks>
        public void RemoveChild(ProjectElement child)
        {
            ArgumentNullException.ThrowIfNull(child);

            ErrorUtilities.VerifyThrowArgument(child.Parent == this, "OM_NodeNotAlreadyParentedByThis");

            if (Link != null)
            {
                ContainerLink.RemoveChild(child);
                return;
            }

            child.ClearParent();

            if (child.PreviousSibling != null)
            {
                child.PreviousSibling.NextSibling = child.NextSibling;
            }

            if (child.NextSibling != null)
            {
                child.NextSibling.PreviousSibling = child.PreviousSibling;
            }

            if (ReferenceEquals(child, FirstChild))
            {
                FirstChild = child.NextSibling;
            }

            if (ReferenceEquals(child, LastChild))
            {
                LastChild = child.PreviousSibling;
            }

            RemoveFromXml(child);

            Count--;
            MarkDirty("Remove element {0}", child.ElementName);
        }

        /// <summary>
        /// Remove all the children, if any.
        /// </summary>
        /// <remarks>
        /// It is safe to modify the children in this way
        /// during enumeration. See <see cref="ProjectElementContainer.RemoveChild(ProjectElement)"/>.
        /// </remarks>
        public void RemoveAllChildren()
        {
            foreach (ProjectElement child in ChildrenEnumerable)
            {
                RemoveChild(child);
            }
        }

        /// <summary>
        /// Applies properties from the specified type to this instance.
        /// </summary>
        /// <param name="element">The element to act as a template to copy from.</param>
        public virtual void DeepCopyFrom(ProjectElementContainer element)
        {
            ArgumentNullException.ThrowIfNull(element);
            ErrorUtilities.VerifyThrowArgument(GetType().IsEquivalentTo(element.GetType()), "CannotCopyFromElementOfThatType");

            if (this == element)
            {
                return;
            }

            RemoveAllChildren();
            CopyFrom(element);

            foreach (ProjectElement child in element.ChildrenEnumerable)
            {
                if (child is ProjectElementContainer childContainer)
                {
                    childContainer.DeepClone(ContainingProject, this);
                }
                else
                {
                    AppendChild(child.Clone(ContainingProject));
                }
            }
        }

        /// <summary>
        /// Appends the provided child.
        /// Does not dirty the project, does not add an element, does not set the child's parent,
        /// and does not check the parent's future siblings and parent are acceptable.
        /// Called during project load, when the child can be expected to
        /// already have a parent and its element is already connected to the
        /// parent's element.
        /// All that remains is to set FirstChild/LastChild and fix up the linked list.
        /// </summary>
        internal void AppendParentedChildNoChecks(ProjectElement child)
        {
            Assumed.Equal(child.Parent, this, "Expected parent already set");
            Assumed.True(child.PreviousSibling == null && child.NextSibling == null, "Invalid structure");
            Assumed.Null(Link, "Attempt to edit a document that is not backed by a local xml is disallowed.");

            if (LastChild == null)
            {
                FirstChild = child;
            }
            else
            {
                child.PreviousSibling = LastChild;
                LastChild.NextSibling = child;
            }

            LastChild = child;

            Count++;
        }

        /// <summary>
        /// Returns a clone of this project element and all its children.
        /// </summary>
        /// <param name="factory">The factory to use for creating the new instance.</param>
        /// <param name="parent">The parent to append the cloned element to as a child.</param>
        /// <returns>The cloned element.</returns>
        protected internal virtual ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer parent)
        {
            var clone = (ProjectElementContainer)Clone(factory);
            parent?.AppendChild(clone);

            foreach (ProjectElement child in ChildrenEnumerable)
            {
                if (child is ProjectElementContainer childContainer)
                {
                    childContainer.DeepClone(clone.ContainingProject, clone);
                }
                else
                {
                    clone.AppendChild(child.Clone(clone.ContainingProject));
                }
            }

            return clone;
        }

        internal static ProjectElementContainer DeepClone(ProjectElementContainer xml, ProjectRootElement factory, ProjectElementContainer parent)
        {
            return xml.DeepClone(factory, parent);
        }

        private void SetElementAsAttributeValue(ProjectElement child)
        {
            Assumed.Null(Link, "Attempt to edit a document that is not backed by a local xml is disallowed.");

            // Assumes that child.ExpressedAsAttribute is true
            Debug.Assert(child.ExpressedAsAttribute, nameof(SetElementAsAttributeValue) + " method requires that " +
                nameof(child.ExpressedAsAttribute) + " property of child is true");

            // ElementData path: update attribute directly without DOM
            if (DataSource is not null)
            {
                string value = child.DataSource?.TextContent ?? string.Empty;
                DataSource.SetAttribute(child.ElementName, value);
                return;
            }

            string xmlValue = Internal.Utilities.GetXmlNodeInnerContents(child.XmlElement);
            ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, child.XmlElement.Name, xmlValue);
        }

        /// <summary>
        /// If child "element" is actually represented as an attribute, update the name in the corresponding Xml attribute
        /// </summary>
        /// <param name="child">A child element which might be represented as an attribute</param>
        /// <param name="oldName">The old name for the child element</param>
        internal void UpdateElementName(ProjectElement child, string oldName)
        {
            Assumed.Null(Link, "Attempt to edit a document that is not backed by a local xml is disallowed.");

            if (child.ExpressedAsAttribute)
            {
                // ElementData path: rename attribute without DOM
                if (DataSource is not null)
                {
                    DataSource.RemoveAttribute(oldName);
                    string value = child.DataSource?.TextContent ?? string.Empty;
                    DataSource.SetAttribute(child.ElementName, value);
                    return;
                }

                // To rename an attribute, we have to fully remove the old one and add a new one.
                XmlElement.RemoveAttribute(oldName);
                SetElementAsAttributeValue(child);
            }
        }

        /// <summary>
        /// If child "element" is actually represented as an attribute, update the value in the corresponding Xml attribute
        /// </summary>
        /// <param name="child">A child element which might be represented as an attribute</param>
        internal void UpdateElementValue(ProjectElement child)
        {
            Assumed.Null(Link, "Attempt to edit a document that is not backed by a local xml is disallowed.");

            if (child.ExpressedAsAttribute)
            {
                SetElementAsAttributeValue(child);
            }
        }

        /// <summary>
        /// Adds a ProjectElement to the Xml tree
        /// </summary>
        /// <param name="child">A child to add to the Xml tree, which has already been added to the ProjectElement tree</param>
        /// <remarks>
        /// The MSBuild construction APIs keep a tree of ProjectElements and a parallel Xml tree which consists of
        /// objects from System.Xml.  This is a helper method which adds an XmlElement or Xml attribute to the Xml
        /// tree after the corresponding ProjectElement has been added to the construction API tree, and fixes up
        /// whitespace as necessary.
        /// </remarks>
        internal void AddToXml(ProjectElement child)
        {
            Assumed.Null(Link, "Attempt to edit a document that is not backed by a local xml is disallowed.");

            // If the parent is ElementData-backed, handle whitespace formatting directly
            // in the ElementData model without materializing the DOM.
            if (DataSource is not null)
            {
                if (child.ExpressedAsAttribute)
                {
                    // Validate no duplicate attribute
                    ProjectErrorUtilities.VerifyThrowInvalidProject(!DataSource.HasAttribute(child.ElementName),
                        Location, "InvalidChildElementDueToDuplication", child.ElementName, ElementName);

                    // Store as attribute on this element's data
                    string value = child.DataSource?.TextContent ?? string.Empty;
                    DataSource.SetAttribute(child.ElementName, value);
                }
                else
                {
                    AddToElementData(child);
                }

                return;
            }

            if (child.ExpressedAsAttribute)
            {
                // todo children represented as attributes need to be placed in order too
                //  Assume that the name of the child has already been validated to conform with rules in XmlUtilities.VerifyThrowArgumentValidElementName

                // Make sure we're not trying to add multiple attributes with the same name
                ProjectErrorUtilities.VerifyThrowInvalidProject(!XmlElement.HasAttribute(child.XmlElement.Name),
                    XmlElement.Location, "InvalidChildElementDueToDuplication", child.XmlElement.Name, ElementName);

                SetElementAsAttributeValue(child);
            }
            else
            {
                // We want to add the XmlElement to the same position in the child list as the corresponding ProjectElement.
                //  Depending on whether the child ProjectElement has a PreviousSibling or a NextSibling, we may need to
                //  use the InsertAfter, InsertBefore, or AppendChild methods to add it in the right place.
                //
                //  Also, if PreserveWhitespace is true, then the elements we add won't automatically get indented, so
                //  we try to match the surrounding formatting.

                // Siblings, in either direction in the linked list, may be represented either as attributes or as elements.
                // Therefore, we need to traverse both directions to find the first sibling of the same type as the one being added.
                // If none is found, then the node being added is inserted as the only node of its kind

                bool SiblingIsExplicitElement(ProjectElement _) => !_.ExpressedAsAttribute;

                if (TrySearchLeftSiblings(child.PreviousSibling, SiblingIsExplicitElement, out ProjectElement referenceSibling))
                {
                    XmlNode insertAfter = referenceSibling.XmlElement;
                    XmlNode next = insertAfter.NextSibling;
                    while (next != null &&
                    (next.NodeType == XmlNodeType.Comment||
                    (next.NodeType == XmlNodeType.Whitespace && !next.Value.Contains("\n") && !next.Value.Contains("\r"))))
                    {
                        // If the next node is a comment or whitespace that does not contain newlines, then insert after it
                        insertAfter = next;
                        next = insertAfter.NextSibling;
                    }
                    XmlElement.InsertAfter(child.XmlElement, insertAfter);
                    if (XmlDocument.PreserveWhitespace)
                    {
                        // Try to match the surrounding formatting by checking the whitespace that precedes the node we inserted
                        //  after, and inserting the same whitespace between the previous node and the one we added
                        if (referenceSibling.XmlElement.PreviousSibling?.NodeType == XmlNodeType.Whitespace)
                        {
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(referenceSibling.XmlElement.PreviousSibling.Value);
                            XmlElement.InsertAfter(newWhitespaceNode, insertAfter);
                        }
                    }
                }
                else if (TrySearchRightSiblings(child.NextSibling, SiblingIsExplicitElement, out referenceSibling))
                {
                    // Add as first child
                    XmlElement.InsertBefore(child.XmlElement, referenceSibling.XmlElement);

                    if (XmlDocument.PreserveWhitespace)
                    {
                        // Try to match the surrounding formatting by checking the whitespace that precedes where we inserted
                        //  the new node, and inserting the same whitespace between the node we added and the one after it.
                        if (child.XmlElement.PreviousSibling?.NodeType == XmlNodeType.Whitespace)
                        {
                            var newWhitespaceNode = XmlDocument.CreateWhitespace(child.XmlElement.PreviousSibling.Value);
                            XmlElement.InsertBefore(newWhitespaceNode, referenceSibling.XmlElement);
                        }
                    }
                }
                else
                {
                    // Add as only child
                    XmlElement.AppendChild(child.XmlElement);

                    if (XmlDocument.PreserveWhitespace)
                    {
                        // If the empty parent has whitespace in it, delete it
                        if (XmlElement.FirstChild.NodeType == XmlNodeType.Whitespace)
                        {
                            XmlElement.RemoveChild(XmlElement.FirstChild);
                        }

                        var parentIndentation = GetElementIndentation(XmlElement);

                        var leadingWhitespaceNode = XmlDocument.CreateWhitespace(Environment.NewLine + parentIndentation + DEFAULT_INDENT);
                        var trailingWhiteSpaceNode = XmlDocument.CreateWhitespace(Environment.NewLine + parentIndentation);

                        XmlElement.InsertBefore(leadingWhitespaceNode, child.XmlElement);
                        XmlElement.InsertAfter(trailingWhiteSpaceNode, child.XmlElement);
                    }
                }
            }
        }

        /// <summary>
        /// Handles whitespace formatting when adding a child element to an ElementData-backed parent.
        /// Replicates the DOM-path whitespace behavior: matching sibling indentation or computing
        /// indentation for the first child of an empty container.
        /// </summary>
        private void AddToElementData(ProjectElement child)
        {
            if (!ContainingProject.PreserveFormatting)
            {
                // When not preserving formatting, the writer handles indentation automatically.
                // Just ensure the parent is not self-closing if it now has children.
                if (DataSource.IsSelfClosing)
                {
                    DataSource.IsSelfClosing = false;
                }

                return;
            }

            // Ensure child has an ElementData to set whitespace on.
            // Newly-created elements will already have one; elements moved from DOM-backed trees may not.
            var childData = child.DataSource;
            if (childData is null)
            {
                return;
            }

            bool SiblingIsExplicitElement(ProjectElement s) => !s.ExpressedAsAttribute;

            if (TrySearchLeftSiblings(child.PreviousSibling, SiblingIsExplicitElement, out ProjectElement leftSibling))
            {
                // Insert after a left sibling.
                // The DOM path skips inline comments/whitespace (no newlines) after the reference sibling
                // and inserts the new element after them. In ElementData, those inline items are stored
                // at the beginning of the NEXT sibling's LeadingTrivia. We need to move them to the
                // new element's LeadingTrivia so they appear between the reference and the new element.
                var nextSibling = child.NextSibling;
                while (nextSibling is not null && nextSibling.ExpressedAsAttribute)
                {
                    nextSibling = nextSibling.NextSibling;
                }

                // Determine the new element's indentation from the LEFT sibling (DOM copies referenceSibling's
                // preceding whitespace). The next sibling's trivia stays intact from splitIndex onward.
                string newIndent = GetIndentationFromSibling(leftSibling.DataSource);

                if (nextSibling?.DataSource?.LeadingTrivia is { } nextTrivia && nextTrivia.Length > 0)
                {
                    // Split: everything before the first newline-containing whitespace is "inline"
                    int splitIndex = FindNewlineWhitespaceIndex(nextTrivia);
                    if (splitIndex > 0)
                    {
                        // Move inline portion to the new child's LeadingTrivia
                        childData.LeadingTrivia = SliceTrivia(nextTrivia, 0, splitIndex);
                        SetLeadingWhitespace(childData, newIndent);

                        // Keep trivia from splitIndex onward on the next sibling (inclusive)
                        nextSibling.DataSource.LeadingTrivia = SliceTrivia(nextTrivia, splitIndex, nextTrivia.Length - splitIndex);
                    }
                    else
                    {
                        // No inline content — just set indentation from left sibling
                        SetLeadingWhitespace(childData, newIndent);
                    }
                }
                else if (DataSource.TrailingTrivia is { } parentTrailing && parentTrailing.Length > 0)
                {
                    // No next sibling — the new element is appended after the last child.
                    // Inline content (comments, non-newline whitespace) after the last child is stored
                    // in the parent's TrailingTrivia. Move it to the new element's LeadingTrivia.
                    int splitIndex = FindNewlineWhitespaceIndex(parentTrailing);
                    if (splitIndex > 0)
                    {
                        childData.LeadingTrivia = SliceTrivia(parentTrailing, 0, splitIndex);
                        SetLeadingWhitespace(childData, newIndent);

                        // Parent keeps trivia from splitIndex onward for closing tag indent
                        DataSource.TrailingTrivia = SliceTrivia(parentTrailing, splitIndex, parentTrailing.Length - splitIndex);
                    }
                    else
                    {
                        // No inline content in parent's trailing — just set indentation
                        SetLeadingWhitespace(childData, newIndent);
                    }
                }
                else
                {
                    // No trivia anywhere — use left sibling's indentation
                    SetLeadingWhitespace(childData, newIndent);
                }
            }
            else if (TrySearchRightSiblings(child.NextSibling, SiblingIsExplicitElement, out ProjectElement rightSibling))
            {
                // Insert before a right sibling: match its leading whitespace.
                // The DOM inserts the new element before the right sibling, then copies the
                // preceding whitespace to re-add before the right sibling. In ElementData terms:
                // both elements end up with the same indentation.
                var siblingData = rightSibling.DataSource;
                if (siblingData?.LeadingTrivia is { } rightTrivia && rightTrivia.Length > 0)
                {
                    int splitIndex = FindNewlineWhitespaceIndex(rightTrivia);
                    if (splitIndex > 0)
                    {
                        // Move inline portion (before splitIndex) to new child's LeadingTrivia.
                        // Use the whitespace at splitIndex as the new element's indentation (copy).
                        // Right sibling keeps trivia from splitIndex onward unchanged.
                        childData.LeadingTrivia = SliceTrivia(rightTrivia, 0, splitIndex);
                        SetLeadingWhitespace(childData, rightTrivia[splitIndex].Text);
                        siblingData.LeadingTrivia = SliceTrivia(rightTrivia, splitIndex, rightTrivia.Length - splitIndex);
                    }
                    else
                    {
                        // No inline content — copy the right sibling's indentation for the new element.
                        // Right sibling's trivia stays unchanged.
                        SetLeadingWhitespace(childData, rightTrivia[0].Text);
                    }
                }
                else if (siblingData?.LeadingWhitespace is not null)
                {
                    SetLeadingWhitespace(childData, siblingData.LeadingWhitespace);
                }
            }
            else
            {
                // Only child: compute indentation from parent
                string parentIndent = GetElementDataIndentation(DataSource);
                string childIndent = Environment.NewLine + parentIndent + DEFAULT_INDENT;

                childData.LeadingWhitespace = childIndent;

                // Parent is no longer self-closing since it has a child
                DataSource.IsSelfClosing = false;

                // For closing tag indentation: the writer uses LeadingWhitespace if set (for new elements),
                // or TrailingTrivia (for parsed elements that only have LeadingTrivia).
                if (DataSource.LeadingWhitespace is not null)
                {
                    // Writer will use LeadingWhitespace for closing tag indent; clear trailing trivia.
                    DataSource.TrailingTrivia = null;
                }
                else
                {
                    // Parsed element: set trailing trivia to provide closing tag indentation.
                    DataSource.TrailingTrivia = [new XmlTrivia(XmlTriviaKind.Whitespace, Environment.NewLine + parentIndent, 0, 0)];
                }
            }
        }

        /// <summary>
        /// Extracts the indentation portion (spaces/tabs after the last newline) from an ElementData's
        /// leading whitespace or leading trivia. Returns empty string if not determinable.
        /// </summary>
        private static string GetElementDataIndentation(ElementData data)
        {
            // First check LeadingWhitespace (set on programmatically-created elements)
            var ws = data?.LeadingWhitespace;
            if (ws is not null)
            {
                var lastNewline = ws.LastIndexOf('\n');
                if (lastNewline != -1)
                {
                    return ws.Substring(lastNewline + 1);
                }
            }

            // For parsed elements, indentation is in LeadingTrivia
            if (data?.LeadingTrivia is { } trivia)
            {
                for (int i = trivia.Length - 1; i >= 0; i--)
                {
                    if (trivia[i].Kind == XmlTriviaKind.Whitespace)
                    {
                        var text = trivia[i].Text;
                        var lastNewline = text.LastIndexOf('\n');
                        if (lastNewline != -1)
                        {
                            return text.Substring(lastNewline + 1);
                        }
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the full leading whitespace string from a sibling's ElementData, suitable for copying
        /// as the new element's indentation. The DOM copies the entire whitespace node preceding the
        /// reference sibling. This method returns equivalent text from either LeadingWhitespace or
        /// the last newline-containing whitespace trivia entry.
        /// </summary>
        private static string GetIndentationFromSibling(ElementData siblingData)
        {
            if (siblingData is null)
            {
                return Environment.NewLine;
            }

            // Programmatically-created elements use LeadingWhitespace
            if (siblingData.LeadingWhitespace is not null)
            {
                return siblingData.LeadingWhitespace;
            }

            // Parsed elements have indentation in LeadingTrivia
            if (siblingData.LeadingTrivia is not null)
            {
                return GetFullLeadingWhitespace(siblingData.LeadingTrivia) ?? Environment.NewLine;
            }

            return Environment.NewLine;
        }

        /// <summary>
        /// Extracts indentation from a trivia array by finding the last whitespace entry
        /// that contains a newline, then returning that text from the newline onward (including \r\n if present).
        /// </summary>
        /// <summary>
        /// Gets the full text of the last whitespace trivia entry that contains a newline.
        /// Unlike ExtractIndentationFromTrivia which returns only the last line's indentation,
        /// this returns the entire whitespace text including blank lines, matching DOM behavior
        /// which copies the entire preceding whitespace node.
        /// </summary>
        private static string GetFullLeadingWhitespace(XmlTrivia[] trivia)
        {
            for (int i = trivia.Length - 1; i >= 0; i--)
            {
                if (trivia[i].Kind == XmlTriviaKind.Whitespace &&
                    (trivia[i].Text.Contains('\n') || trivia[i].Text.Contains('\r')))
                {
                    return trivia[i].Text;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the leading whitespace on a child element, splitting multi-line whitespace
        /// so that only the last newline+indent goes into <see cref="ElementData.LeadingWhitespace"/>
        /// (which the writer uses for both opening and closing tag indentation) and any
        /// preceding blank lines go into <see cref="ElementData.LeadingTrivia"/> (used only before opening tag).
        /// </summary>
        private static void SetLeadingWhitespace(ElementData childData, string fullWhitespace)
        {
            if (fullWhitespace is null)
            {
                return;
            }

            // Find the last newline — everything from there is the "indent" portion
            int lastNewline = fullWhitespace.LastIndexOf('\n');
            if (lastNewline == -1)
            {
                // No newline at all — just spaces/tabs
                childData.LeadingWhitespace = fullWhitespace;
                return;
            }

            // Check for \r\n
            int splitPos = (lastNewline > 0 && fullWhitespace[lastNewline - 1] == '\r') ? lastNewline - 1 : lastNewline;

            if (splitPos == 0)
            {
                // Only one newline — entire string is the indentation
                childData.LeadingWhitespace = fullWhitespace;
                return;
            }

            // Multiple newlines: split into prefix (blank lines → LeadingTrivia) and suffix (indent → LeadingWhitespace)
            string prefix = fullWhitespace.Substring(0, splitPos);
            string suffix = fullWhitespace.Substring(splitPos);

            // Append to existing LeadingTrivia rather than overwriting (inline content may already be there)
            var prefixTrivia = new XmlTrivia(XmlTriviaKind.Whitespace, prefix, 0, 0);
            if (childData.LeadingTrivia is { } existing)
            {
                var combined = new XmlTrivia[existing.Length + 1];
                Array.Copy(existing, combined, existing.Length);
                combined[existing.Length] = prefixTrivia;
                childData.LeadingTrivia = combined;
            }
            else
            {
                childData.LeadingTrivia = [prefixTrivia];
            }

            childData.LeadingWhitespace = suffix;
        }

        /// <summary>
        /// Finds the index of the first whitespace trivia entry that contains a newline.
        /// Returns 0 if the first entry already contains a newline (no inline content).
        /// Returns trivia.Length if no newline-containing whitespace is found.
        /// The DOM path considers all comments and same-line whitespace (without newlines) as
        /// "inline trailing content" that belongs to the preceding sibling.
        /// </summary>
        private static int FindNewlineWhitespaceIndex(XmlTrivia[] trivia)
        {
            for (int i = 0; i < trivia.Length; i++)
            {
                if (trivia[i].Kind == XmlTriviaKind.Whitespace &&
                    (trivia[i].Text.Contains('\n') || trivia[i].Text.Contains('\r')))
                {
                    return i;
                }
            }

            return trivia.Length;
        }

        /// <summary>
        /// Creates a new array containing a subset of the given trivia array.
        /// Used instead of range operators for .NET Framework compatibility.
        /// </summary>
        private static XmlTrivia[] SliceTrivia(XmlTrivia[] source, int start, int length)
        {
            var result = new XmlTrivia[length];
            Array.Copy(source, start, result, 0, length);
            return result;
        }

        private static string GetElementIndentation(XmlElementWithLocation xmlElement)
        {
            if (xmlElement.PreviousSibling?.NodeType != XmlNodeType.Whitespace)
            {
                return string.Empty;
            }

            var leadingWhiteSpace = xmlElement.PreviousSibling.Value;

            var lastIndexOfNewLine = leadingWhiteSpace.LastIndexOf('\n');

            if (lastIndexOfNewLine == -1)
            {
                return string.Empty;
            }

            // the last newline is not included in the indentation, only what comes after it
            return leadingWhiteSpace.Substring(lastIndexOfNewLine + 1);
        }

        internal void RemoveFromXml(ProjectElement child)
        {
            Assumed.Null(Link, "Attempt to edit a document that is not backed by a local xml is disallowed.");

            // For ElementData-backed projects, handle removal without DOM.
            // The child is already removed from the linked list, so the serializer
            // will no longer see it. We only need to handle attribute removal and
            // mark the parent self-closing if it's now empty.
            if (DataSource is not null)
            {
                if (child.ExpressedAsAttribute)
                {
                    DataSource.RemoveAttribute(child.ElementName);
                }

                // If we removed the last non-attribute child, mark parent self-closing.
                // Exclude the child being removed since it may still be in the linked list
                // (e.g., when transitioning from element to attribute via ExpressedAsAttribute setter).
                if (!HasNonAttributeChildren(excluding: child))
                {
                    DataSource.IsSelfClosing = true;

                    if (ContainingProject.PreserveFormatting)
                    {
                        DataSource.TrailingTrivia = null;
                    }
                }

                return;
            }

            // Orphan elements (e.g. implicit SDK imports) that were never part of the DOM tree
            // don't need DOM removal — the construction model removal is sufficient.
            if (child.XmlElement is null)
            {
                return;
            }

            if (child.ExpressedAsAttribute)
            {
                XmlElement.RemoveAttribute(child.XmlElement.Name);
            }
            else
            {
                var previousSibling = child.XmlElement.PreviousSibling;

                XmlElement.RemoveChild(child.XmlElement);

                if (XmlDocument.PreserveWhitespace)
                {
                    // If we are trying to preserve formatting of the file, then also remove any whitespace
                    //  that came before the node we removed.
                    if (previousSibling?.NodeType == XmlNodeType.Whitespace)
                    {
                        XmlElement.RemoveChild(previousSibling);
                    }

                    // If we removed the last non-whitespace child node, set IsEmpty to true so that we get:
                    //      <ItemName />
                    //  instead of:
                    //      <ItemName>
                    //      </ItemName>
                    if (XmlElement.HasChildNodes)
                    {
                        if (XmlElement.ChildNodes.Cast<XmlNode>().All(c => c.NodeType == XmlNodeType.Whitespace))
                        {
                            XmlElement.IsEmpty = true;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns true if this container has any non-attribute children in the linked list.
        /// Used after a removal to determine if the container should become self-closing.
        /// </summary>
        private bool HasNonAttributeChildren(ProjectElement excluding = null)
        {
            for (var c = FirstChild; c is not null; c = c.NextSibling)
            {
                if (!c.ExpressedAsAttribute && !ReferenceEquals(c, excluding))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Sets the first child in this container
        /// </summary>
        internal void AddInitialChild(ProjectElement child)
        {
            Assumed.True(FirstChild == null && LastChild == null, "Expecting no children");

            if (Link != null)
            {
                ContainerLink.AddInitialChild(child);
                return;
            }

            VerifyForInsertBeforeAfterFirst(child, null);

            child.VerifyThrowInvalidOperationAcceptableLocation(this, null, null);

            child.Parent = this;

            FirstChild = child;
            LastChild = child;

            child.PreviousSibling = null;
            child.NextSibling = null;

            AddToXml(child);

            Count++;

            MarkDirty("Add child element named '{0}'", child.ElementName);
        }

        /// <summary>
        /// Common verification for insertion of an element.
        /// Reference may be null.
        /// </summary>
        private void VerifyForInsertBeforeAfterFirst(ProjectElement child, ProjectElement reference)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(Parent != null || ContainingProject == this, "OM_ParentNotParented");
            ErrorUtilities.VerifyThrowInvalidOperation(reference == null || reference.Parent == this, "OM_ReferenceDoesNotHaveThisParent");
            ErrorUtilities.VerifyThrowInvalidOperation(child.Parent == null, "OM_NodeAlreadyParented");
            ErrorUtilities.VerifyThrowInvalidOperation(child.ContainingProject == ContainingProject, "OM_MustBeSameProject");

            // In RemoveChild() we do not update the victim's NextSibling (or PreviousSibling) to null, to allow RemoveChild to be
            // called within an enumeration. So we can't expect these to be null if the child was previously removed. However, we
            // can expect that what they point to no longer point back to it. They've been reconnected.
            Assumed.True(child.NextSibling == null || child.NextSibling.PreviousSibling != this, "Invalid structure");
            Assumed.True(child.PreviousSibling == null || child.PreviousSibling.NextSibling != this, "Invalid structure");
            VerifyThrowInvalidOperationNotSelfAncestor(child);
        }

        /// <summary>
        /// Verifies that the provided element isn't this element or a parent of it.
        /// If it is, throws InvalidOperationException.
        /// </summary>
        private void VerifyThrowInvalidOperationNotSelfAncestor(ProjectElement element)
        {
            ProjectElement ancestor = this;

            while (ancestor != null)
            {
                ErrorUtilities.VerifyThrowInvalidOperation(ancestor != element, "OM_SelfAncestor");
                ancestor = ancestor.Parent;
            }
        }

        /// <summary>
        /// Recurses into the provided container (such as a choose) and finds all child elements, even if nested.
        /// Result does NOT include the element passed in.
        /// The caller could filter these.
        /// </summary>
        private IEnumerable<ProjectElement> GetDescendants()
        {
            ProjectElement child = FirstChild;

            while (child != null)
            {
                yield return child;

                if (child is ProjectElementContainer container)
                {
                    foreach (ProjectElement grandchild in container.AllChildren)
                    {
                        yield return grandchild;
                    }
                }

                child = child.NextSibling;
            }
        }

        private IEnumerable<T> GetDescendantsOfType<T>()
            where T : ProjectElement
        {
            ProjectElement child = FirstChild;

            while (child != null)
            {
                if (child is T childOfType)
                {
                    yield return childOfType;
                }

                if (child is ProjectElementContainer container)
                {
                    foreach (T grandchild in container.GetAllChildrenOfType<T>())
                    {
                        yield return grandchild;
                    }
                }

                child = child.NextSibling;
            }
        }

        private static bool TrySearchLeftSiblings(ProjectElement initialElement, Predicate<ProjectElement> siblingIsAcceptable, out ProjectElement referenceSibling)
        {
            return TrySearchSiblings(initialElement, siblingIsAcceptable, s => s.PreviousSibling, out referenceSibling);
        }

        private static bool TrySearchRightSiblings(ProjectElement initialElement, Predicate<ProjectElement> siblingIsAcceptable, out ProjectElement referenceSibling)
        {
            return TrySearchSiblings(initialElement, siblingIsAcceptable, s => s.NextSibling, out referenceSibling);
        }

        private static bool TrySearchSiblings(
            ProjectElement initialElement,
            Predicate<ProjectElement> siblingIsAcceptable,
            Func<ProjectElement, ProjectElement> nextSibling,
            out ProjectElement referenceSibling)
        {
            if (initialElement == null)
            {
                referenceSibling = null;
                return false;
            }

            var sibling = initialElement;

            while (sibling != null && !siblingIsAcceptable(sibling))
            {
                sibling = nextSibling(sibling);
            }

            referenceSibling = sibling;

            return referenceSibling != null;
        }

        internal sealed class ProjectElementSiblingSubTypeCollection<T> : ICollection<T>, ICollection
            where T : ProjectElement
        {
            private readonly ProjectElement _initial;
            private readonly bool _forwards;
            private List<T> _realizedElements;

            internal ProjectElementSiblingSubTypeCollection(ProjectElement initial, bool forwards = true)
            {
                _initial = initial;
                _forwards = forwards;
            }

            public static ProjectElementSiblingSubTypeCollection<T> Empty { get; } = new ProjectElementSiblingSubTypeCollection<T>(initial: null);

            public int Count => RealizedElements.Count;

            public bool IsReadOnly => true;

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => this;

            private List<T> RealizedElements
            {
                get
                {
                    if (_realizedElements == null)
                    {
                        // Note! Don't use the List ctor which takes an IEnumerable as it casts to an ICollection and calls Count,
                        // which leads to a StackOverflow exception in this implementation (see Count above)
                        List<T> list = new();
                        foreach (T element in this)
                        {
                            list.Add(element);
                        }

                        _realizedElements = list;
                    }

                    return _realizedElements;
                }
            }

            public void Add(T item) => ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");

            public void Clear() => ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");

            public bool Contains(T item) => RealizedElements.Contains(item);

            public void CopyTo(T[] array, int arrayIndex)
            {
                ArgumentNullException.ThrowIfNull(array);

                if (_realizedElements != null)
                {
                    _realizedElements.CopyTo(array, arrayIndex);
                }
                else
                {
                    int i = arrayIndex;
                    foreach (T entry in this)
                    {
                        array[i] = entry;
                        i++;
                    }
                }
            }

            public bool Remove(T item)
            {
                ErrorUtilities.ThrowInvalidOperation("OM_NotSupportedReadOnlyCollection");
                return false;
            }

            public Enumerator GetEnumerator() => new Enumerator(_initial, _forwards);

            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            void ICollection.CopyTo(Array array, int index)
            {
                ArgumentNullException.ThrowIfNull(array);

                int i = index;
                foreach (T entry in this)
                {
                    array.SetValue(entry, i);
                    i++;
                }
            }

            public struct Enumerator : IEnumerator<T>
            {
                // Note! Should not be readonly or we run into infinite loop issues with mutable structs
                private ProjectElementSiblingEnumerable.Enumerator _innerEnumerator;
                private T _current;

                internal Enumerator(ProjectElement initial, bool forwards = true)
                {
                    _innerEnumerator = new ProjectElementSiblingEnumerable.Enumerator(initial, forwards);
                }

                public T Current
                {
                    get
                    {
                        if (_current != null)
                        {
                            return _current;
                        }

                        throw new InvalidOperationException();
                    }
                }

                object IEnumerator.Current => Current;

                public readonly void Dispose()
                {
                }

                public bool MoveNext()
                {
                    while (_innerEnumerator.MoveNext())
                    {
                        ProjectElement innerCurrent = _innerEnumerator.Current;
                        if (innerCurrent is T innerCurrentOfType)
                        {
                            _current = innerCurrentOfType;
                            return true;
                        }
                    }

                    return false;
                }

                public void Reset()
                {
                    _innerEnumerator.Reset();
                    _current = null;
                }
            }
        }

        /// <summary>
        /// Enumerable over a series of sibling ProjectElement objects
        /// </summary>
        internal readonly struct ProjectElementSiblingEnumerable : IEnumerable<ProjectElement>
        {
            /// <summary>
            /// The enumerator
            /// </summary>
            private readonly Enumerator _enumerator;

            /// <summary>
            /// Constructor allowing reverse enumeration
            /// </summary>
            internal ProjectElementSiblingEnumerable(ProjectElement initial, bool forwards = true)
            {
                _enumerator = new Enumerator(initial, forwards);
            }

            /// <summary>
            /// Get enumerator
            /// </summary>
            public readonly Enumerator GetEnumerator() => _enumerator;

            /// <summary>
            /// Get non generic enumerator
            /// </summary>
            IEnumerator IEnumerable.GetEnumerator() => _enumerator;

            /// <summary>
            /// Get enumerator
            /// </summary>
            IEnumerator<ProjectElement> IEnumerable<ProjectElement>.GetEnumerator() => _enumerator;

            /// <summary>
            /// Enumerator over a series of sibling ProjectElement objects
            /// </summary>
            public struct Enumerator : IEnumerator<ProjectElement>
            {
                /// <summary>
                /// First element
                /// </summary>
                private readonly ProjectElement _initial;

                /// <summary>
                /// Whether enumeration should go forwards or backwards.
                /// If backwards, the "initial" will be the first returned, then each previous
                /// node in turn.
                /// </summary>
                private readonly bool _forwards;

                /// <summary>
                /// Constructor taking the first element
                /// </summary>
                internal Enumerator(ProjectElement initial, bool forwards)
                {
                    _initial = initial;
                    Current = null;
                    _forwards = forwards;
                }

                /// <summary>
                /// Current element
                /// Returns null if MoveNext() hasn't been called
                /// </summary>
                public ProjectElement Current { get; private set; }

                /// <summary>
                /// Current element.
                /// Throws if MoveNext() hasn't been called
                /// </summary>
                object IEnumerator.Current
                {
                    get
                    {
                        if (Current != null)
                        {
                            return Current;
                        }

                        throw new InvalidOperationException();
                    }
                }

                /// <summary>
                /// Dispose. Do nothing.
                /// </summary>
                public readonly void Dispose()
                {
                }

                /// <summary>
                /// Moves to the next item if any, otherwise returns false
                /// </summary>
                public bool MoveNext()
                {
                    ProjectElement next;

                    if (Current == null)
                    {
                        next = _initial;
                    }
                    else
                    {
                        next = _forwards ? Current.NextSibling : Current.PreviousSibling;
                    }

                    if (next != null)
                    {
                        Current = next;
                        return true;
                    }

                    return false;
                }

                /// <summary>
                /// Return to start
                /// </summary>
                public void Reset()
                {
                    Current = null;
                }
            }
        }
    }
}
