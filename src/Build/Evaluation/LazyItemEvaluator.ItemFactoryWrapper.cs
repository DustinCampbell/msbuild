// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Restores an operation's factory XML context immediately before each delegated factory call.
    /// </summary>
    /// <remarks>
    ///  Item expression expansion can recursively materialize another history and rebind the shared
    ///  factory. Binding only at operation entry would assign the wrong origin or item type after
    ///  that recursion. Cloning also temporarily binds the original Include rather than an Update.
    /// </remarks>
    private sealed class ItemFactoryWrapper : IItemFactory<I, I>
    {
        /// <summary>
        ///  The operation or original Include XML to bind for the next factory call.
        /// </summary>
        private ProjectItemElement _itemElement;

        /// <summary>
        ///  The shared model-specific factory.
        /// </summary>
        private readonly IItemFactory<I, I> _wrappedItemFactory;

        /// <summary>
        ///  Initializes a rebinding wrapper for one operation.
        /// </summary>
        /// <param name="itemElement">The operation XML.</param>
        /// <param name="wrappedItemFactory">The shared real item factory.</param>
        public ItemFactoryWrapper(ProjectItemElement itemElement, IItemFactory<I, I> wrappedItemFactory)
        {
            _itemElement = itemElement;
            _wrappedItemFactory = wrappedItemFactory;
        }

        /// <summary>
        ///  Restores the XML expected by the next delegated call.
        /// </summary>
        private void SetItemElement() => _wrappedItemFactory.ItemElement = _itemElement;

        /// <summary>
        ///  Sets the wrapper's current XML, including temporary original-Include binding for clones.
        /// </summary>
        public ProjectItemElement ItemElement
        {
            set
            {
                _itemElement = value;
                SetItemElement();
            }
        }

        /// <summary>
        ///  Gets the bound item type; changing it directly is not supported.
        /// </summary>
        public string ItemType
        {
            get
            {
                SetItemElement();
                return _wrappedItemFactory.ItemType;
            }
            set => throw new NotSupportedException();
        }

        /// <summary>
        ///  Clones <paramref name="source"/> under the restored XML context.
        /// </summary>
        /// <param name="source">The source item.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The model-specific clone.
        /// </returns>
        public I CreateItem(I source, string definingProject)
        {
            SetItemElement();
            return _wrappedItemFactory.CreateItem(source, definingProject);
        }

        /// <summary>
        ///  Creates a literal item under the restored XML context.
        /// </summary>
        /// <param name="include">The escaped include.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The model-specific item.
        /// </returns>
        public I CreateItem(string include, string definingProject)
        {
            SetItemElement();
            return _wrappedItemFactory.CreateItem(include, definingProject);
        }

        /// <summary>
        ///  Creates a literal or glob-expanded item while retaining its unexpanded Include.
        /// </summary>
        /// <param name="include">The escaped evaluated include.</param>
        /// <param name="includeBeforeWildcardExpansion">The escaped original include.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The model-specific item with wildcard provenance.
        /// </returns>
        public I CreateItem(string include, string includeBeforeWildcardExpansion, string definingProject)
        {
            SetItemElement();
            return _wrappedItemFactory.CreateItem(include, includeBeforeWildcardExpansion, definingProject);
        }

        /// <summary>
        ///  Creates a transformed item while copying metadata from <paramref name="baseItem"/>.
        /// </summary>
        /// <param name="include">The escaped transformed include.</param>
        /// <param name="baseItem">The metadata source item.</param>
        /// <param name="definingProject">The defining project path.</param>
        /// <returns>
        ///  The model-specific transformed item.
        /// </returns>
        public I CreateItem(string include, I baseItem, string definingProject)
        {
            SetItemElement();
            return _wrappedItemFactory.CreateItem(include, baseItem, definingProject);
        }

        /// <summary>
        ///  Preserves the real factory's bulk metadata sharing and predecessor behavior.
        /// </summary>
        /// <param name="metadata">The evaluated metadata in declaration order.</param>
        /// <param name="destinationItems">The items receiving metadata.</param>
        public void SetMetadata(IEnumerable<KeyValuePair<ProjectMetadataElement, string>> metadata, IEnumerable<I> destinationItems)
        {
            SetItemElement();
            _wrappedItemFactory.SetMetadata(metadata, destinationItems);
        }
    }
}
