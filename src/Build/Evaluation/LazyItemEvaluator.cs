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
using System.Threading;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    ///  Records item operations and materializes current or captured earlier states on demand.
    /// </summary>
    /// <typeparam name="P">The property model supplied by the outer evaluator.</typeparam>
    /// <typeparam name="I">The item model created and cloned by the supplied factory.</typeparam>
    /// <typeparam name="M">The model's metadata type.</typeparam>
    /// <typeparam name="D">The model's item-definition type.</typeparam>
    /// <remarks>
    ///  Conditions request the current history; recorded expressions retain an exclusive earlier
    ///  operation position. Later glob removals can prune earlier Includes only for a compatible
    ///  materialization. Saved item containers preserve order and ownership, while updates still
    ///  clone item objects before changing metadata. This evaluator is owned by one evaluation.
    /// </remarks>
    internal partial class LazyItemEvaluator<P, I, M, D> : IItemProvider<I>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        /// <summary>
        ///  The outer properties, model policy, and root directory, not the recorded item histories.
        /// </summary>
        private readonly IEvaluatorData<P, I, M, D> _outerEvaluatorData;

        /// <summary>
        ///  The property-expansion context used while parsing operation specifications.
        /// </summary>
        private readonly Expander<P, I> _outerExpander;

        /// <summary>
        ///  The expander whose item provider observes current recorded histories.
        /// </summary>
        private readonly Expander<P, I> _expander;

        /// <summary>
        ///  The shared model-specific factory, rebound by each operation before factory calls.
        /// </summary>
        private readonly IItemFactory<I, I> _itemFactory;

        /// <summary>
        ///  The logging context for conditions, expressions, and globs.
        /// </summary>
        private readonly LoggingContext _loggingContext;

        /// <summary>
        ///  The outer evaluation's element, condition, and glob profiler.
        /// </summary>
        private readonly EvaluationProfiler _evaluationProfiler;

        /// <summary>
        ///  The next global Include ordinal used for stable publication.
        /// </summary>
        private int _nextElementOrder;

        /// <summary>
        ///  Per-type append-only operation histories under the established item-name policy.
        /// </summary>
        private readonly Dictionary<string, ItemHistory> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            new Dictionary<string, ItemHistory>() :
            new Dictionary<string, ItemHistory>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///  Gets the evaluation's shared expression and filesystem services.
        /// </summary>
        protected EvaluationContext EvaluationContext { get; }

        /// <summary>
        ///  Gets the filesystem used by condition and glob evaluation.
        /// </summary>
        protected IFileSystem FileSystem => EvaluationContext.FileSystem;

        /// <summary>
        ///  Gets the matcher and file-entry caches associated with the evaluation context.
        /// </summary>
        protected FileMatcher FileMatcher => EvaluationContext.FileMatcher;

        /// <summary>
        ///  Initializes current-state item evaluation over the already evaluated outer properties.
        /// </summary>
        /// <param name="data">The outer evaluator's data and model policy.</param>
        /// <param name="itemFactory">The model-specific item factory.</param>
        /// <param name="loggingContext">The logging context.</param>
        /// <param name="evaluationProfiler">The evaluation profiler.</param>
        /// <param name="evaluationContext">The expression and filesystem context.</param>
        public LazyItemEvaluator(
            IEvaluatorData<P, I, M, D> data,
            IItemFactory<I, I> itemFactory,
            LoggingContext loggingContext,
            EvaluationProfiler evaluationProfiler,
            EvaluationContext evaluationContext)
        {
            _outerEvaluatorData = data;
            _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData, evaluationContext, loggingContext);
            _expander = new Expander<P, I>(_outerEvaluatorData, this, evaluationContext, loggingContext);
            _itemFactory = itemFactory;
            _loggingContext = loggingContext;
            _evaluationProfiler = evaluationProfiler;

            EvaluationContext = evaluationContext;
        }

        /// <summary>
        ///  Evaluates a condition against the current end of each recorded item history.
        /// </summary>
        /// <param name="element">The element owning the condition.</param>
        /// <param name="expanderOptions">The allowed expansion kinds.</param>
        /// <param name="parserOptions">The allowed condition syntax.</param>
        /// <returns>
        ///  The current-state condition result.
        /// </returns>
        public bool EvaluateConditionWithCurrentState(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element.Condition, element, expanderOptions, parserOptions, _expander);
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

        /// <summary>
        ///  Evaluates and profiles a condition using the supplied current or captured item context.
        /// </summary>
        /// <param name="condition">The unevaluated condition.</param>
        /// <param name="element">The XML owning the condition.</param>
        /// <param name="expanderOptions">The allowed expansion kinds.</param>
        /// <param name="parserOptions">The allowed condition syntax.</param>
        /// <param name="expander">The current-state or operation-specific expander.</param>
        /// <returns>
        ///  The evaluated condition result.
        /// </returns>
        private bool EvaluateCondition(
            string condition,
            ProjectElement element,
            ExpanderOptions expanderOptions,
            ParserOptions parserOptions,
            Expander<P, I> expander)
        {
            if (condition?.Length == 0)
            {
                return true;
            }
            MSBuildEventSource.Log.EvaluateConditionStart(condition);

            using (_evaluationProfiler.TrackCondition(element.ConditionLocation, condition))
            {
                bool result = ConditionEvaluator.EvaluateCondition(
                    condition,
                    parserOptions,
                    expander,
                    expanderOptions,
                    GetCurrentDirectoryForConditionEvaluation(element),
                    element.ConditionLocation,
                    FileSystem,
                    loggingContext: _loggingContext);
                MSBuildEventSource.Log.EvaluateConditionStop(condition, result);

                return result;
            }
        }

        /// <summary>
        ///  Preserves project-file-relative import/property conditions and root-relative item conditions.
        /// </summary>
        /// <param name="element">The XML owning the condition.</param>
        /// <returns>
        ///  The directory required by the element's established condition semantics.
        /// </returns>
        private string GetCurrentDirectoryForConditionEvaluation(ProjectElement element)
        {
            if (element is ProjectPropertyGroupElement || element is ProjectImportElement || element is ProjectImportGroupElement)
            {
                return element.ContainingProject.DirectoryPath;
            }
            else
            {
                return _outerEvaluatorData.Directory;
            }
        }

        /// <summary>
        ///  Keeps an item together with its original Include, ordinal, condition, and path memoization.
        /// </summary>
        public struct ItemData
        {
            /// <summary>
            ///  Initializes an ordered item entry.
            /// </summary>
            /// <param name="item">The evaluated item.</param>
            /// <param name="originatingItemElement">The Include that originally created it.</param>
            /// <param name="elementOrder">The Include's global ordinal.</param>
            /// <param name="conditionResult">The originating combined condition.</param>
            /// <param name="normalizedItemValue">An optional already normalized path.</param>
            public ItemData(
                I item, ProjectItemElement originatingItemElement, int elementOrder, bool conditionResult, string normalizedItemValue = null)
            {
                Item = item;
                OriginatingItemElement = originatingItemElement;
                ElementOrder = elementOrder;
                ConditionResult = conditionResult;
                _normalizedItemValue = normalizedItemValue;
            }

            /// <summary>
            ///  Clones an item before metadata mutation while retaining its Include provenance.
            /// </summary>
            /// <param name="itemFactory">The operation's rebinding factory.</param>
            /// <param name="initialItemElementForFactory">The operation XML to restore after cloning.</param>
            /// <returns>
            ///  The cloned entry with unchanged ordering and condition data.
            /// </returns>
            public readonly ItemData Clone(IItemFactory<I, I> itemFactory, ProjectItemElement initialItemElementForFactory)
            {
                // Clones belong to their original Include, not the Update currently applying metadata.
                itemFactory.ItemElement = OriginatingItemElement;
                var clonedItem = itemFactory.CreateItem(Item, OriginatingItemElement.ContainingProject.FullPath);
                itemFactory.ItemElement = initialItemElementForFactory;

                return new ItemData(clonedItem, OriginatingItemElement, ElementOrder, ConditionResult, _normalizedItemValue);
            }

            /// <summary>
            ///  Gets the evaluated item object.
            /// </summary>
            public I Item { get; }

            /// <summary>
            ///  Gets the Include XML that originally created the item.
            /// </summary>
            public ProjectItemElement OriginatingItemElement { get; }

            /// <summary>
            ///  Gets the original Include's global ordinal.
            /// </summary>
            public int ElementOrder { get; }

            /// <summary>
            ///  Gets the originating group/item condition result.
            /// </summary>
            public bool ConditionResult { get; }

            /// <summary>
            ///  The lazily computed normalized path, independent of metadata mutations.
            /// </summary>
            private string _normalizedItemValue;

            /// <summary>
            ///  Gets and memoizes the normalized value used by indexed matching.
            /// </summary>
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

        /// <summary>
        ///  Materializes current item states when enumerated and publishes them in Include order.
        /// </summary>
        /// <returns>
        ///  All surviving items, retaining false-condition entries and stable within-Include order.
        /// </returns>
        public IEnumerable<ItemData> GetAllItemsDeferred()
        {
            ItemData[] items = CollectOrderedItems();
            foreach (ItemData item in items)
            {
                yield return item;
            }
        }

        /// <summary>
        ///  Places final items directly into their known Include-element order without sorting.
        /// </summary>
        /// <returns>
        ///  The ordered publication buffer.
        /// </returns>
        private ItemData[] CollectOrderedItems()
        {
            if (_itemLists.Count == 0)
            {
                return Array.Empty<ItemData>();
            }

            var states = new OrderedItemDataCollection.Builder[_itemLists.Count];
            int[] offsets = new int[_nextElementOrder + 1];
            int stateIndex = 0;
            foreach (ItemHistory history in _itemLists.Values)
            {
                OrderedItemDataCollection.Builder state = history.GetItemData(history.Count);
                states[stateIndex++] = state;
                for (int index = 0; index < state.Count; index++)
                {
                    offsets[state[index].ElementOrder + 1]++;
                }
            }

            for (int index = 1; index < offsets.Length; index++)
            {
                offsets[index] += offsets[index - 1];
            }
            ItemData[] result = new ItemData[offsets[offsets.Length - 1]];
            foreach (OrderedItemDataCollection.Builder state in states)
            {
                for (int index = 0; index < state.Count; index++)
                {
                    ItemData item = state[index];
                    result[offsets[item.ElementOrder]++] = item;
                }
            }
            return result;
        }

        /// <summary>
        ///  Constructs and appends an operation after its condition and earlier references are known.
        /// </summary>
        /// <param name="rootDirectory">The root project directory.</param>
        /// <param name="itemElement">The Include, Update, or Remove XML.</param>
        /// <param name="conditionResult">The already evaluated combined condition.</param>
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
        ///  Evaluates an admitted group's item condition and records the operation when policy allows it.
        /// </summary>
        /// <param name="rootDirectory">The root project directory.</param>
        /// <param name="itemElement">The item operation XML.</param>
        /// <param name="groupConditionResult">The already evaluated parent group's condition.</param>
        /// <returns>
        ///  Whether both conditions were <see langword="true"/>, allowing the caller to record the evaluated XML element.
        /// </returns>
        public bool EvaluateItemElement(string rootDirectory, ProjectItemElement itemElement, bool groupConditionResult)
        {
            bool itemCondition = EvaluateConditionWithCurrentState(
                itemElement, ExpanderOptions.ExpandPropertiesAndItems, ParserOptions.AllowPropertiesAndItemLists);
            if (!itemCondition
                && !(_outerEvaluatorData.ShouldEvaluateForDesignTime && _outerEvaluatorData.CanEvaluateElementsWithFalseConditions))
            {
                return false;
            }
            bool conditionResult = groupConditionResult && itemCondition;
            ProcessItemElement(rootDirectory, itemElement, conditionResult);
            return conditionResult;
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
                string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(
                    itemElement.Exclude, ExpanderOptions.ExpandProperties, itemElement.ExcludeLocation);
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

                    ExpressionShredder.GetReferencedItemNamesAndMetadata(
                        expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);

                    expression = _expander.ExpandIntoStringLeaveEscaped(
                        metadatumElement.Condition,
                        expanderOptions,
                        metadatumElement.ConditionLocation);

                    ExpressionShredder.GetReferencedItemNamesAndMetadata(
                        expression, 0, expression.Length, ref itemsAndMetadataFound, ShredderOptions.All);
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
        private void AddItemReferences(
            string expression, ref Dictionary<string, ItemListSnapshot> references, IElementLocation elementLocation)
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
        private void AddReferencedItemLists(
            ref Dictionary<string, ItemListSnapshot> references, ExpressionShredder.ItemExpressionCapture match)
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
