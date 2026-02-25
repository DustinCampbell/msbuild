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
using Microsoft.CodeAnalysis.Collections;
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
        private readonly IItemFactory<I> _itemFactory;
        private readonly LoggingContext _loggingContext;
        private readonly EvaluationProfiler _evaluationProfiler;

        private int _nextElementOrder = 0;

        private Dictionary<string, LazyItemList> _itemLists = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames ?
            new Dictionary<string, LazyItemList>() :
            new Dictionary<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

        protected EvaluationContext EvaluationContext { get; }

        protected IFileSystem FileSystem => EvaluationContext.FileSystem;

        protected FileMatcher FileMatcher => EvaluationContext.FileMatcher;

        public LazyItemEvaluator(IEvaluatorData<P, I, M, D> data, IItemFactory<I> itemFactory, LoggingContext loggingContext, EvaluationProfiler evaluationProfiler, EvaluationContext evaluationContext)
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
            return EvaluateCondition(element, expanderOptions, parserOptions, _expander, this);
        }

        private static bool EvaluateCondition(
            ProjectElement element,
            ExpanderOptions expanderOptions,
            ParserOptions parserOptions,
            Expander<P, I> expander,
            LazyItemEvaluator<P, I, M, D> lazyEvaluator)
        {
            string condition = element.Condition;

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

            public readonly ItemData Clone(IItemFactory<I> itemFactory, ProjectItemElement initialItemElementForFactory)
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

        private class MemoizedOperation : IItemOperation
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

            public void Apply(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
#if DEBUG
                CheckInvariant();
#endif

                Operation.Apply(listBuilder, globsToIgnore);

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
                                itemsWithNoWildcards.Remove(currentList._memoizedOperation.Operation.Spec.Fragments[j].TextFragment);
                            }
                        }
                        else
                        {
                            addedToBatch = true;
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
                operation = CreateIncludeOperation(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.RemoveLocation != null)
            {
                operation = CreateRemoveOperation(rootDirectory, itemElement, conditionResult);
            }
            else if (itemElement.UpdateLocation != null)
            {
                operation = CreateUpdateOperation(rootDirectory, itemElement, conditionResult);
            }
            else
            {
                ErrorUtilities.ThrowInternalErrorUnreachable();
            }

            _itemLists.TryGetValue(itemElement.ItemType, out LazyItemList previousItemList);
            LazyItemList newList = new LazyItemList(previousItemList, operation);
            _itemLists[itemElement.ItemType] = newList;
        }

