// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Microsoft.Build.Internal;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// A container for project elements.
/// </summary>
public abstract class ProjectElementContainer : ProjectElement
{
    private const string DEFAULT_INDENT = "  ";

    internal ProjectElementContainerLink? ContainerLink => (ProjectElementContainerLink?)Link;

    [MemberNotNullWhen(true, nameof(ContainerLink))]
    internal override bool IsLink => base.IsLink;

    /// <summary>
    /// Constructor called by ProjectRootElement only.
    /// XmlElement is set directly after construction.
    /// </summary>
    private protected ProjectElementContainer()
    {
    }

    /// <summary>
    /// Constructor called by derived classes, except from ProjectRootElement.
    /// </summary>
    private protected ProjectElementContainer(XmlElementWithLocation xmlElement, ProjectElementContainer? parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
    }

    private protected ProjectElementContainer(ProjectElementContainerLink link)
        : base(link)
    {
    }

    /// <summary>
    /// Get an enumerator over all descendants in a depth-first manner.
    /// </summary>
    public IEnumerable<ProjectElement> AllChildren => GetDescendants();

    internal IEnumerable<T> GetAllChildrenOfType<T>()
        where T : ProjectElement
        => FirstChild != null
            ? GetDescendantsOfType<T>()
            : [];

    /// <summary>
    /// Get enumerable over all the children.
    /// </summary>
    public ICollection<ProjectElement> Children
    {
        [DebuggerStepThrough]
        get => FirstChild != null
            ? new Collections.ReadOnlyCollection<ProjectElement>(new ProjectElementSiblingEnumerable(FirstChild))
            : [];
    }

#pragma warning disable RS0030 // The ref to the banned API is in a doc comment
    /// <summary>
    /// Use this instead of <see cref="Children"/> to avoid boxing.
    /// </summary>
#pragma warning restore RS0030
    internal ProjectElementSiblingEnumerable ChildrenEnumerable
        => new(FirstChild);

    internal ProjectElementSiblingSubTypeCollection<T> GetChildrenOfType<T>()
        where T : ProjectElement
        => FirstChild != null
            ? new ProjectElementSiblingSubTypeCollection<T>(FirstChild)
            : ProjectElementSiblingSubTypeCollection<T>.Empty;

    /// <summary>
    /// Get enumerable over all the children, starting from the last
    /// </summary>
    public ICollection<ProjectElement> ChildrenReversed
    {
        [DebuggerStepThrough]
        get => LastChild != null
            ? new Collections.ReadOnlyCollection<ProjectElement>(new ProjectElementSiblingEnumerable(LastChild, forwards: false))
            : [];
    }

    internal ProjectElementSiblingSubTypeCollection<T> GetChildrenReversedOfType<T>()
        where T : ProjectElement
        => LastChild != null
            ? new ProjectElementSiblingSubTypeCollection<T>(LastChild, forwards: false)
            : ProjectElementSiblingSubTypeCollection<T>.Empty;

    /// <summary>
    /// Number of children of any kind.
    /// </summary>
    public int Count
    {
        get => IsLink ? ContainerLink.Count : field;
        private set;
    }

    /// <summary>
    /// First child, if any, otherwise null.
    /// Cannot be set directly; use <see cref="PrependChild">PrependChild()</see>.
    /// </summary>
    public ProjectElement? FirstChild
    {
        get => IsLink ? ContainerLink.FirstChild : field;
        private set;
    }

    /// <summary>
    /// Last child, if any, otherwise null.
    /// Cannot be set directly; use <see cref="AppendChild">AppendChild()</see>.
    /// </summary>
    public ProjectElement? LastChild
    {
        get => IsLink ? ContainerLink.LastChild : field;
        private set;
    }

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
    public void InsertAfterChild(ProjectElement child, ProjectElement? reference)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (IsLink)
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

        child.VerifyParent(this);
        child.VerifySiblings(reference, reference.NextSibling);

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
    public void InsertBeforeChild(ProjectElement child, ProjectElement? reference)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (IsLink)
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

        child.VerifyParent(this);
        child.VerifySiblings(reference.PreviousSibling, reference);

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

