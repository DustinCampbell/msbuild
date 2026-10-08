// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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

            /// <summary>
            ///  Initializes an Include with normalized construction data.
            /// </summary>
            /// <param name="element">The Include XML.</param>
            /// <param name="spec">The property-expanded Include specification.</param>
            /// <param name="references">The captured earlier item histories.</param>
            /// <param name="conditionResult">The combined group and item condition.</param>
            /// <param name="evaluator">The owning evaluator.</param>
            /// <param name="elementOrder">The global Include ordinal.</param>
            /// <param name="rootDirectory">The root project directory.</param>
            /// <param name="excludes">The property-expanded Exclude fragments.</param>
            /// <param name="metadata">The metadata XML in declaration order.</param>
            public IncludeOperation(
                ProjectItemElement element,
                ItemSpec<P, I> spec,
                Dictionary<string, LazyItemList>? references,
                bool conditionResult,
                LazyItemEvaluator<P, I, M, D> evaluator,
                int elementOrder,
                string rootDirectory,
                ImmutableArray<string> excludes,
                ImmutableArray<ProjectMetadataElement> metadata)
                : base(element, spec, references, conditionResult, evaluator)
            {
                _elementOrder = elementOrder;
                _rootDirectory = rootDirectory;
                _excludes = excludes;
                _metadata = metadata;
            }

            /// <summary>
            ///  Creates, decorates, and appends included items in fragment order.
            /// </summary>
            /// <param name="listBuilder">The working item state.</param>
            /// <param name="globsToIgnore">Later glob removals applicable to earlier Includes.</param>
            protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, ImmutableHashSet<string> globsToIgnore)
            {
                ImmutableArray<I> items = CreateItems(globsToIgnore);
                DecorateItemsWithMetadata(items.Select(i => new ItemBatchingContext(i)), _metadata);
                foreach (I item in items)
                {
                    listBuilder.Add(new ItemData(item, _itemElement, _elementOrder, _conditionResult));
                }
            }

            /// <summary>
            ///  Expands Include fragments and applies the operation's exclusions.
            /// </summary>
            /// <param name="globsToIgnore">The applicable later glob removals.</param>
            /// <returns>
            ///  The new items before metadata decoration.
            /// </returns>
            [SuppressMessage("Microsoft.Dispose", "CA2000:Dispose objects before losing scope", Justification = "_lazyEvaluator._evaluationProfiler has own dipose logic.")]
            private ImmutableArray<I> CreateItems(ImmutableHashSet<string> globsToIgnore)
            {
                ImmutableArray<I>.Builder? itemsToAdd = null;

                List<string> excludePatterns = [];
                if (!_excludes.IsEmpty)
                {
                    // STEP 4: Evaluate, split, expand and subtract any Exclude
                    foreach (string exclude in _excludes)
                    {
                        string excludeExpanded = _expander.ExpandIntoStringLeaveEscaped(exclude, ExpanderOptions.ExpandPropertiesAndItems, _itemElement.ExcludeLocation);
                        var excludeSplits = ExpressionShredder.SplitSemiColonSeparatedList(excludeExpanded);
                        excludePatterns.AddRange(excludeSplits);
                    }
                }

                ISet<string>? excludePatternsForGlobs = null;
                FileSpecMatcherTester?[]? matchers = null;

                foreach (var fragment in _itemSpec.Fragments)
                {
                    if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemReferenceFragment)
                    {
                        // STEP 3: If expression is "@(x)" copy specified list with its metadata, otherwise just treat as string
                        var itemsFromExpression = _expander.ExpandExpressionCaptureIntoItems(
                            itemReferenceFragment.Capture,
                            this,
                            _itemFactory,
                            ExpanderOptions.ExpandItems,
                            includeNullEntries: false,
                            isTransformExpression: out _,
                            elementLocation: _itemElement.IncludeLocation);

                        itemsToAdd ??= ImmutableArray.CreateBuilder<I>();

                        if (excludePatterns.Count > 0)
                        {
                            matchers ??= new FileSpecMatcherTester?[excludePatterns.Count];

                            foreach (var item in itemsFromExpression)
                            {
                                if (!ExcludeTester(_rootDirectory, excludePatterns, matchers, item.EvaluatedInclude))
                                {
                                    itemsToAdd.Add(item);
                                }
                            }
                        }
                        else
                        {
                            itemsToAdd.AddRange(itemsFromExpression);
                        }
                    }
                    else if (fragment is ValueFragment valueFragment)
                    {
                        string value = valueFragment.TextFragment;
                        if (excludePatterns.Count > 0)
                        {
                            matchers ??= new FileSpecMatcherTester?[excludePatterns.Count];
                            if (ExcludeTester(_rootDirectory, excludePatterns, matchers, EscapingUtilities.UnescapeAll(value)))
                            {
                                continue;
                            }
                        }
                        itemsToAdd ??= ImmutableArray.CreateBuilder<I>();
                        itemsToAdd.Add(_itemFactory.CreateItem(value, value, _itemElement.ContainingProject.FullPath));
                    }
                    else if (fragment is GlobFragment globFragment)
                    {
                        // If this item is behind a false condition and represents a full drive/filesystem scan, expanding it is
                        // almost certainly undesired. It should be skipped to avoid evaluation taking an excessive amount of time.
                        bool skipGlob = !_conditionResult && globFragment.IsFullFileSystemScan && !Traits.Instance.EscapeHatches.AlwaysEvaluateDangerousGlobs;
                        if (!skipGlob)
                        {
                            string glob = globFragment.TextFragment;

                            if (excludePatternsForGlobs == null)
                            {
                                excludePatternsForGlobs = BuildExcludePatternsForGlobs(globsToIgnore, excludePatterns);
                            }

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
                                itemsToAdd ??= ImmutableArray.CreateBuilder<I>();
                                itemsToAdd.Add(_itemFactory.CreateItem(includeSplitFileEscaped, glob, _itemElement.ContainingProject.FullPath));
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(fragment.GetType().ToString());
                    }
                }

                return itemsToAdd?.ToImmutable() ?? ImmutableArray<I>.Empty;

                static bool ExcludeTester(string? directory, List<string> excludePatterns, FileSpecMatcherTester?[] matchers, string item)
                {
                    if (excludePatterns.Count == 0)
                    {
                        return false;
                    }

                    bool found = false;
                    for (int i = 0; i < matchers.Length; ++i)
                    {
                        FileSpecMatcherTester? matcher = matchers[i];
                        if (!matcher.HasValue)
                        {
                            matcher = FileSpecMatcherTester.Parse(directory, excludePatterns[i]);
                            matchers[i] = matcher;
                        }

                        if (matcher.Value.IsMatch(item))
                        {
                            found = true;
                            break;
                        }
                    }

                    return found;
                }
            }

            private static ISet<string> BuildExcludePatternsForGlobs(ImmutableHashSet<string> globsToIgnore, List<string> excludePatterns)
            {
                var anyExcludes = excludePatterns.Count > 0;
                var anyGlobsToIgnore = globsToIgnore.Count > 0;

                if (anyExcludes)
                {
                    var patterns = new HashSet<string>(excludePatterns, StringComparer.Ordinal);
                    if (anyGlobsToIgnore)
                    {
                        patterns.UnionWith(globsToIgnore);
                    }
                    return patterns;
                }

                return globsToIgnore;
            }
        }
    }
}
