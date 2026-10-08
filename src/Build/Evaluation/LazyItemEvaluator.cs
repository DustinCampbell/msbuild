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
#if DEBUG
using System.Diagnostics;
#endif
using System.Linq;
using System.Threading;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
        where P : class, IProperty, IEquatable<P>, IValued
        where I : class, IItem<M>, IMetadataTable
        where M : class, IMetadatum
        where D : class, IItemDefinition<M>
    {
        private readonly IEvaluatorData<P, I, M, D> _outerEvaluatorData;
        private readonly Expander<P, I> _outerExpander;
        private readonly IEvaluatorData<P, I, M, D> _evaluatorData;
        private readonly Expander<P, I> _expander;
        private readonly IItemFactory<I, I> _itemFactory;
        private readonly LoggingContext _loggingContext;
        private readonly EvaluationProfiler _evaluationProfiler;

        private int _nextElementOrder = 0;

        private Dictionary<string, LazyItemList> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            new Dictionary<string, LazyItemList>() :
            new Dictionary<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

        protected EvaluationContext EvaluationContext { get; }

        protected IFileSystem FileSystem => EvaluationContext.FileSystem;

        protected FileMatcher FileMatcher => EvaluationContext.FileMatcher;

        public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I, I> itemFactory, LoggingContext loggingContext, EvaluationProfiler evaluationProfiler, EvaluationContext evaluationContext)
        {
            _outerEvaluatorData = data;
            _outerExpander = new Expander<P, I>(_outerEvaluatorData, _outerEvaluatorData, evaluationContext, loggingContext);
            _evaluatorData = new EvaluatorData(_outerEvaluatorData, _itemLists);
            _expander = new Expander<P, I>(_evaluatorData, _evaluatorData, evaluationContext, loggingContext);
            _itemFactory = itemFactory;
            _loggingContext = loggingContext;
            _evaluationProfiler = evaluationProfiler;

            EvaluationContext = evaluationContext;
        }

        public bool EvaluateConditionWithCurrentState(ProjectElement element, ExpanderOptions expanderOptions, ParserOptions parserOptions)
        {
            return EvaluateCondition(element.Condition, element, expanderOptions, parserOptions, _expander, this);
        }

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

        private class MemoizedOperation
        {
            public LazyItemOperation Operation { get; }
            private Dictionary<ISet<string>, OrderedItemDataCollection> _cache;

            private bool _isReferenced;
#if DEBUG
            private int _applyCalls;
#endif

            public MemoizedOperation(LazyItemOperation operation)
            {
                Operation = operation;
            }

            /// <summary>
            ///  Gets whether an expression or current-state read requires this operation's result.
            /// </summary>
            public bool IsReferenced => _isReferenced;

            public void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
#if DEBUG
                CheckInvariant();
#endif

                Operation.Apply(listBuilder, globsToIgnore);

                RecordResult(listBuilder, globsToIgnore);
            }

            /// <summary>
            ///  Records the result of an operation applied either directly or through a literal batch.
            /// </summary>
            /// <param name="listBuilder">The item state immediately after the operation.</param>
            /// <param name="globsToIgnore">The exclusions under which that state was computed.</param>
            public void RecordResult(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                // cache results if somebody is referencing this operation
                if (_isReferenced)
                {
                    AddItemsToCache(globsToIgnore, listBuilder.ToImmutable());
                }
#if DEBUG
                _applyCalls++;
                CheckInvariant();
#endif
            }

#if DEBUG
            private void CheckInvariant()
            {
                if (_isReferenced)
                {
                    var cacheCount = _cache?.Count ?? 0;
                    Debug.Assert(_applyCalls == cacheCount, "Apply should only be called once per globsToIgnore. Otherwise caching is not working");
                }
                else
                {
                    // non referenced operations should not be cached
                    // non referenced operations should have as many apply calls as the number of cache keys of the immediate dominator with _isReferenced == true
                    Debug.Assert(_cache == null);
                }
            }