#nullable enable

        private UpdateOperation CreateUpdateOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            var itemSpec = new ItemSpec<P, I>(itemElement.Update, _outerExpander, itemElement.UpdateLocation, rootDirectory);
            var referencedItemListsBuilder = new ReferencedItemListsBuilder(lazyEvaluator: this);
            referencedItemListsBuilder.Add(itemSpec);

            var metadata = GetMetadata(itemElement, ref referencedItemListsBuilder);

            return new UpdateOperation(
                itemElement,
                itemSpec,
                referencedItemListsBuilder.ToImmutable(),
                conditionResult,
                metadata,
                lazyEvaluator: this);
        }

        private IncludeOperation CreateIncludeOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            var elementOrder = _nextElementOrder++;

            var itemSpec = new ItemSpec<P, I>(itemElement.Include, _outerExpander, itemElement.IncludeLocation, rootDirectory);
            var referencedItemListsBuilder = new ReferencedItemListsBuilder(lazyEvaluator: this);
            referencedItemListsBuilder.Add(itemSpec);

            var excludes = GetExcludes(itemElement, ref referencedItemListsBuilder);
            var metadata = GetMetadata(itemElement, ref referencedItemListsBuilder);

            return new IncludeOperation(
                itemElement,
                itemSpec,
                referencedItemListsBuilder.ToImmutable(),
                conditionResult,
                elementOrder,
                rootDirectory,
                excludes,
                metadata,
                lazyEvaluator: this);
        }

        private RemoveOperation CreateRemoveOperation(string rootDirectory, ProjectItemElement itemElement, bool conditionResult)
        {
            var itemSpec = new ItemSpec<P, I>(itemElement.Remove, _outerExpander, itemElement.RemoveLocation, rootDirectory);
            var referencedItemListsBuilder = new ReferencedItemListsBuilder(lazyEvaluator: this);
            referencedItemListsBuilder.Add(itemSpec);

            var matchOnMetadata = GetMatchOnMetadata(itemElement, ref referencedItemListsBuilder);

            var matchOnMetadataOptions = Enum.TryParse(itemElement.MatchOnMetadataOptions, out MatchOnMetadataOptions options)
                ? options
                : MatchOnMetadataOptions.CaseSensitive;

            return new RemoveOperation(
                itemElement,
                itemSpec,
                referencedItemListsBuilder.ToImmutable(),
                conditionResult,
                matchOnMetadata,
                matchOnMetadataOptions,
                lazyEvaluator: this);
        }

        private ImmutableSegmentedList<string> GetExcludes(
            ProjectItemElement itemElement,
            ref ReferencedItemListsBuilder referencedItemListsBuilder)
        {
            string exclude = itemElement.Exclude;

            if (exclude.Length == 0)
            {
                return [];
            }

            IElementLocation location = itemElement.ExcludeLocation;

            // Expand properties here, because a property may have a value which is an item reference (ie "@(Bar)"), and
            //  if so we need to add the right item reference
            string evaluatedExclude = _expander.ExpandIntoStringLeaveEscaped(
                exclude,
                ExpanderOptions.ExpandProperties,
                location);

            if (evaluatedExclude.Length == 0)
            {
                return [];
            }

            var builder = ImmutableSegmentedList.CreateBuilder<string>();

            foreach (var excludeSplit in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedExclude))
            {
                builder.Add(excludeSplit);
                referencedItemListsBuilder.Add(excludeSplit, location);
            }

            return builder.ToImmutable();
        }

        private ImmutableList<string> GetMatchOnMetadata(
            ProjectItemElement itemElement,
            ref ReferencedItemListsBuilder referencedItemListsBuilder)
        {
            string matchOnMetadata = itemElement.MatchOnMetadata;

            if (matchOnMetadata.Length == 0)
            {
                return [];
            }

            IElementLocation location = itemElement.MatchOnMetadataLocation;

            string evaluatedMatchOnMetadata = _expander.ExpandIntoStringLeaveEscaped(
                matchOnMetadata,
                ExpanderOptions.ExpandProperties,
                location);

            if (evaluatedMatchOnMetadata.Length == 0)
            {
                return [];
            }

            var builder = ImmutableList.CreateBuilder<string>();

            foreach (string matchOnMetadataSplit in ExpressionShredder.SplitSemiColonSeparatedList(evaluatedMatchOnMetadata))
            {
                referencedItemListsBuilder.Add(matchOnMetadataSplit, location);

                string metadataExpanded = _expander.ExpandIntoStringLeaveEscaped(
                    matchOnMetadataSplit,
                    ExpanderOptions.ExpandPropertiesAndItems,
                    location);

                foreach (string metadataSplit in ExpressionShredder.SplitSemiColonSeparatedList(metadataExpanded))
                {
                    builder.Add(metadataSplit);
                }
            }

            return builder.ToImmutable();
        }

        private ImmutableArray<ProjectMetadataElement> GetMetadata(
            ProjectItemElement itemElement,
            ref ReferencedItemListsBuilder referencedItemListsBuilder)
        {
            if (!itemElement.HasMetadata)
            {
                return [];
            }

            using var builder = new RefArrayBuilder<ProjectMetadataElement>();

            ItemsAndMetadataPair itemsAndMetadataFound = new ItemsAndMetadataPair(null, null);

            // Since we're just attempting to expand properties in order to find referenced items and not expanding metadata,
            // unexpected errors may occur when evaluating property functions on unexpanded metadata. Just ignore them if that happens.
            // See: https://github.com/dotnet/msbuild/issues/3460
            const ExpanderOptions expanderOptions = ExpanderOptions.ExpandProperties | ExpanderOptions.LeavePropertiesUnexpandedOnError;

            foreach (var metadatumElement in itemElement.MetadataEnumerable)
            {
                builder.Add(metadatumElement);

                string expression = _expander.ExpandIntoStringLeaveEscaped(
                    metadatumElement.Value,
                    expanderOptions,
                    metadatumElement.Location);

                ExpressionShredder.GetReferencedItemNamesAndMetadata(
                    expression,
                    start: 0,
                    expression.Length,
                    ref itemsAndMetadataFound,
                    ShredderOptions.All);

                expression = _expander.ExpandIntoStringLeaveEscaped(
                    metadatumElement.Condition,
                    expanderOptions,
                    metadatumElement.ConditionLocation);

                ExpressionShredder.GetReferencedItemNamesAndMetadata(
                    expression,
                    start: 0,
                    expression.Length,
                    ref itemsAndMetadataFound,
                    ShredderOptions.All);
            }

            if (itemsAndMetadataFound.Items is { } items)
            {
                foreach (var itemType in items)
                {
                    referencedItemListsBuilder.Add(itemType);
                }
            }

            return builder.ToImmutable();
        }

        private ref struct ReferencedItemListsBuilder
        {
            // WORKAROUND: Unnecessary boxed allocation: https://github.com/dotnet/corefx/issues/24563
            private static readonly ImmutableDictionary<string, LazyItemList> s_empty = Traits.Instance.EscapeHatches.UseCaseSensitiveItemNames
                ? ImmutableDictionary<string, LazyItemList>.Empty
                : ImmutableDictionary.Create<string, LazyItemList>(StringComparer.OrdinalIgnoreCase);

            private readonly LazyItemEvaluator<P, I, M, D> _lazyEvaluator;
            private ImmutableDictionary<string, LazyItemList>.Builder? _builder;

            public ReferencedItemListsBuilder(LazyItemEvaluator<P, I, M, D> lazyEvaluator)
            {
                _lazyEvaluator = lazyEvaluator;
            }

            public void Add(string itemType)
            {
                if (_lazyEvaluator._itemLists.TryGetValue(itemType, out LazyItemList? itemList))
                {
                    itemList.MarkAsReferenced();

                    _builder ??= s_empty.ToBuilder();
                    _builder[itemType] = itemList;
                }
            }

            public void Add(ExpressionShredder.ItemExpressionCapture match)
            {
                if (match.ItemType is { } itemType)
                {
                    Add(itemType);
                }

                if (match.Captures is { } subMatches)
                {
                    foreach (var subMatch in subMatches)
                    {
                        Add(subMatch);
                    }
                }
            }

            public void Add(ItemSpec<P, I> itemSpec)
            {
                foreach (ItemSpecFragment fragment in itemSpec.Fragments)
                {
                    if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemExpression)
                    {
                        Add(itemExpression.Capture);
                    }
                }
            }

            public void Add(string expression, IElementLocation elementLocation)
            {
                if (expression.Length > 0 &&
                    Expander<P, I>.TryExpandSingleItemVectorExpressionIntoExpressionCapture(
                        expression, ExpanderOptions.ExpandItems, elementLocation, out var match))
                {
                    Add(match);
                }
            }

            public ImmutableDictionary<string, LazyItemList> ToImmutable()
                => _builder?.ToImmutable() ?? s_empty;
        }
    }
}