        if (IsLink)
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
    /// during enumeration. See <see cref="RemoveChild(ProjectElement)"/>.
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
    protected internal virtual ProjectElementContainer DeepClone(ProjectRootElement factory, ProjectElementContainer? parent)
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
        => xml.DeepClone(factory, parent);

    private void SetElementAsAttributeValue(ProjectElement child)
    {
        Assumed.False(IsLink, "Attempt to edit a document that is not backed by a local xml is disallowed.");

        // Assumes that child.ExpressedAsAttribute is true
        Debug.Assert(child.ExpressedAsAttribute, $"{nameof(SetElementAsAttributeValue)} method requires that {nameof(child.ExpressedAsAttribute)} property of child is true.");

        XmlElementWithLocation? childXmlElement = child.XmlElement;
        Assumed.NotNull(childXmlElement);

        string value = Internal.Utilities.GetXmlNodeInnerContents(childXmlElement);

        ProjectXmlUtilities.SetOrRemoveAttribute(XmlElement, childXmlElement.Name, value);
    }

    /// <summary>
    /// If child "element" is actually represented as an attribute, update the name in the corresponding Xml attribute
    /// </summary>
    /// <param name="child">A child element which might be represented as an attribute</param>
    /// <param name="oldName">The old name for the child element</param>
    internal void UpdateElementName(ProjectElement child, string oldName)
    {
        Assumed.False(IsLink, "Attempt to edit a document that is not backed by a local xml is disallowed.");

        if (child.ExpressedAsAttribute)
        {
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
        Assumed.False(IsLink, "Attempt to edit a document that is not backed by a local xml is disallowed.");

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
        Assumed.False(IsLink, "Attempt to edit a document that is not backed by a local xml is disallowed.");

        Assumed.NotNull(child.XmlElement);

        if (child.ExpressedAsAttribute)
        {
            // todo children represented as attributes need to be placed in order too
            //  Assume that the name of the child has already been validated to conform with rules in XmlUtilities.VerifyThrowArgumentValidElementName

            // Make sure we're not trying to add multiple attributes with the same name
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                !XmlElement.HasAttribute(child.XmlElement.Name),
                XmlElement.Location,
                "InvalidChildElementDueToDuplication",
                child.XmlElement.Name,
                ElementName);

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

            static bool SiblingIsExplicitElement(ProjectElement x) => !x.ExpressedAsAttribute;

            if (TrySearchLeftSiblings(child.PreviousSibling, SiblingIsExplicitElement, out ProjectElement? referenceSibling))
            {
                Assumed.NotNull(referenceSibling.XmlElement);

                XmlNode insertAfter = referenceSibling.XmlElement;

                XmlNode? next = insertAfter.NextSibling;
                while (next != null &&
                    (next.NodeType == XmlNodeType.Comment ||
                    (next.NodeType == XmlNodeType.Whitespace && next.Value is { Length: > 0 } s && s.IndexOfAny(['\n', '\r']) == -1)))
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
                    Assumed.NotNull(XmlElement.FirstChild);

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

    private static string GetElementIndentation(XmlElementWithLocation xmlElement)
    {
        if (xmlElement.PreviousSibling?.NodeType != XmlNodeType.Whitespace)
        {
            return string.Empty;
        }

        var leadingWhiteSpace = xmlElement.PreviousSibling.Value;
        if (leadingWhiteSpace is null)
        {
            return string.Empty;
        }

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
        Assumed.False(IsLink, "Attempt to edit a document that is not backed by a local xml is disallowed.");

        Assumed.NotNull(child.XmlElement);

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
                    bool allWhitespace = true;

                    foreach (XmlNode node in XmlElement.ChildNodes)
                    {
                        if (node.NodeType != XmlNodeType.Whitespace)
                        {
                            allWhitespace = false;
                            break;
                        }
                    }

                    if (allWhitespace)
                    {
                        XmlElement.IsEmpty = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Sets the first child in this container
    /// </summary>
    internal void AddInitialChild(ProjectElement child)
    {
        Assumed.True(FirstChild == null && LastChild == null, "Expecting no children");

        if (IsLink)
        {
            ContainerLink.AddInitialChild(child);
            return;
        }

        VerifyForInsertBeforeAfterFirst(child, reference: null);

        child.VerifyParent(this);
        child.VerifySiblings(previousSibling: null, nextSibling: null);

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
    private void VerifyForInsertBeforeAfterFirst(ProjectElement child, ProjectElement? reference)
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
        ProjectElement? ancestor = this;

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
        ProjectElement? child = FirstChild;

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
        ProjectElement? child = FirstChild;

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

    private static bool TrySearchLeftSiblings(ProjectElement? initialElement, Predicate<ProjectElement> siblingIsAcceptable, [NotNullWhen(true)] out ProjectElement? referenceSibling)
        => TrySearchSiblings(initialElement, siblingIsAcceptable, s => s.PreviousSibling, out referenceSibling);

    private static bool TrySearchRightSiblings(ProjectElement? initialElement, Predicate<ProjectElement> siblingIsAcceptable, [NotNullWhen(true)] out ProjectElement? referenceSibling)
        => TrySearchSiblings(initialElement, siblingIsAcceptable, s => s.NextSibling, out referenceSibling);

    private static bool TrySearchSiblings(
        ProjectElement? initialElement,
        Predicate<ProjectElement> siblingIsAcceptable,
        Func<ProjectElement, ProjectElement?> nextSibling,
        [NotNullWhen(true)] out ProjectElement? referenceSibling)
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

    internal sealed class ProjectElementSiblingSubTypeCollection<T>(ProjectElement? initial, bool forwards = true) : ICollection<T>, ICollection
        where T : ProjectElement
    {
        private readonly ProjectElement? _initial = initial;
        private readonly bool _forwards = forwards;
        private List<T>? _realizedElements;

        public static ProjectElementSiblingSubTypeCollection<T> Empty { get; } = new(initial: null);

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
                    List<T> list = [];

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

        public Enumerator GetEnumerator()
            => new(_initial, _forwards);

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
            => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

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

        public struct Enumerator(ProjectElement? initial, bool forwards = true) : IEnumerator<T>
        {
            // Note! Should not be readonly or we run into infinite loop issues with mutable structs
            private ProjectElementSiblingEnumerable.Enumerator _innerEnumerator = new(initial, forwards);
            private T _current = null!;

            public readonly T Current => _current ?? throw new InvalidOperationException();

            readonly object IEnumerator.Current => Current;

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
                _current = null!;
            }
        }
    }

    /// <summary>
    /// Enumerable over a series of sibling ProjectElement objects.
    /// </summary>
    internal readonly struct ProjectElementSiblingEnumerable(ProjectElement? initial, bool forwards = true) : IEnumerable<ProjectElement>
    {
        private readonly Enumerator _enumerator = new(initial, forwards);

        public readonly Enumerator GetEnumerator()
            => _enumerator;

        IEnumerator IEnumerable.GetEnumerator()
            => _enumerator;

        IEnumerator<ProjectElement> IEnumerable<ProjectElement>.GetEnumerator()
            => _enumerator;

        /// <summary>
        /// Enumerator over a series of sibling ProjectElement objects
        /// </summary>
        public struct Enumerator : IEnumerator<ProjectElement>
        {
            /// <summary>
            /// First element.
            /// </summary>
            private readonly ProjectElement? _initial;

            /// <summary>
            /// Whether enumeration should go forwards or backwards.
            /// If backwards, the "initial" will be the first returned, then each previous
            /// node in turn.
            /// </summary>
            private readonly bool _forwards;

            internal Enumerator(ProjectElement? initial, bool forwards)
            {
                _initial = initial;
                Current = null!;
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
            readonly object IEnumerator.Current
                => Current ?? throw new InvalidOperationException();

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
                ProjectElement? next = Current == null
                    ? _initial
                    : _forwards ? Current.NextSibling : Current.PreviousSibling;

                if (next != null)
                {
                    Current = next;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Return to start.
            /// </summary>
            public void Reset()
            {
                Current = null!;
            }
        }
    }
}
