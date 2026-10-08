// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal partial class LazyItemEvaluator<P, I, M, D>
    {
        /// <summary>
        ///  Removes matching items while preserving surviving order and earlier saved states.
        /// </summary>
        private sealed class RemoveOperation : LazyItemOperation
        {
            /// <summary>
            ///  The metadata tuple names, empty for identity/specification removal.
            /// </summary>
            private readonly ImmutableArray<string> _matchOnMetadata;

            /// <summary>
            ///  The eagerly built captured metadata set, null for ordinary specification removal.
            /// </summary>
            private readonly MetadataTrie<P, I> _metadataSet;

            /// <summary>
            ///  Initializes a Remove and eagerly validates/builds its metadata match set.
            /// </summary>
            /// <param name="element">The Remove XML.</param>
            /// <param name="spec">The property-expanded Remove specification.</param>
            /// <param name="references">The captured earlier item histories.</param>
            /// <param name="conditionResult">The combined group and item condition.</param>
            /// <param name="evaluator">The owning evaluator.</param>
            /// <param name="metadataNames">The expanded metadata names to match.</param>
            /// <param name="options">The metadata comparison policy.</param>
            public RemoveOperation(
                ProjectItemElement element,
                ItemSpec<P, I> spec,
                Dictionary<string, ItemListSnapshot> references,
                bool conditionResult,
                LazyItemEvaluator<P, I, M, D> evaluator,
                ImmutableArray<string> metadataNames,
                MatchOnMetadataOptions options)
                : base(element, spec, references, conditionResult, evaluator)
            {
                _matchOnMetadata = metadataNames;

                bool validReferences = true;
                if (!_matchOnMetadata.IsEmpty)
                {
                    foreach (ItemSpecFragment fragment in _itemSpec.Fragments)
                    {
                        if (fragment is not ItemSpec<P, I>.ItemExpressionFragment)
                        {
                            validReferences = false;
                            break;
                        }
                    }
                }
                ProjectFileErrorUtilities.VerifyThrowInvalidProjectFile(
                    validReferences,
                    new BuildEventFileInfo(string.Empty),
                    "OM_MatchOnMetadataIsRestrictedToReferencedItems");

                if (!_matchOnMetadata.IsEmpty)
                {
                    _metadataSet = new MetadataTrie<P, I>(options, _matchOnMetadata, _itemSpec);
                }
            }

            /// <summary>
            ///  Applies ordinary specification removal or captured metadata-tuple removal.
            /// </summary>
            /// <param name="listBuilder">The ordered working item state.</param>
            /// <param name="globsToIgnore">Later removals used only by Include pruning.</param>
            /// <remarks>
            ///  Bare self-removal clears the prefix. Large ordinary removals use the lazy normalized
            ///  index; metadata matching retains its independent tuple comparison policy.
            /// </remarks>
            protected override void ApplyImpl(OrderedItemDataCollection.Builder listBuilder, GlobExclusions globsToIgnore)
            {
                if (!_conditionResult)
                {
                    return;
                }

                bool matchingOnMetadata = !_matchOnMetadata.IsEmpty;
                if (!matchingOnMetadata)
                {
                    if (ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType))
                    {
                        // Perf optimization: If the Remove operation references itself (e.g. <I Remove="@(I)"/>)
                        // then all items are removed and matching is not necessary
                        listBuilder.Clear();
                        return;
                    }

                    if (listBuilder.Count >= Traits.Instance.DictionaryBasedItemRemoveThreshold)
                    {
                        // Perf optimization: If the number of items in the running list is large, construct a dictionary,
                        // enumerate all items referenced by the item spec, and perform dictionary look-ups to find items
                        // to remove.
                        IList<string> matches = _itemSpec.IntersectsWith(listBuilder.Dictionary);
                        listBuilder.RemoveAll(matches);
                        return;
                    }
                }

                HashSet<I> items = null;
                foreach (ItemData item in listBuilder)
                {
                    bool isMatch = matchingOnMetadata ? MatchesItemOnMetadata(item.Item) : _itemSpec.MatchesItem(item.Item);
                    if (isMatch)
                    {
                        items ??= new HashSet<I>();
                        items.Add(item.Item);
                    }
                }
                if (items is not null)
                {
                    listBuilder.RemoveAll(items);
                }
            }

            /// <summary>
            ///  Tests the item's ordered metadata tuple against the captured source set.
            /// </summary>
            /// <param name="item">The candidate item.</param>
            /// <returns>
            ///  Whether the metadata tuple matches.
            /// </returns>
            private bool MatchesItemOnMetadata(I item) => _metadataSet.Contains(item, _matchOnMetadata);

            /// <summary>
            ///  Appends statically known true-condition removed globs to the owning history.
            /// </summary>
            /// <param name="patterns">The history's append-only removal-pattern list.</param>
            public void AppendRemovedGlobs(List<string> patterns)
            {
                if (!_conditionResult)
                {
                    return;
                }
                foreach (ItemSpecFragment fragment in _itemSpec.Fragments)
                {
                    if (fragment is GlobFragment glob)
                    {
                        patterns.Add(glob.TextFragment);
                    }
                }
            }
        }
    }
}
