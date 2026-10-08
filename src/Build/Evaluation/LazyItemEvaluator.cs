// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D> : IItemProvider<I>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        private readonly IEvaluatorData<P, I, M, D> _outerEvaluatorData;
        private readonly Expander<P, I> _outerExpander;
        private readonly Expander<P, I> _expander;
        private readonly IItemFactory<I, I> _itemFactory;
        private readonly LoggingContext _loggingContext;
        private readonly EvaluationProfiler _evaluationProfiler;

        private int _nextElementOrder = 0;

        private readonly Dictionary<string, ItemHistory> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            new Dictionary<string, ItemHistory>() :
            new Dictionary<string, ItemHistory>(StringComparer.OrdinalIgnoreCase);

        protected EvaluationContext EvaluationContext { get; }

        protected IFileSystem FileSystem => EvaluationContext.FileSystem;

        protected FileMatcher FileMatcher => EvaluationContext.FileMatcher;

        public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I, I> itemFactory, LoggingContext loggingContext, EvaluationProfiler evaluationProfiler, EvaluationContext evaluationContext)
        {
            _outerEvaluatorData = data;
            _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData, evaluationContext, loggingContext);
            _expander = new Expander<P, I>(_outerEvaluatorData, this, evaluationContext, loggingContext);
            _itemFactory = itemFactory;
            _loggingContext = loggingContext;
            _evaluationProfiler = evaluationProfiler;

            EvaluationContext = evaluationContext;
        }

        public bool EvaluateConditionWithCurrentState(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element.Condition, element, expanderOptions, parserOptions, _expander, this);
        }

        /// <summary>
        ///  Supplies condition-visible items from the current end of the requested item history.
        /// </summary>
        /// <param name="itemType">The requested item type.</param>
        /// <returns>
        ///  Matching items, excluding false-condition items, or an empty collection.
        /// </returns>
        public ICollection<I> GetItems(string itemType)
            => _itemLists.TryGetValue(itemType, out ItemHistory list)
                ? list.GetMatchedItems(list.Count)
                : Array.Empty<I>();

        private static bool EvaluateCondition(
            string condition,
            ProjectElement element,
            ExpanderOptions expanderOptions,
            ParserOptions parserOptions,
            Expander<P, I> expander,
            LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            if (condition?.Length == 0)
            {
                return true;
            }
            MSBuildEventSource.Log.EvaluateConditionStart(condition);

            using (lazyEvaluator._evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
            {
                bool result = ConditionEvaluator.EvaluateCondition(
                    condition,
                    parserOptions,
                    expander,
                    expanderOptions,
                    GetCurrentDirectoryForConditionEvaluation(element, lazyEvaluator),
                    element.ConditionLocation,
                    lazyEvaluator.FileSystem,
                    loggingContext: lazyEvaluator._loggingContext);
                MSBuildEventSource.Log.EvaluateConditionStop(condition, result);

                return result;
            }
        }

        /// <summary>
        /// COMPAT: Whidbey used the "current project file/targets" directory for evaluating Import and PropertyGroup conditions
        /// Orcas broke this by using the current root project file for all conditions
        /// For Dev10+, we'll fix this, and use the current project file/targets directory for Import, ImportGroup and PropertyGroup
        /// but the root project file for the rest. Inside of targets will use the root project file as always.
        /// </summary>
        private static string GetCurrentDirectoryForConditionEvaluation(ProjectElement element, LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            if (element is ProjectPropertyGroupElement || element is ProjectImportElement || element is ProjectImportGroupElement)
            {
                return element.ContainingProject.DirectoryPath;
            }
            else
            {
                return lazyEvaluator._outerEvaluatorData.Directory;
            }
        }

        public struct ItemData
        {
            public ItemData(I item, ProjectItemElement originatingItemElement, int elementOrder, bool conditionResult, string normalizedItemValue = null)
            {
                Item = item;
                OriginatingItemElement = originatingItemElement;
                ElementOrder = elementOrder;
                ConditionResult = conditionResult;
                _normalizedItemValue = normalizedItemValue;
            }

            public readonly ItemData Clone(IItemFactory<I, I> itemFactory, ProjectItemElement initialItemElementForFactory)
            {
                // setting the factory's item element to the original item element that produced the item
                // otherwise you get weird things like items that appear to have been produced by update elements
                itemFactory.ItemElement = OriginatingItemElement;
                var clonedItem = itemFactory.CreateItem(Item, OriginatingItemElement.ContainingProject.FullPath);
                itemFactory.ItemElement = initialItemElementForFactory;

                return new ItemData(clonedItem, OriginatingItemElement, ElementOrder, ConditionResult, _normalizedItemValue);
            }

            public I Item { get; }
            public ProjectItemElement OriginatingItemElement { get; }
            public int ElementOrder { get; }
            public bool ConditionResult { get; }

            /// <summary>
            /// Lazily created normalized item value.
            /// </summary>
            private string _normalizedItemValue;
            public string NormalizedItemValue
            {
                get
                {
                    var normalizedItemValue = Volatile.Read(ref _normalizedItemValue);
                    if (normalizedItemValue == null)
                    {
                        normalizedItemValue = FileUtilities.NormalizePathForComparisonNoThrow(Item.EvaluatedInclude, Item.ProjectDirectory);
                        Volatile.Write(ref _normalizedItemValue, normalizedItemValue);
                    }
                    return normalizedItemValue;
                }
            }
        }

        /// <summary>
        ///  Captures an existing item history before the operation that references it is appended.
        /// </summary>
        /// <param name="itemType">The referenced item type.</param>
        /// <param name="referencedItemLists">The operation's reference map being constructed.</param>
        private void AddReferencedItemList(string itemType, ref Dictionary<string, ItemListSnapshot> referencedItemLists)
        {
            if (_itemLists.TryGetValue(itemType, out ItemHistory itemList))
            {
                referencedItemLists ??= new Dictionary<string, ItemListSnapshot>(_itemLists.Comparer);
                referencedItemLists[itemType] = new ItemListSnapshot(itemList);
            }
        }

        public IEnumerable<ItemData> GetAllItemsDeferred()
        {
            return _itemLists.Values.SelectMany(itemList => itemList.GetItemData(itemList.Count))
                                    .OrderBy(itemData => itemData.ElementOrder);
        }

        public void ProcessItemElement(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            LazyItemOperation operation = null;

            if (itemElement.IncludeLocation != null)
            {
                operation = BuildIncludeOperation(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.RemoveLocation != null)
            {
                operation = BuildRemoveOperation(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.UpdateLocation != null)
            {
                operation = BuildUpdateOperation(rootDirectory, itemElement, conditionResult);
            }
            else
            {
                Assumed.Unreachable();
            }

            if (!_itemLists.TryGetValue(itemElement.ItemType, out ItemHistory history))
            {
                _itemLists.Add(itemElement.ItemType, history = new ItemHistory());
            }
            history.Add(operation);
        }

        /// <summary>
        ///  Constructs an Update after discovering references in its specification and metadata.
        /// </summary>
        /// <param name="rootDirectory">The root project directory.</param>
        /// <param name="itemElement">The Update XML.</param>
        /// <param name="conditionResult">The combined group and item condition.</param>
        /// <returns>
        ///  The constructed operation.
        /// </returns>
        private UpdateOperation BuildUpdateOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            Dictionary<string, ItemListSnapshot> references = null;
            ItemSpec<P, I> spec = CreateItemSpec(rootDirectory, itemElement.Update, itemElement.UpdateLocation, ref references);
            ImmutableArray<ProjectMetadataElement> metadata = ProcessMetadataElements(itemElement, ref references);
            return new UpdateOperation(itemElement, spec, references, conditionResult, this, metadata);
        }

        /// <summary>
        ///  Constructs an Include, preserving property expansion and reference capture order.
        /// </summary>
        /// <param name="rootDirectory">The root project directory.</param>
        /// <param name="itemElement">The Include XML.</param>
        /// <param name="conditionResult">The combined group and item condition.</param>
        /// <returns>
        ///  The constructed operation.
        /// </returns>
        private IncludeOperation BuildIncludeOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            int elementOrder = _nextElementOrder++;
            Dictionary<string, ItemListSnapshot> references = null;
            ItemSpec<P, I> spec = CreateItemSpec(rootDirectory, itemElement.Include, itemElement.IncludeLocation, ref references);
            ImmutableArray<string>.Builder excludes = null;
            if (itemElement.Exclude.Length > 0)
            {
                // A property can introduce an item reference that must capture this earlier state.
                string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(itemElement.Exclude, ExpanderOptions.ExpandProperties, itemElement.ExcludeLocation);
                if (evaluatedExclude.Length > 0)
                {
                    foreach (string exclude in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude))
                    {
                        (excludes ??= ImmutableArray.CreateBuilder<string>()).Add(exclude);
                        AddItemReferences(exclude, ref references, itemElement.ExcludeLocation);
                    }
                }
            }

            ImmutableArray<ProjectMetadataElement> metadata = ProcessMetadataElements(itemElement, ref references);
            return new IncludeOperation(
                itemElement, spec, references, conditionResult, this, elementOrder, rootDirectory,
                excludes?.ToImmutable() ?? ImmutableArray<string>.Empty, metadata);
        }

        /// <summary>
        ///  Constructs a Remove and expands its metadata-name specification at recording time.
        /// </summary>
        /// <param name="rootDirectory">The root project directory.</param>
        /// <param name="itemElement">The Remove XML.</param>
        /// <param name="conditionResult">The combined group and item condition.</param>
        /// <returns>
        ///  The constructed operation, including any eagerly built metadata match set.
        /// </returns>
        private RemoveOperation BuildRemoveOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            Dictionary<string, ItemListSnapshot> references = null;
            ItemSpec<P, I> spec = CreateItemSpec(rootDirectory, itemElement.Remove, itemElement.RemoveLocation, ref references);
            ImmutableArray<string>.Builder metadataNames = null;
            if (itemElement.MatchOnMetadata.Length > 0)
            {
                string evaluatedNames = _expander.ExpandIntoStringLeaveEscaped(
                    itemElement.MatchOnMetadata, ExpanderOptions.ExpandProperties, itemElement.MatchOnMetadataLocation);
                if (evaluatedNames.Length > 0)
                {
                    foreach (string name in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedNames))
                    {
                        AddItemReferences(name, ref references, itemElement.MatchOnMetadataLocation);
                        string expanded = _expander.ExpandIntoStringLeaveEscaped(
                            name, ExpanderOptions.ExpandPropertiesAndItems, itemElement.MatchOnMetadataLocation);
                        (metadataNames ??= ImmutableArray.CreateBuilder<string>()).AddRange(
                            ExpressionShredder.SplitSemiColonSeparatedList(expanded));
                    }
                }
            }

            MatchOnMetadataOptions options = Enum.TryParse(itemElement.MatchOnMetadataOptions, out MatchOnMetadataOptions parsed)
                ? parsed
                : MatchOnMetadataOptions.CaseSensitive;
            return new RemoveOperation(
                itemElement, spec, references, conditionResult, this,
                metadataNames?.ToImmutable() ?? ImmutableArray<string>.Empty, options);
        }

        /// <summary>
        ///  Expands properties in a specification and captures its item references.
        /// </summary>
        /// <param name="rootDirectory">The root project directory.</param>
        /// <param name="itemSpec">The unevaluated specification.</param>
        /// <param name="itemSpecLocation">The specification's XML location.</param>
        /// <param name="references">The operation's reference map being constructed.</param>
        /// <returns>
        ///  The parsed specification, before it is bound to the operation's expander.
        /// </returns>
        private ItemSpec<P, I> CreateItemSpec(
            string rootDirectory, string itemSpec, IElementLocation itemSpecLocation, ref Dictionary<string, ItemListSnapshot> references)
        {
            var spec = new ItemSpec<P, I>(itemSpec, _outerExpander, itemSpecLocation, rootDirectory);
            foreach (ItemSpecFragment fragment in spec.Fragments)
            {
                if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemExpression)
                {
                    AddReferencedItemLists(ref references, itemExpression.Capture);
                }
            }
            return spec;
        }

        /// <summary>
        ///  Collects metadata XML and discovers item references without evaluating metadata values.
        /// </summary>
        /// <param name="itemElement">The operation's XML.</param>
        /// <param name="references">The operation's reference map being constructed.</param>
        /// <returns>
        ///  The metadata elements in declaration order.
        /// </returns>
        private ImmutableArray<ProjectMetadataElement> ProcessMetadataElements(
            ProjectItemElement itemElement, ref Dictionary<string, ItemListSnapshot> references)
        {
            if (!itemElement.HasMetadata)
            {
                return ImmutableArray<ProjectMetadataElement>.Empty;
            }
            var metadata = ImmutableArray.CreateBuilder<ProjectMetadataElement>();
            if (itemElement.HasMetadata)
            {
                ItemsAndMetadataPair itemsAndMetadataFound = new ItemsAndMetadataPair(null, null);

                // Since we're just attempting to expand properties in order to find referenced items and not expanding metadata,
                // unexpected errors may occur when evaluating property functions on unexpanded metadata. Just ignore them if that happens.
                // See: https://github.com/dotnet/msbuild/issues/3460
                const ExpanderOptions expanderOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError;
                foreach (var metadatumElement in itemElement.MetadataEnumerable)
                {
                    metadata.Add(metadatumElement);

                    string expression = _expander.ExpandIntoStringLeaveEscaped(
                        metadatumElement.Value,
                        expanderOptions,
                        metadatumElement.Location);

                    ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);

                    expression = _expander.ExpandIntoStringLeaveEscaped(
                        metadatumElement.Condition,
                        expanderOptions,
                        metadatumElement.ConditionLocation);

                    ExpressionShredder.GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);
                }

                if (itemsAndMetadataFound.Items != null)
                {
                    foreach (var itemType in itemsAndMetadataFound.Items)
                    {
                        AddReferencedItemList(itemType, ref references);
                    }
                }
            }
            return metadata.ToImmutable();
        }

        /// <summary>
        ///  Captures references in a single item-vector expression.
        /// </summary>
        /// <param name="expression">The expression after property expansion.</param>
        /// <param name="references">The operation's reference map being constructed.</param>
        /// <param name="elementLocation">The expression's XML location.</param>
        private void AddItemReferences(string expression, ref Dictionary<string, ItemListSnapshot> references, IElementLocation elementLocation)
        {
            if (Expander<P, I>.TryExpandSingleItemVectorExpression(
                    expression,
                    ExpanderOptions.ExpandItems,
                    elementLocation,
                    out ExpressionShredder.ItemExpressionCapture itemVector))
            {
                AddReferencedItemLists(ref references, itemVector);
            }
        }

        /// <summary>
        ///  Captures item types in an expression and its nested function arguments.
        /// </summary>
        /// <param name="references">The operation's reference map being constructed.</param>
        /// <param name="match">The parsed item expression.</param>
        private void AddReferencedItemLists(ref Dictionary<string, ItemListSnapshot> references, ExpressionShredder.ItemExpressionCapture match)
        {
            if (match.ItemType != null)
            {
                AddReferencedItemList(match.ItemType, ref references);
            }
            if (match.Captures != null)
            {
                foreach (var subMatch in match.Captures)
                {
                    AddReferencedItemLists(ref references, subMatch);
                }
            }
        }
    }
}
