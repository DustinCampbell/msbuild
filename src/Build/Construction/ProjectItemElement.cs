// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using Microsoft.Build.ObjectModelRemoting;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Construction;

/// <summary>
/// ProjectItemElement class represents the Item element in the MSBuild project.
/// </summary>
[DebuggerDisplay("{ItemType} Include={Include} Exclude={Exclude} #Metadata={Count} Condition={Condition}")]
public class ProjectItemElement : ProjectElementContainer
{
    private string? _include;
    private string? _exclude;
    private string? _remove;
    private string? _matchOnMetadata;
    private string? _update;

    /// <summary>
    /// Whether the include value has wildcards, cached for performance.
    /// </summary>
    private bool? _includeHasWildcards;

    internal ProjectItemElementLink? ItemLink => (ProjectItemElementLink?)Link;

    [MemberNotNullWhen(true, nameof(ItemLink))]
    internal override bool IsLink => base.IsLink;

    internal ProjectItemElement(ProjectItemElementLink link)
        : base(link)
    {
    }

    internal ProjectItemElement(XmlElementWithLocation xmlElement, ProjectItemGroupElement parent, ProjectRootElement containingProject)
        : base(xmlElement, parent, containingProject)
    {
        ArgumentNullException.ThrowIfNull(parent);
    }

    private ProjectItemElement(XmlElementWithLocation xmlElement, ProjectRootElement containingProject)
        : base(xmlElement, parent: null, containingProject)
    {
    }

    /// <summary>
    /// Gets the item's type.
    /// </summary>
    public string ItemType
    {
        [DebuggerStepThrough]
        get => ElementName;
        set => ChangeItemType(value);
    }

    /// <summary>
    /// Gets or sets the Include value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string Include
    {
        // No thread-safety lock required here because many reader threads would set the same value to the field.
        [DebuggerStepThrough]
        get => GetAndCachedAttributeValue(XMakeAttributes.include, ref _include) ?? string.Empty;

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || (Remove.Length == 0 && Update.Length == 0),
                "OM_OneOfAttributeButNotMore",
                ElementName,
                XMakeAttributes.include,
                XMakeAttributes.remove,
                XMakeAttributes.update);

