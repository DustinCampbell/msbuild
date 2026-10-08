// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Applies one recorded operation with its captured item states and XML context.
    /// </summary>
    private abstract class LazyItemOperation : IItemProvider<I>
    {
        /// <summary>
        ///  The privately owned reference map, never changed after operation construction.
        /// </summary>
        private readonly Dictionary<string, ItemListSnapshot> _referencedItemLists;

        /// <summary>
        ///  The evaluator owning filesystem, logging, profiling, and outer property services.
        /// </summary>
        protected readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator;

        /// <summary>
        ///  The XML from which the operation was recorded.
        /// </summary>
        protected readonly ProjectItemElement _itemElement;

        /// <summary>
        ///  The property-expanded specification, bound to this operation's stable expander.
        /// </summary>
        protected readonly ItemSpec<P, I> _itemSpec;

        /// <summary>
        ///  The expander whose item provider observes only this operation's captured prefixes.
        /// </summary>
        protected readonly Expander<P, I> _expander;

        /// <summary>
        ///  The already evaluated combined group and item condition.
        /// </summary>
        protected readonly bool _conditionResult;

        /// <summary>
        ///  The wrapper that restores this operation's factory context before each factory call.
        /// </summary>
        protected readonly IItemFactory<I, I> _itemFactory;

        /// <summary>
        ///  Binds a parsed operation to its captured references and model-specific factory.
        /// </summary>
        /// <param name="itemElement">The operation XML.</param>
        /// <param name="itemSpec">The property-expanded specification.</param>
        /// <param name="references">The earlier item states captured during construction.</param>
        /// <param name="conditionResult">The combined group and item condition.</param>
        /// <param name="lazyEvaluator">The owning evaluator.</param>
        protected LazyItemOperation(
            ProjectItemElement itemElement,
            ItemSpec<P, I> itemSpec,
            Dictionary<string, ItemListSnapshot> references,
            bool conditionResult,
            LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            _itemElement = itemElement;
            _itemSpec = itemSpec;
            _referencedItemLists = references;
            _conditionResult = conditionResult;
            _lazyEvaluator = lazyEvaluator;
            _itemFactory = new ItemFactoryWrapper(itemElement, lazyEvaluator._itemFactory);
            _expander = new Expander<P, I>(
                lazyEvaluator._outerEvaluatorData, this, lazyEvaluator.EvaluationContext, lazyEvaluator._loggingContext);
            _itemSpec.Expander = _expander;
        }

        /// <summary>
        ///  Gets the evaluation's file matcher and entry caches.
        /// </summary>
        protected FileMatcher FileMatcher => _lazyEvaluator.FileMatcher;

        /// <summary>
        ///  Supplies only the earlier item states captured during operation construction.
        /// </summary>
        /// <param name="itemType">The requested item type.</param>
        /// <returns>
        ///  Captured condition-visible items, or an empty collection for a missing reference.
        /// </returns>
        public ICollection<I> GetItems(string itemType)
            => _referencedItemLists is not null && _referencedItemLists.TryGetValue(itemType, out ItemListSnapshot list)
                ? list.GetMatchedItems()
                : Array.Empty<I>();

        /// <summary>
        ///  Profiles and applies one operation to a working ordered item state.
        /// </summary>
        /// <param name="items">The working state.</param>
        /// <param name="exclusions">Later removals applicable to earlier glob expansion.</param>
        public void Apply(OrderedItemDataCollection.Builder items, GlobExclusions exclusions)
        {
            MSBuildEventSource.Log.ApplyLazyItemOperationsStart(_itemElement.ItemType);
            using (_lazyEvaluator._evaluationProfiler.TrackElement(_itemElement))
            {
                ApplyImpl(items, exclusions);
            }
            MSBuildEventSource.Log.ApplyLazyItemOperationsStop(_itemElement.ItemType);
        }

        /// <summary>
        ///  Applies the concrete Include, Update, or Remove algorithm.
        /// </summary>
        /// <param name="listBuilder">The working item state.</param>
        /// <param name="globsToIgnore">The applicable later glob removals.</param>
        protected abstract void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, GlobExclusions globsToIgnore);

        /// <summary>
        ///  Associates an operation item with optional matching sources for qualified metadata.
        /// </summary>
        protected readonly struct ItemBatchingContext
        {
            /// <summary>
            ///  Gets the item being decorated.
            /// </summary>
            public I OperationItem { get; }

            /// <summary>
            ///  The matching source table, or null when only the operation item is needed.
            /// </summary>
            private Dictionary<string, I> CapturedItems { get; }

            /// <summary>
            ///  Initializes a decoration context.
            /// </summary>
            /// <param name="operationItem">The item receiving metadata.</param>
            /// <param name="capturedItems">The optional matching source items.</param>
            public ItemBatchingContext(I operationItem, Dictionary<string, I> capturedItems = null)
            {
                OperationItem = operationItem;
                CapturedItems = capturedItems is null || capturedItems.Count == 0 ? null : capturedItems;
            }

            /// <summary>
            ///  Resolves unqualified and qualified metadata for this item.
            /// </summary>
            /// <returns>
            ///  The operation item or a table also containing captured source items.
            /// </returns>
            public IMetadataTable GetMetadataTable()
                => CapturedItems is null ? OperationItem : new ItemOperationMetadataTable(OperationItem, CapturedItems);
        }

        /// <summary>
        ///  Routes metadata reads to the operation item or a captured matching source.
        /// </summary>
        private sealed class ItemOperationMetadataTable : IMetadataTable
        {
            /// <summary>
            ///  The operation item used for unqualified and self-qualified metadata.
            /// </summary>
            private readonly I _operationItem;

            /// <summary>
            ///  The read-only captured source table keyed by item type.
            /// </summary>
            private readonly Dictionary<string, I> _capturedItems;

            /// <summary>
            ///  Initializes qualified metadata routing.
            /// </summary>
            /// <param name="operationItem">The operation item.</param>
            /// <param name="capturedItems">The matching source table.</param>
            public ItemOperationMetadataTable(I operationItem, Dictionary<string, I> capturedItems)
            {
                Assumed.Equal(
                    capturedItems.Comparer, StringComparer.OrdinalIgnoreCase, "MSBuild assumes case insensitive item name comparison");
                _operationItem = operationItem;
                _capturedItems = capturedItems;
            }

            /// <summary>
            ///  Reads unqualified metadata from the operation item.
            /// </summary>
            /// <param name="name">The metadata name.</param>
            /// <returns>
            ///  The escaped metadata value.
            /// </returns>
            public string GetEscapedValue(string name) => _operationItem.GetEscapedValue(name);

            /// <summary>
            ///  Reads qualified metadata from the matching item table.
            /// </summary>
            /// <param name="itemType">The qualifier.</param>
            /// <param name="name">The metadata name.</param>
            /// <returns>
            ///  The escaped value, or an empty string for an uncaptured qualifier.
            /// </returns>
            public string GetEscapedValue(string itemType, string name)
            {
                IMetadataTable table = GetTable(itemType);
                return table is null ? string.Empty : table.GetEscapedValue(itemType, name);
            }

            /// <summary>
            ///  Reads qualified metadata without converting a table's missing-value null to empty.
            /// </summary>
            /// <param name="itemType">The qualifier.</param>
            /// <param name="name">The metadata name.</param>
            /// <returns>
            ///  The table's escaped value or null; uncaptured qualifiers retain their empty result.
            /// </returns>
            public string GetEscapedValueIfPresent(string itemType, string name)
            {
                IMetadataTable table = GetTable(itemType);
                return table is null ? string.Empty : table.GetEscapedValueIfPresent(itemType, name);
            }

            /// <summary>
            ///  Resolves the item supplying qualified metadata.
            /// </summary>
            /// <param name="itemType">The qualifier, or null for the operation item.</param>
            /// <returns>
            ///  The selected table, or null for an uncaptured item type.
            /// </returns>
            private IMetadataTable GetTable(string itemType)
                => itemType is null || itemType.Equals(_operationItem.Key, StringComparison.OrdinalIgnoreCase)
                    ? _operationItem
                    : _capturedItems.TryGetValue(itemType, out I item) ? item : null;
        }

        /// <summary>
        ///  Evaluates metadata in declaration order, either per item or once for a shared decoration.
        /// </summary>
        /// <param name="contexts">The operation items and their optional matching sources.</param>
        /// <param name="metadata">The metadata XML in declaration order.</param>
        /// <param name="needToExpandMetadata">An optional previously prepared per-item requirement.</param>
        /// <remarks>
        ///  Construction discovers references but does not replace the metadata XML's values.
        ///  Full expansion therefore remains necessary here. Constant metadata is evaluated even
        ///  for an empty selection to preserve diagnostics; per-item metadata has no empty batch.
        /// </remarks>
        protected void DecorateItemsWithMetadata(
            IEnumerable<ItemBatchingContext> contexts, ImmutableArray<ProjectMetadataElement> metadata, bool? needToExpandMetadata = null)
        {
            if (metadata.IsEmpty)
            {
                return;
            }
            const ExpanderOptions options = ExpanderOptions.ExpandAll;
            needToExpandMetadata ??= NeedToExpandMetadataForEachItem(metadata, out _);
            if (needToExpandMetadata.Value)
            {
                foreach (ItemBatchingContext context in contexts)
                {
                    _expander.Metadata = context.GetMetadataTable();
                    foreach (ProjectMetadataElement element in metadata)
                    {
                        if (_lazyEvaluator.EvaluateCondition(element.Condition, element, options, ParserOptions.AllowAll, _expander))
                        {
                            string value = _expander.ExpandIntoStringLeaveEscaped(element.Value, options, element.Location);
                            context.OperationItem.SetMetadata(
                                element, FileUtilities.MaybeAdjustFilePath(value, element.ContainingProject.DirectoryPath));
                        }
                    }
                }
            }
            else
            {
                var table = new EvaluatorMetadataTable(_itemElement.ItemType, capacity: metadata.Length);
                _expander.Metadata = table;
                var values = new List<KeyValuePair<ProjectMetadataElement, string>>(metadata.Length);
                foreach (ProjectMetadataElement element in metadata)
                {
                    if (_lazyEvaluator.EvaluateCondition(element.Condition, element, options, ParserOptions.AllowAll, _expander))
                    {
                        string value = _expander.ExpandIntoStringLeaveEscaped(element.Value, options, element.Location);
                        value = FileUtilities.MaybeAdjustFilePath(value, element.ContainingProject.DirectoryPath);
                        table.SetValue(element, value);
                        values.Add(new(element, value));
                    }
                }
                // Keep bulk factory decoration so model-specific metadata sharing/predecessors survive.
                _itemFactory.SetMetadata(values, contexts.Select(context => context.OperationItem));
            }
            _expander.Metadata = null;
        }

        /// <summary>
        ///  Discovers metadata references that require separate expansion for each item.
        /// </summary>
        /// <param name="metadata">The metadata XML.</param>
        /// <param name="references">The discovered item and metadata references.</param>
        /// <returns>
        ///  Whether any metadata value or condition references metadata.
        /// </returns>
        protected bool NeedToExpandMetadataForEachItem(ImmutableArray<ProjectMetadataElement> metadata, out ItemsAndMetadataPair references)
        {
            references = new(null, null);
            foreach (ProjectMetadataElement element in metadata)
            {
                string expression = element.Value;
                ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref references, ShredderOptions.All);
                expression = element.Condition;
                ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref references, ShredderOptions.All);
            }
            return references.Metadata?.Count > 0;
        }

        /// <summary>
        ///  Recognizes only a bare item reference, not a transform or item-function expression.
        /// </summary>
        /// <param name="itemSpec">The operation specification.</param>
        /// <param name="referencedItemType">The expected referenced type.</param>
        /// <returns>
        ///  Whether the specification is a single bare reference to that type.
        /// </returns>
        protected static bool ItemspecContainsASingleBareItemReference(ItemSpec<P, I> itemSpec, string referencedItemType)
            => itemSpec.Fragments.Count == 1
                && itemSpec.Fragments[0] is ItemSpec<P, I>.ItemExpressionFragment fragment
                && fragment.Capture.ItemType.Equals(referencedItemType, StringComparison.OrdinalIgnoreCase)
                && fragment.Capture.Captures is null;
    }
}