#endif

            public bool TryGetFromCache(ISet<string> globsToIgnore, out OrderedItemDataCollection items)
            {
                if (_cache != null)
                {
                    return _cache.TryGetValue(globsToIgnore, out items);
                }

                items = null;
                return false;
            }

            /// <summary>
            /// Somebody is referencing this operation
            /// </summary>
            public void MarkAsReferenced()
            {
                _isReferenced = true;
            }

            private void AddItemsToCache(ImmutableHashSet<string> globsToIgnore, OrderedItemDataCollection items)
            {
                if (_cache == null)
                {
                    _cache = new Dictionary<ISet<string>, OrderedItemDataCollection>();
                }

                _cache[globsToIgnore] = items;
            }
        }

        private class LazyItemList
        {
            private readonly LazyItemList _previous;
            private readonly MemoizedOperation _memoizedOperation;

            public LazyItemList(LazyItemList previous, LazyItemOperation operation)
            {
                _previous = previous;
                _memoizedOperation = new MemoizedOperation(operation);
            }

            public ImmutableList<I> GetMatchedItems(ImmutableHashSet<string> globsToIgnore)
            {
                ImmutableList<I>.Builder items = ImmutableList.CreateBuilder<I>();
                foreach (ItemData data in GetItemData(globsToIgnore))
                {
                    if (data.ConditionResult)
                    {
                        items.Add(data.Item);
                    }
                }

                return items.ToImmutable();
            }

            public OrderedItemDataCollection.Builder GetItemData(ImmutableHashSet<string> globsToIgnore)
            {
                // Cache results only on the LazyItemOperations whose results are required by an external caller (via GetItems). This means:
                //   - Callers of GetItems who have announced ahead of time that they would reference an operation (via MarkAsReferenced())
                // This includes: item references (Include="@(foo)") and metadata conditions (Condition="@(foo->Count()) == 0")
                // Without ahead of time notifications more computation is done than needed when the results of a future operation are requested
                // The future operation is part of another item list referencing this one (making this operation part of the tail).
                // The future operation will compute this list but since no ahead of time notifications have been made by callers, it won't cache the
                // intermediary operations that would be requested by those callers.
                //   - Callers of GetItems that cannot announce ahead of time. This includes item referencing conditions on
                // Item Groups and Item Elements. However, those conditions are performed eagerly outside of the LazyItemEvaluator, so they will run before
                // any item referencing operations from inside the LazyItemEvaluator. This
                //
                // If the head of this LazyItemList is uncached, then the tail may contain cached and un-cached nodes.
                // In this case we have to compute the head plus the part of the tail up to the first cached operation.
                //
                // The cache is based on a couple of properties:
                // - uses immutable lists for structural sharing between multiple cached nodes (multiple include operations won't have duplicated memory for the common items)
                // - if an operation is cached for a certain set of globsToIgnore, then the entire operation tail can be reused. This is because (i) the structure of LazyItemLists
                // does not mutate: one can add operations on top, but the base never changes, and (ii) the globsToIgnore passed to the tail is the concatenation between
                // the globsToIgnore received as an arg, and the globsToIgnore produced by the head (if the head is a Remove operation)

                OrderedItemDataCollection items;
                if (_memoizedOperation.TryGetFromCache(globsToIgnore, out items))
                {
                    return items.ToBuilder();
                }
                else
                {
                    // tell the cache that this operation's result is needed by an external caller
                    // this is required for callers that cannot tell the item list ahead of time that
                    // they would be using an operation
                    MarkAsReferenced();

                    return ComputeItems(this, globsToIgnore);
                }
            }

            /// <summary>
            /// Applies uncached item operations (include, remove, update) in order. Since Remove effectively overwrites Include or Update,
            /// Remove operations are preprocessed (adding to globsToIgnore) to create a longer list of globs we don't need to process
            /// properly because we know they will be removed. Update operations are batched as much as possible, meaning rather
            /// than being applied immediately, they are combined into a dictionary of UpdateOperations that need to be applied. This
            /// is to optimize the case in which as series of UpdateOperations, each of which affects a single ItemSpec, are applied to all
            /// items in the list, leading to a quadratic-time operation.
            /// </summary>
            private static OrderedItemDataCollection.Builder ComputeItems(LazyItemList lazyItemList, ImmutableHashSet<string> globsToIgnore)
            {
                // Stack of operations up to the first one that's cached (exclusive)
                Stack<LazyItemList> itemListStack = new Stack<LazyItemList>();

                OrderedItemDataCollection.Builder items = null;

                // Keep a separate stack of lists of globs to ignore that only gets modified for Remove operations
                Stack<ImmutableHashSet<string>> globsToIgnoreStack = null;

                for (var currentList = lazyItemList; currentList != null; currentList = currentList._previous)
                {
                    var globsToIgnoreFromFutureOperations = globsToIgnoreStack?.Peek() ?? globsToIgnore;

                    OrderedItemDataCollection itemsFromCache;
                    if (currentList._memoizedOperation.TryGetFromCache(globsToIgnoreFromFutureOperations, out itemsFromCache))
                    {
                        // the base items on top of which to apply the uncached operations are the items of the first operation that is cached
                        items = itemsFromCache.ToBuilder();
                        break;
                    }

                    // If this is a remove operation, then add any globs that will be removed
                    //  to a list of globs to ignore in previous operations
                    if (currentList._memoizedOperation.Operation is RemoveOperation removeOperation)
                    {
                        globsToIgnoreStack ??= new Stack<ImmutableHashSet<string>>();

                        var globsToIgnoreForPreviousOperations = removeOperation.GetRemovedGlobs();
                        foreach (var globToRemove in globsToIgnoreFromFutureOperations)
                        {
                            globsToIgnoreForPreviousOperations.Add(globToRemove);
                        }

                        globsToIgnoreStack.Push(globsToIgnoreForPreviousOperations.ToImmutable());
                    }

                    itemListStack.Push(currentList);
                }

                if (items == null)
                {
                    items = OrderedItemDataCollection.CreateBuilder();
                }

                ImmutableHashSet<string> currentGlobsToIgnore = globsToIgnoreStack == null ? globsToIgnore : globsToIgnoreStack.Peek();

                Dictionary<string, UpdateOperation> itemsWithNoWildcards = new Dictionary<string, UpdateOperation>(StringComparer.OrdinalIgnoreCase);
                bool addedToBatch = false;

                // Walk back down the stack of item lists applying operations
                while (itemListStack.Count > 0)
                {
                    var currentList = itemListStack.Pop();

                    if (currentList._memoizedOperation.Operation is UpdateOperation op)
                    {
                        bool addToBatch = true;
                        int i;
                        // The TextFragments are things like abc.def or x*y.*z.
                        for (i = 0; i < op.Spec.Fragments.Count; i++)
                        {
                            ItemSpecFragment frag = op.Spec.Fragments[i];
                            if (MSBuildConstants.CharactersForExpansion.Any(frag.TextFragment.Contains))
                            {
                                // Fragment contains wild cards, items, or properties. Cannot batch over it using a dictionary.
                                addToBatch = false;
                                break;
                            }

                            string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(frag.TextFragment, frag.ProjectDirectory);
                            if (itemsWithNoWildcards.ContainsKey(fullPath))
                            {
                                // Another update will already happen on this path. Make that happen before evaluating this one.
                                addToBatch = false;
                                break;
                            }
                            else
                            {
                                itemsWithNoWildcards.Add(fullPath, op);
                            }
                        }
                        if (!addToBatch)
                        {
                            // We found a wildcard. Remove any fragments associated with the current operation and process them later.
                            for (int j = 0; j < i; j++)
                            {
                                ItemSpecFragment fragment = op.Spec.Fragments[j];
                                string key = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                                itemsWithNoWildcards.Remove(key);
                            }
                        }
                        else
                        {
                            addedToBatch = true;
                            if (currentList._memoizedOperation.IsReferenced)
                            {
                                // Publish demanded prefixes before later updates can join the batch.
                                ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);
                                currentList._memoizedOperation.RecordResult(items, currentGlobsToIgnore);
                                addedToBatch = false;
                            }
                            continue;
                        }
                    }

                    if (addedToBatch)
                    {
                        addedToBatch = false;
                        ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);
                    }

                    // If this is a remove operation, then it could modify the globs to ignore, so pop the potentially
                    //  modified entry off the stack of globs to ignore
                    if (currentList._memoizedOperation.Operation is RemoveOperation)
                    {
                        globsToIgnoreStack.Pop();
                        currentGlobsToIgnore = globsToIgnoreStack.Count == 0 ? globsToIgnore : globsToIgnoreStack.Peek();
                    }

                    currentList._memoizedOperation.Apply(items, currentGlobsToIgnore);
                }

                // We finished looping through the operations. Now process the final batch if necessary.
                ProcessNonWildCardItemUpdates(itemsWithNoWildcards, items);

                return items;
            }

            private static void ProcessNonWildCardItemUpdates(Dictionary<string, UpdateOperation> itemsWithNoWildcards, OrderedItemDataCollection.Builder items)
            {
                if (itemsWithNoWildcards.Count > 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        string fullPath = FileUtilities.NormalizePathForComparisonNoThrow(items[i].Item.EvaluatedInclude, items[i].Item.ProjectDirectory);
                        if (itemsWithNoWildcards.TryGetValue(fullPath, out UpdateOperation op))
                        {
                            items[i] = op.UpdateItem(items[i]);
                        }
                    }
                    itemsWithNoWildcards.Clear();
                }
            }

            public void MarkAsReferenced()
            {
                _memoizedOperation.MarkAsReferenced();
            }
        }

        /// <summary>
        ///  Captures an existing item history before the operation that references it is appended.
        /// </summary>
        /// <param name="itemType">The referenced item type.</param>
        /// <param name="referencedItemLists">The operation's reference map being constructed.</param>
        private void AddReferencedItemList(string itemType, IDictionary<string, LazyItemList> referencedItemLists)
        {
            if (_itemLists.TryGetValue(itemType, out LazyItemList itemList))
            {
                itemList.MarkAsReferenced();
                referencedItemLists[itemType] = itemList;
            }
        }

        public IEnumerable<ItemData> GetAllItemsDeferred()
        {
            return _itemLists.Values.SelectMany(itemList => itemList.GetItemData(ImmutableHashSet<string>.Empty))
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

            _itemLists.TryGetValue(itemElement.ItemType, out LazyItemList previousItemList);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;
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
            var references = CreateReferenceBuilder();
            ItemSpec<P, I> spec = CreateItemSpec(rootDirectory, itemElement.Update, itemElement.UpdateLocation, references);
            ImmutableArray<ProjectMetadataElement> metadata = ProcessMetadataElements(itemElement, references);
            return new UpdateOperation(itemElement, spec, references.ToImmutable(), conditionResult, this, metadata);
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
            var references = CreateReferenceBuilder();
            ItemSpec<P, I> spec = CreateItemSpec(rootDirectory, itemElement.Include, itemElement.IncludeLocation, references);
            var excludes = ImmutableArray.CreateBuilder<string>();
            if (itemElement.Exclude.Length > 0)
            {
                // A property can introduce an item reference that must capture this earlier state.
                string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(itemElement.Exclude, ExpanderOptions.ExpandProperties, itemElement.ExcludeLocation);
                if (evaluatedExclude.Length > 0)
                {
                    foreach (string exclude in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude))
                    {
                        excludes.Add(exclude);
                        AddItemReferences(exclude, references, itemElement.ExcludeLocation);
                    }
                }
            }

            ImmutableArray<ProjectMetadataElement> metadata = ProcessMetadataElements(itemElement, references);
            return new IncludeOperation(
                itemElement, spec, references.ToImmutable(), conditionResult, this, elementOrder, rootDirectory, excludes.ToImmutable(), metadata);
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
            var references = CreateReferenceBuilder();
            ItemSpec<P, I> spec = CreateItemSpec(rootDirectory, itemElement.Remove, itemElement.RemoveLocation, references);
            var metadataNames = ImmutableArray.CreateBuilder<string>();
            if (itemElement.MatchOnMetadata.Length > 0)
            {
                string evaluatedNames = _expander.ExpandIntoStringLeaveEscaped(
                    itemElement.MatchOnMetadata, ExpanderOptions.ExpandProperties, itemElement.MatchOnMetadataLocation);
                if (evaluatedNames.Length > 0)
                {
                    foreach (string name in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedNames))
                    {
                        AddItemReferences(name, references, itemElement.MatchOnMetadataLocation);
                        string expanded = _expander.ExpandIntoStringLeaveEscaped(
                            name, ExpanderOptions.ExpandPropertiesAndItems, itemElement.MatchOnMetadataLocation);
                        metadataNames.AddRange(ExpressionShredder.SplitSemiColonSeparatedList(expanded));
                    }
                }
            }

            MatchOnMetadataOptions options = Enum.TryParse(itemElement.MatchOnMetadataOptions, out MatchOnMetadataOptions parsed)
                ? parsed
                : MatchOnMetadataOptions.CaseSensitive;
            return new RemoveOperation(itemElement, spec, references.ToImmutable(), conditionResult, this, metadataNames.ToImmutable(), options);
        }

        /// <summary>
        ///  Creates an operation's reference map using the existing item-name comparison policy.
        /// </summary>
        /// <returns>
        ///  A mutable map used only while constructing that operation.
        /// </returns>
        private static ImmutableDictionary<string, LazyItemList>.Builder CreateReferenceBuilder()
            => ImmutableDictionary.CreateBuilder<string, LazyItemList>(
                Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);

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
            string rootDirectory, string itemSpec, IElementLocation itemSpecLocation, IDictionary<string, LazyItemList> references)
        {
            var spec = new ItemSpec<P, I>(itemSpec, _outerExpander, itemSpecLocation, rootDirectory);
            foreach (ItemSpecFragment fragment in spec.Fragments)
            {
                if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemExpression)
                {
                    AddReferencedItemLists(references, itemExpression.Capture);
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
            ProjectItemElement itemElement, IDictionary<string, LazyItemList> references)
        {
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
                        AddReferencedItemList(itemType, references);
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
        private void AddItemReferences(string expression, IDictionary<string, LazyItemList> references, IElementLocation elementLocation)
        {
            if (Expander<P, I>.TryExpandSingleItemVectorExpression(
                    expression,
                    ExpanderOptions.ExpandItems,
                    elementLocation,
                    out ExpressionShredder.ItemExpressionCapture itemVector))
            {
                AddReferencedItemLists(references, itemVector);
            }
        }

        /// <summary>
        ///  Captures item types in an expression and its nested function arguments.
        /// </summary>
        /// <param name="references">The operation's reference map being constructed.</param>
        /// <param name="match">The parsed item expression.</param>
        private void AddReferencedItemLists(IDictionary<string, LazyItemList> references, ExpressionShredder.ItemExpressionCapture match)
        {
            if (match.ItemType != null)
            {
                AddReferencedItemList(match.ItemType, references);
            }
            if (match.Captures != null)
            {
                foreach (var subMatch in match.Captures)
                {
                    AddReferencedItemLists(references, subMatch);
                }
            }
        }
    }
}