            SetOrRemoveAttribute(XMakeAttributes.include, value, ref _include, "Set item Include {0}", value);
            _includeHasWildcards = null;
        }
    }

    /// <summary>
    /// Gets or sets the Exclude value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string Exclude
    {
        // No thread-safety lock required here because many reader threads would set the same value to the field.
        [DebuggerStepThrough]
        get => GetAndCachedAttributeValue(XMakeAttributes.exclude, ref _exclude) ?? string.Empty;

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || Remove.Length == 0,
                "OM_EitherAttributeButNotBoth",
                ElementName,
                XMakeAttributes.exclude,
                XMakeAttributes.remove);

            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || Update.Length == 0,
                "OM_EitherAttributeButNotBoth",
                ElementName,
                XMakeAttributes.exclude,
                XMakeAttributes.update);

            SetOrRemoveAttribute(XMakeAttributes.exclude, value, ref _exclude, "Set item Exclude {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the Remove value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string Remove
    {
        // No thread-safety lock required here because many reader threads would set the same value to the field.
        [DebuggerStepThrough]
        get => GetAndCachedAttributeValue(XMakeAttributes.remove, ref _remove) ?? string.Empty;

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || (Include.Length == 0 && Update.Length == 0),
                "OM_OneOfAttributeButNotMore",
                ElementName,
                XMakeAttributes.include,
                XMakeAttributes.remove,
                XMakeAttributes.update);

            SetOrRemoveAttribute(XMakeAttributes.remove, value, ref _remove, "Set item Remove {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the Update value.
    /// </summary>
    public string Update
    {
        // No thread-safety lock required here because many reader threads would set the same value to the field.
        [DebuggerStepThrough]
        get => GetAndCachedAttributeValue(XMakeAttributes.update, ref _update) ?? string.Empty;

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || (Remove.Length == 0 && Include.Length == 0),
                "OM_OneOfAttributeButNotMore",
                ElementName,
                XMakeAttributes.include,
                XMakeAttributes.remove,
                XMakeAttributes.update);

            SetOrRemoveAttribute(XMakeAttributes.update, value, ref _update, "Set item Update {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the MatchOnMetadata value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string MatchOnMetadata
    {
        [DebuggerStepThrough]
        get => GetAndCachedAttributeValue(XMakeAttributes.matchOnMetadata, ref _matchOnMetadata) ?? string.Empty;

        set
        {
            // MatchOnMetadata must be inside of a target
            ErrorUtilities.VerifyThrowInvalidOperation(
                Parent == null || Parent.Parent is ProjectTargetElement or ProjectRootElement,
                "OM_NoMatchOnMetadataOutsideTargets");

            // MatchOnMetadata must be inside of a remove item
            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || RemoveMetadata.Length != 0,
                "OM_MatchOnMetadataOnlyApplicableToRemoveItems",
                ElementName,
                XMakeAttributes.matchOnMetadata);

            SetOrRemoveAttribute(XMakeAttributes.matchOnMetadata, value, ref _matchOnMetadata, "Set item MatchOnMetadata {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the MatchOnMetadataOptions value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string MatchOnMetadataOptions
    {
        [DebuggerStepThrough]
        get => GetAttributeValueOrEmpty(XMakeAttributes.matchOnMetadataOptions);

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || MatchOnMetadata.Length != 0,
                "OM_MatchOnMetadataOptionsOnlyApplicableToItemsWithMatchOnMetadata",
                ElementName,
                XMakeAttributes.matchOnMetadataOptions);

            SetOrRemoveAttribute(XMakeAttributes.matchOnMetadataOptions, value, "Set item MatchOnMetadataOptions {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the KeepMetadata value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string KeepMetadata
    {
        [DebuggerStepThrough]
        get => GetAttributeValueOrEmpty(XMakeAttributes.keepMetadata);

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                Parent == null || Parent.Parent is ProjectTargetElement,
                "OM_NoKeepMetadataOutsideTargets");

            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || RemoveMetadata.Length == 0,
                "OM_EitherAttributeButNotBoth",
                ElementName,
                XMakeAttributes.removeMetadata,
                XMakeAttributes.keepMetadata);

            SetOrRemoveAttribute(XMakeAttributes.keepMetadata, value, "Set item KeepMetadata {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the RemoveMetadata value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string RemoveMetadata
    {
        [DebuggerStepThrough]
        get => GetAttributeValueOrEmpty(XMakeAttributes.removeMetadata);

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                Parent == null || Parent.Parent is ProjectTargetElement,
                "OM_NoRemoveMetadataOutsideTargets");

            ErrorUtilities.VerifyThrowInvalidOperation(
                string.IsNullOrEmpty(value) || KeepMetadata.Length == 0,
                "OM_EitherAttributeButNotBoth",
                ElementName,
                XMakeAttributes.keepMetadata,
                XMakeAttributes.removeMetadata);

            SetOrRemoveAttribute(XMakeAttributes.removeMetadata, value, "Set item RemoveMetadata {0}", value);
        }
    }

    /// <summary>
    /// Gets or sets the KeepDuplicates value.
    /// Returns empty string if it is not present.
    /// Removes the attribute if the value to set is empty or null.
    /// </summary>
    public string KeepDuplicates
    {
        [DebuggerStepThrough]
        get => GetAttributeValueOrEmpty(XMakeAttributes.keepDuplicates);

        set
        {
            ErrorUtilities.VerifyThrowInvalidOperation(
                Parent == null || Parent.Parent is ProjectTargetElement,
                "OM_NoKeepDuplicatesOutsideTargets");

            SetOrRemoveAttribute(XMakeAttributes.keepDuplicates, value, "Set item KeepDuplicates {0}", value);
        }
    }

    /// <summary>
    /// Whether there are any child metadata elements.
    /// </summary>
    public bool HasMetadata => FirstChild != null;

    /// <summary>
    /// Get any child metadata.
    /// </summary>
    public ICollection<ProjectMetadataElement> Metadata
        => GetChildrenOfType<ProjectMetadataElement>();

    /// <summary>
    /// Use this instead of <see cref="Metadata"/> to avoid boxing the struct enumerator.
    /// </summary>
    internal ProjectElementSiblingSubTypeCollection<ProjectMetadataElement> MetadataEnumerable
        => GetChildrenOfType<ProjectMetadataElement>();

    /// <summary>
    /// Location of the include attribute.
    /// </summary>
    public ElementLocation IncludeLocation
        => GetAttributeLocation(XMakeAttributes.include);

    /// <summary>
    /// Location of the exclude attribute.
    /// </summary>
    public ElementLocation ExcludeLocation
        => GetAttributeLocation(XMakeAttributes.exclude);

    /// <summary>
    /// Location of the remove attribute.
    /// </summary>
    public ElementLocation RemoveLocation
        => GetAttributeLocation(XMakeAttributes.remove);

    /// <summary>
    /// Location of the update attribute.
    /// </summary>
    public ElementLocation UpdateLocation
        => GetAttributeLocation(XMakeAttributes.update);

    /// <summary>
    /// Location of the MatchOnMetadata attribute.
    /// </summary>
    public ElementLocation MatchOnMetadataLocation
        => GetAttributeLocation(XMakeAttributes.matchOnMetadata);

    /// <summary>
    /// Location of the MatchOnMetadataOptions attribute.
    /// </summary>
    public ElementLocation MatchOnMetadataOptionsLocation
        => GetAttributeLocation(XMakeAttributes.matchOnMetadataOptions);

    /// <summary>
    /// Location of the keepMetadata attribute.
    /// </summary>
    public ElementLocation KeepMetadataLocation
        => GetAttributeLocation(XMakeAttributes.keepMetadata);

    /// <summary>
    /// Location of the removeMetadata attribute.
    /// </summary>
    public ElementLocation RemoveMetadataLocation
        => GetAttributeLocation(XMakeAttributes.removeMetadata);

    /// <summary>
    /// Location of the keepDuplicates attribute.
    /// </summary>
    public ElementLocation KeepDuplicatesLocation
        => GetAttributeLocation(XMakeAttributes.keepDuplicates);

    /// <summary>
    /// Whether the include value has wildcards,
    /// cached for performance.
    /// </summary>
    internal bool IncludeHasWildcards
        // No thread-safety lock required here because many reader threads would set the same value to the field.
        => _includeHasWildcards ??= (Include != null) && FileMatcher.HasWildcards(_include);

    /// <summary>
    /// Internal helper to get the next ProjectItemElement sibling.
    /// If there is none, returns null.
    /// </summary>
    internal ProjectItemElement? NextItem
    {
        get
        {
            ProjectItemElement? result = null;
            ProjectElement? sibling = NextSibling;

            while (sibling != null && result == null)
            {
                result = NextSibling as ProjectItemElement;
                sibling = sibling.NextSibling;
            }

            return result;
        }
    }

    /// <summary>
    /// Convenience method to add a piece of metadata to this item.
    /// Adds after any existing metadata. Does not modify any existing metadata.
    /// </summary>
    public ProjectMetadataElement AddMetadata(string name, string unevaluatedValue)
        => AddMetadata(name, unevaluatedValue, expressAsAttribute: false);

    /// <summary>
    /// Convenience method to add a piece of metadata to this item.
    /// Adds after any existing metadata. Does not modify any existing metadata.
    /// </summary>
    /// <param name="name">The name of the metadata to add</param>
    /// <param name="unevaluatedValue">The value of the metadata to add</param>
    /// <param name="expressAsAttribute">If true, then the metadata will be expressed as an attribute instead of a child element, for example
    /// &lt;Reference Include="Libary.dll" HintPath="..\lib\Library.dll" Private="True" /&gt;
    /// </param>
    public ProjectMetadataElement AddMetadata(string name, string unevaluatedValue, bool expressAsAttribute)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(unevaluatedValue);

        if (expressAsAttribute)
        {
            ProjectMetadataElement.ValidateValidMetadataAsAttributeName(name, ElementName, Location);
        }

        ProjectMetadataElement metadata = ContainingProject.CreateMetadataElement(name);
        metadata.Value = unevaluatedValue;
        metadata.ExpressedAsAttribute = expressAsAttribute;

        AppendChild(metadata);

        return metadata;
    }

    /// <inheritdoc />
    public override void CopyFrom(ProjectElement element)
    {
        base.CopyFrom(element);

        // clear cached fields
        ClearAttributeCache();
    }

    /// <summary>
    /// Creates an unparented ProjectItemElement, wrapping an unparented XmlElement.
    /// Caller should then ensure the element is added to a parent.
    /// </summary>
    internal static ProjectItemElement CreateDisconnected(string itemType, ProjectRootElement containingProject)
    {
        XmlUtilities.VerifyThrowArgumentValidElementName(itemType);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(itemType), "CannotModifyReservedItem", itemType);

        XmlElementWithLocation element = containingProject.CreateElement(itemType);

        return new(element, containingProject);
    }

    /// <summary>
    /// Changes the item type.
    /// </summary>
    /// <remarks>
    /// The implementation has to actually replace the element to do this.
    /// </remarks>
    internal void ChangeItemType(string newItemType)
    {
        ArgumentException.ThrowIfNullOrEmpty(newItemType);
        XmlUtilities.VerifyThrowArgumentValidElementName(newItemType);
        ErrorUtilities.VerifyThrowArgument(!XMakeElements.ReservedItemNames.Contains(newItemType), "CannotModifyReservedItem", newItemType);

        if (IsLink)
        {
            ItemLink.ChangeItemType(newItemType);
            return;
        }

        // Because the element was created from our special XmlDocument, we know it's
        // an XmlElementWithLocation.
        XmlElementWithLocation newElement = XmlUtilities.RenameXmlElement(XmlElement, newItemType, XmlElement.NamespaceURI);

        ReplaceElement(newElement);
    }

    private protected override void ClearAttributeCache()
    {
        base.ClearAttributeCache();

        _include = null;
        _exclude = null;
        _remove = null;
        _matchOnMetadata = null;
        _update = null;
        _includeHasWildcards = null;
    }

    private protected override bool CanAcceptParent(ProjectElementContainer newParent)
    {
        if (newParent.Parent is not (ProjectRootElement or ProjectTargetElement or ProjectWhenElement or ProjectOtherwiseElement))
        {
            return false;
        }

        ErrorUtilities.VerifyThrowInvalidOperation(
            newParent.Parent is ProjectTargetElement || Include.Length > 0 || Update.Length > 0 || Remove.Length > 0,
            "OM_ItemsOutsideTargetMustHaveIncludeOrUpdateOrRemove");

        return true;
    }

    /// <summary>
    /// Overridden to update the parent's children-have-no-wildcards flag.
    /// </summary>
    private protected override void OnAfterParentChanged(ProjectElementContainer? parent)
    {
        base.OnAfterParentChanged(parent);

        if (parent != null)
        {
            // This is our indication that we just got attached to a parent
            // Update its children-with-wildcards flag
            if (parent is ProjectItemGroupElement groupParent && groupParent.DefinitelyAreNoChildrenWithWildcards && IncludeHasWildcards)
            {
                groupParent.DefinitelyAreNoChildrenWithWildcards = false;
            }
        }
    }

    protected override ProjectElement CreateNewInstance(ProjectRootElement owner)
        => owner.CreateItemElement(ItemType, Include);

    /// <summary>
    /// Do not clone attributes which can be metadata. The corresponding expressed as attribute project elements are responsible for adding their attribute
    /// </summary>
    protected override bool ShouldCloneXmlAttribute(XmlAttribute attribute)
        => !ProjectMetadataElement.AttributeNameIsValidMetadataName(attribute.LocalName);

    internal override bool ShouldCloneXmlAttribute(XmlAttributeLink attributeLink)
        => !ProjectMetadataElement.AttributeNameIsValidMetadataName(attributeLink.LocalName);
}
