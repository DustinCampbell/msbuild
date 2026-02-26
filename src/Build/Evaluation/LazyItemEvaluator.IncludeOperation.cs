// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
        private sealed class IncludeOperation : ItemOperation
        {
            private readonly int _elementOrder;
            private readonly string? _rootDirectory;
            private readonly ImmutableArray<string> _excludes;
            private readonly ImmutableArray<ProjectMetadataElement> _metadata;

            public IncludeOperation(
                ProjectItemElement itemElement,
                ItemSpec<P, I> itemSpec,
                IReadOnlyDictionary<string, ItemListRef> referencedItemLists,
                bool conditionResult,
                int elementOrder,
                string rootDirectory,
                ImmutableArray<string> excludes,
                ImmutableArray<ProjectMetadataElement> metadata,
                LazyItemEvaluator<P, I, M, D> lazyEvaluator)
                : base(itemElement, itemSpec, referencedItemLists, conditionResult, lazyEvaluator)
            {
                _elementOrder = elementOrder;
                _rootDirectory = rootDirectory;
                _excludes = excludes.IsDefault ? [] : excludes;
                _metadata = metadata.IsDefault ? [] : metadata;
            }

            protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, GlobSet globsToIgnore)
            {
                var items = new RefArrayBuilder<I>(initialCapacity: _itemSpec.Fragments.Count);
                try
                {
                    CollectItems(globsToIgnore, ref items);
                    MutateItems(items.AsSpan());
                    SaveItems(items.AsSpan(), listBuilder);
                }
                finally
                {
                    items.Dispose();
                }
            }

            /// <summary>
            ///  Produce the items to operate on. For example, create new ones or select existing ones.
            /// </summary>
            [SuppressMessage("Microsoft.Dispose", "CA2000:Dispose objects before losing scope", Justification = "_lazyEvaluator._evaluationProfiler has own dipose logic.")]
            private void CollectItems(GlobSet globsToIgnore, ref RefArrayBuilder<I> collector)
            {
                using var excludePatterns = new RefArrayBuilder<string>();

                if (_excludes.Length > 0)
                {
                    // STEP 4: Evaluate, split, expand and subtract any Exclude
                    foreach (string exclude in _excludes)
                    {
                        string excludeExpanded = _expander.ExpandIntoStringLeaveEscaped(
                            exclude,
                            ExpanderOptions.ExpandPropertiesAndItems,
                            _itemElement.ExcludeLocation);

                        foreach (var excludeSplit in ExpressionShredder.SplitSemiColonSeparatedList(excludeExpanded))
                        {
                            excludePatterns.Add(excludeSplit);
                        }
                    }
                }

                // Partition exclude patterns into literal (no wildcards) and glob (has wildcards).
                // Literal excludes use a HashSet for O(1) per-item lookups instead of O(E) linear scans,
                // reducing the overall complexity from O(N*E) to O(N+E) for the common all-literal case.
                HashSet<string>? literalExcludeSet = null;
                using var globExcludePatterns = new RefArrayBuilder<string>();

                if (excludePatterns.Count > 0)
                {
                    foreach (string pattern in excludePatterns.AsSpan())
                    {
                        if (EngineFileUtilities.FilespecHasWildcards(pattern))
                        {
                            globExcludePatterns.Add(pattern);
                        }
                        else
                        {
                            literalExcludeSet ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            string unescaped = EscapingUtilities.UnescapeAll(pattern);
                            literalExcludeSet.Add(FileUtilities.NormalizePathForComparisonNoThrow(unescaped, _rootDirectory));
                        }
                    }
                }

                bool hasAnyExcludes = literalExcludeSet is { Count: > 0 } || globExcludePatterns.Count > 0;
                FileSpecMatcherTester?[]? globMatchers = globExcludePatterns.Count > 0
                    ? new FileSpecMatcherTester?[globExcludePatterns.Count]
                    : null;

                ISet<string>? excludePatternsForGlobs = null;

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

                        if (hasAnyExcludes)
                        {
                            foreach (var item in itemsFromExpression)
                            {
                                if (!IsExcluded(item.EvaluatedInclude, _rootDirectory, literalExcludeSet, globExcludePatterns.AsSpan(), globMatchers))
                                {
                                    collector.Add(item);
                                }
                            }
                        }
                        else
                        {
                            foreach (var item in itemsFromExpression)
                            {
                                collector.Add(item);
                            }
                        }
                    }
                    else if (fragment is ValueFragment valueFragment)
                    {
                        string value = valueFragment.TextFragment;

                        if (!hasAnyExcludes || !IsExcluded(EscapingUtilities.UnescapeAll(value), _rootDirectory, literalExcludeSet, globExcludePatterns.AsSpan(), globMatchers))
                        {
                            collector.Add(_itemFactory.CreateItem(value, value, _itemElement.ContainingProject.FullPath));
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

                            // The glob expansion engine needs all exclude patterns (literal + glob) for file enumeration.
                            excludePatternsForGlobs ??= BuildExcludePatternsForGlobs(excludePatterns.AsSpan(), globsToIgnore);

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
                                collector.Add(_itemFactory.CreateItem(includeSplitFileEscaped, glob, _itemElement.ContainingProject.FullPath));
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(fragment.GetType().ToString());
                    }
                }

                static bool IsExcluded(
                    string itemToCheck,
                    string? directory,
                    HashSet<string>? literalExcludeSet,
                    ReadOnlySpan<string> globExcludePatterns,
                    FileSpecMatcherTester?[]? globMatchers)
                {
                    // Tests whether an item should be excluded. Literal excludes are checked via
                    // O(1) HashSet lookup; glob excludes fall back to per-pattern matching.
                    // Each item's path is normalized at most once for the literal check.

                    // O(1) check against pre-normalized literal excludes.
                    if (literalExcludeSet is not null)
                    {
                        string normalized = FileUtilities.NormalizePathForComparisonNoThrow(itemToCheck, directory);
                        if (literalExcludeSet.Contains(normalized))
                        {
                            return true;
                        }
                    }

                    // O(G) check against glob excludes where G is the number of glob patterns (typically small).
                    if (globMatchers is not null)
                    {
                        for (int i = 0; i < globMatchers.Length; i++)
                        {
                            if (globMatchers[i] is not FileSpecMatcherTester matcher)
                            {
                                matcher = FileSpecMatcherTester.Parse(directory, globExcludePatterns[i]);
                                globMatchers[i] = matcher;
                            }

                            if (matcher.IsMatch(itemToCheck))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }
            }

            private static HashSet<string> BuildExcludePatternsForGlobs(
                ReadOnlySpan<string> excludePatterns,
                GlobSet globsToIgnore)
            {
                var result = new HashSet<string>(excludePatterns.Length + globsToIgnore.Globs.Length);

                foreach (var excludePattern in excludePatterns)
                {
                    result.Add(excludePattern);
                }

                foreach (var glob in globsToIgnore.Globs)
                {
                    result.Add(glob);
                }

                return result;
            }

            // todo Refactoring: MutateItems should clone each item before mutation. See https://github.com/dotnet/msbuild/issues/2328
            private void MutateItems(ReadOnlySpan<I> items)
            {
                using var contexts = new RefArrayBuilder<ItemBatchingContext>(items.Length);

                foreach (var item in items)
                {
                    contexts.Add(new(item));
                }

                DecorateItemsWithMetadata(contexts.AsSpan(), _metadata);
            }

            private void SaveItems(ReadOnlySpan<I> items, OrderedItemDataCollection.Builder listBuilder)
            {
                foreach (var item in items)
                {
                    listBuilder.Add(new ItemData(item, _itemElement, _elementOrder, _conditionResult));
                }
            }
        }
    }
}
