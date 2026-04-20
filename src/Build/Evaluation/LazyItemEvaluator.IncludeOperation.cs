// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        private class IncludeOperation : LazyItemOperation
        {
            private readonly int _elementOrder;
            private readonly string? _rootDirectory;
            private readonly ImmutableArray<string> _excludes;
            private readonly ImmutableArray<ProjectMetadataElement> _metadata;

            public IncludeOperation(
                ProjectItemElement itemElement,
                string itemType,
                ItemSpec<P, I> itemSpec,
                ImmutableDictionary<string, LazyItemList> referencedItemLists,
                bool conditionResult,
                ImmutableArray<ProjectMetadataElement> metadata,
                int elementOrder,
                string? rootDirectory,
                ImmutableArray<string> excludes,
                LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(itemElement, itemType, itemSpec, referencedItemLists, conditionResult, lazyEvaluator)
            {
                _elementOrder = elementOrder;
                _rootDirectory = rootDirectory;

                _excludes = excludes;
                _metadata = metadata;
            }

            protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                var items = SelectItems(globsToIgnore);
                MutateItems(items);
                SaveItems(items, listBuilder);
            }

            /// <summary>
            /// Produce the items to operate on. For example, create new ones or select existing ones
            /// </summary>
            [SuppressMessage("Microsoft.Dispose", "CA2000:Dispose objects before losing scope", Justification = "_lazyEvaluator._evaluationProfiler has own dipose logic.")]
            private ImmutableArray<I> SelectItems(ImmutableHashSet<string> globsToIgnore)
            {
                using RefArrayBuilder<string> excludePatterns = default;

                if (_excludes != null)
                {
                    // STEP 4: Evaluate, split, expand and subtract any Exclude
                    foreach (string exclude in _excludes)
                    {
                        string excludeExpanded = _expander.ExpandIntoStringLeaveEscaped(exclude, ExpanderOptions.ExpandPropertiesAndItems, _itemElement.ExcludeLocation);

                        foreach (string excludeSplit in ExpressionShredder.SplitSemiColonSeparatedList(excludeExpanded))
                        {
                            excludePatterns.Add(excludeSplit);
                        }
                    }
                }

                ImmutableHashSet<string>? excludePatternsForGlobs = null;
                FileSpecMatcherTester?[]? matchers = null;

                using RefArrayBuilder<I> itemsToAdd = default;

                foreach (var fragment in _itemSpec.Fragments)
                {
                    if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemReferenceFragment)
                    {
                        // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                        var itemsFromExpression = _expander.ExpandExpressionCaptureIntoItems(
                            itemReferenceFragment.Capture,
                            _evaluatorData,
                            _itemFactory,
                            ExpanderOptions.ExpandItems,
                            includeNullEntries: false,
                            isTransformExpression: out _,
                            elementLocation: _itemElement.IncludeLocation);

                        if (excludePatterns.Count > 0)
                        {
                            matchers ??= new FileSpecMatcherTester?[excludePatterns.Count];

                            foreach (var item in itemsFromExpression)
                            {
                                if (!ExcludeTester(_rootDirectory, excludePatterns.AsSpan(), matchers, item.EvaluatedInclude))
                                {
                                    itemsToAdd.Add(item);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < itemsFromExpression.Count; i++)
                            {
                                itemsToAdd.Add(itemsFromExpression[i]);
                            }
                        }
                    }
                    else if (fragment is ValueFragment valueFragment)
                    {
                        string value = valueFragment.TextFragment;
                        matchers ??= new FileSpecMatcherTester?[excludePatterns.Count];

                        if (excludePatterns.Count == 0 || !ExcludeTester(_rootDirectory, excludePatterns.AsSpan(), matchers, EscapingUtilities.UnescapeAll(value)))
                        {
                            itemsToAdd.Add(_itemFactory.CreateItem(value, value, _itemElement.ContainingProject.FullPath));
                        }
                    }
                    else if (fragment is GlobFragment globFragment)
                    {
                        // If this item is behind a false condition and represents a full drive/filesystem scan, expanding it is
                        // almost certainly undesired. It should be skipped to avoid evaluation taking an excessive amount of time.
                        bool skipGlob = !_conditionResult && globFragment.IsFullFileSystemScan && !Traits.Instance.EscapeHatches.AlwaysEvaluateDangerousGlobs;
                        if (!skipGlob)
                        {
                            string glob = globFragment.TextFragment;

                            excludePatternsForGlobs ??= BuildExcludePatternsForGlobs(globsToIgnore, excludePatterns.AsSpan());

                            string[] includeSplitFilesEscaped;
                            if (MSBuildEventSource.Log.IsEnabled())
                            {
                                MSBuildEventSource.Log.ExpandGlobStart(_rootDirectory ?? string.Empty, glob, string.Join(", ", excludePatternsForGlobs));
                            }

                            using (_lazyEvaluator?._evaluationProfiler.TrackGlob(_rootDirectory, glob, excludePatternsForGlobs))
                            {
                                includeSplitFilesEscaped = EngineFileUtilities.GetFileListEscaped(
                                    _rootDirectory,
                                    glob,
                                    excludePatternsForGlobs,
                                    fileMatcher: FileMatcher,
                                    loggingMechanism: _lazyEvaluator?._loggingContext,
                                    includeLocation: _itemElement.IncludeLocation,
                                    excludeLocation: _itemElement.ExcludeLocation);
                            }

                            if (MSBuildEventSource.Log.IsEnabled())
                            {
                                MSBuildEventSource.Log.ExpandGlobStop(_rootDirectory ?? string.Empty, glob, string.Join(", ", excludePatternsForGlobs));
                            }

                            foreach (string includeSplitFileEscaped in includeSplitFilesEscaped)
                            {
                                itemsToAdd.Add(_itemFactory.CreateItem(includeSplitFileEscaped, glob, _itemElement.ContainingProject.FullPath));
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(fragment.GetType().ToString());
                    }
                }

                return itemsToAdd.ToImmutable();

                static bool ExcludeTester(string? directory, ReadOnlySpan<string> excludePatterns, FileSpecMatcherTester?[] matchers, string item)
                {
                    if (excludePatterns.IsEmpty)
                    {
                        return false;
                    }

                    bool found = false;
                    for (int i = 0; i < matchers.Length; ++i)
                    {
                        FileSpecMatcherTester matcher = matchers[i] ??= FileSpecMatcherTester.Parse(directory, excludePatterns[i]);

                        if (matcher.IsMatch(item))
                        {
                            found = true;
                            break;
                        }
                    }

                    return found;
                }
            }

            private static ImmutableHashSet<string> BuildExcludePatternsForGlobs(ImmutableHashSet<string> globsToIgnore, ReadOnlySpan<string> excludePatterns)
            {
                if (excludePatterns.Length == 0)
                {
                    return globsToIgnore;
                }

                if (globsToIgnore.Count == 0)
                {
                    return ImmutableHashSet.Create(excludePatterns);
                }

                var builder = globsToIgnore.ToBuilder();

                foreach (string excludePattern in excludePatterns)
                {
                    builder.Add(excludePattern);
                }

                return builder.ToImmutable();
            }

            // todo Refactoring: MutateItems should clone each item before mutation. See https://github.com/dotnet/msbuild/issues/2328
            private void MutateItems(ImmutableArray<I> items)
            {
                DecorateItemsWithMetadata(items.Select(i => new ItemBatchingContext(i)), _metadata);
            }

            private void SaveItems(ImmutableArray<I> items, OrderedItemDataCollection.Builder listBuilder)
            {
                foreach (var item in items)
                {
                    listBuilder.Add(new ItemData(item, _itemElement, _elementOrder, _conditionResult));
                }
            }
        }
    }
}
