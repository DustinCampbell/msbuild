// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class LazyItemEvaluator<P, I, M, D>
{
    /// <summary>
    ///  Clones matching items and decorates their metadata without changing earlier item states.
    /// </summary>
    private sealed class UpdateOperation : LazyItemOperation
    {
        /// <summary>
        ///  The metadata XML in declaration order.
        /// </summary>
        private readonly ImmutableArray<ProjectMetadataElement> _metadata;

        /// <summary>
        ///  Reusable contexts for the current application.
        /// </summary>
        private List<ItemBatchingContext> _itemsToUpdate;

        /// <summary>
        ///  The matching mode, prepared once on the first true-condition application.
        /// </summary>
        private MatchKind _matchKind;

        /// <summary>
        ///  Item-reference fragments used when qualified metadata requires source capture.
        /// </summary>
        private ImmutableArray<ItemSpec<P, I>.ItemExpressionFragment> _referenceFragments;

        /// <summary>
        ///  Other fragments that can match without capturing a source item.
        /// </summary>
        private ImmutableArray<ItemSpecFragment> _otherFragments;

        /// <summary>
        ///  Whether metadata values or conditions need per-item expansion.
        /// </summary>
        private bool? _needToExpandMetadataForEachItem;

        /// <summary>
        ///  Whether literal-batch eligibility has already been checked.
        /// </summary>
        private bool _literalKeysChecked;

        /// <summary>
        ///  Cached normalized literal keys, or null when the operation cannot be batched.
        /// </summary>
        private string[] _literalKeys;

        /// <summary>
        ///  Whether qualified source indexing has been prepared or rejected for wildcard values.
        /// </summary>
        private bool _sourceIndexPrepared;

        /// <summary>
        ///  Read-only matching source tables keyed by normalized item identity.
        /// </summary>
        private Dictionary<string, Dictionary<string, I>> _matchingSources;

        /// <summary>
        ///  Identifies the matching algorithm without allocating strategy delegates.
        /// </summary>
        private enum MatchKind
        {
            /// <summary>
            ///  Matching has not yet been prepared.
            /// </summary>
            Uninitialized,

            /// <summary>
            ///  A bare self-reference matches every item.
            /// </summary>
            All,

            /// <summary>
            ///  Normal specification matching requires no qualified source capture.
            /// </summary>
            Specification,

            /// <summary>
            ///  Matching also captures the last matching source item of each referenced type.
            /// </summary>
            Capture,
        }

        /// <summary>
        ///  Initializes an Update with its normalized specification and captured references.
        /// </summary>
        /// <param name="element">The Update XML.</param>
        /// <param name="spec">The property-expanded Update specification.</param>
        /// <param name="references">The captured earlier item histories.</param>
        /// <param name="conditionResult">The combined group and item condition.</param>
        /// <param name="evaluator">The owning evaluator.</param>
        /// <param name="metadata">The metadata XML in declaration order.</param>
        public UpdateOperation(
            ProjectItemElement element,
            ItemSpec<P, I> spec,
            Dictionary<string, ItemListSnapshot> references,
            bool conditionResult,
            LazyItemEvaluator<P, I, M, D> evaluator,
            ImmutableArray<ProjectMetadataElement> metadata)
            : base(element, spec, references, conditionResult, evaluator)
        {
            _metadata = metadata;
        }

        /// <summary>
        ///  Applies specification matching and bulk metadata decoration to the working item state.
        /// </summary>
        /// <param name="items">The ordered working state.</param>
        /// <param name="globsToIgnore">The materialization's applicable later glob removals.</param>
        protected override void ApplyImpl(OrderedItemDataCollection.Builder items, ImmutableHashSet<string> globsToIgnore)
        {
            if (!_conditionResult)
            {
                return;
            }
            PrepareMatching();
            (_itemsToUpdate ??= []).Clear();
            for (int index = 0; index < items.Count; index++)
            {
                ItemData data = items[index];
                if (Matches(data.Item, out Dictionary<string, I> captured))
                {
                    items[index] = CloneForUpdate(data, captured);
                }
            }
            // Preserve bulk decoration even when no item matches: constant metadata can diagnose errors.
            DecorateItemsWithMetadata(_itemsToUpdate, _metadata, _needToExpandMetadataForEachItem);
        }

        /// <summary>
        ///  Applies the same matching/cloning kernel to one item selected by a literal batch.
        /// </summary>
        /// <param name="item">The candidate item.</param>
        /// <returns>
        ///  A decorated clone when matched, otherwise the original entry.
        /// </returns>
        public ItemData UpdateItem(ItemData item)
        {
            if (!_conditionResult)
            {
                return item;
            }
            PrepareMatching();
            if (!Matches(item.Item, out Dictionary<string, I> captured))
            {
                return item;
            }
            (_itemsToUpdate ??= []).Clear();
            ItemData updated = CloneForUpdate(item, captured);
            DecorateItemsWithMetadata(_itemsToUpdate, _metadata, _needToExpandMetadataForEachItem);
            return updated;
        }

        /// <summary>
        ///  Classifies and normalizes all literal batch keys before any candidate enters a batch.
        /// </summary>
        /// <returns>
        ///  Unique normalized keys, or <see langword="null"/> for a nonliteral or duplicate specification.
        /// </returns>
        public string[] GetLiteralKeys()
        {
            if (_literalKeysChecked)
            {
                return _literalKeys;
            }
            _literalKeysChecked = true;
            foreach (ItemSpecFragment fragment in _itemSpec.Fragments)
            {
                foreach (string token in MSBuildConstants.CharactersForExpansion)
                {
                    if (fragment.TextFragment.IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        return null;
                    }
                }
            }

            string[] keys = new string[_itemSpec.Fragments.Count];
            HashSet<string> unique = keys.Length > 1 ? new(StringComparer.OrdinalIgnoreCase) : null;
            for (int index = 0; index < keys.Length; index++)
            {
                ItemSpecFragment fragment = _itemSpec.Fragments[index];
                string key = FileUtilities.NormalizePathForComparisonNoThrow(fragment.TextFragment, fragment.ProjectDirectory);
                if (unique is not null && !unique.Add(key))
                {
                    return null;
                }
                keys[index] = key;
            }
            return _literalKeys = keys;
        }

        /// <summary>
        ///  Clones an entry before mutation and registers its metadata context.
        /// </summary>
        /// <param name="item">The earlier entry.</param>
        /// <param name="captured">Matching source items for qualified metadata.</param>
        /// <returns>
        ///  The clone retaining its originating Include and ordering data.
        /// </returns>
        private ItemData CloneForUpdate(ItemData item, Dictionary<string, I> captured)
        {
            ItemData clone = item.Clone(_itemFactory, _itemElement);
            _itemsToUpdate.Add(new ItemBatchingContext(clone.Item, captured));
            return clone;
        }

        /// <summary>
        ///  Prepares metadata requirements and matching fragments once per recorded operation.
        /// </summary>
        private void PrepareMatching()
        {
            if (_matchKind != MatchKind.Uninitialized)
            {
                return;
            }
            _needToExpandMetadataForEachItem = NeedToExpandMetadataForEachItem(_metadata, out ItemsAndMetadataPair references);
            if (ItemspecContainsASingleBareItemReference(_itemSpec, _itemElement.ItemType))
            {
                _matchKind = MatchKind.All;
                return;
            }

            bool qualified = false;
            if (references.Metadata is not null)
            {
                foreach (var reference in references.Metadata.Values)
                {
                    if (!string.IsNullOrWhiteSpace(reference.ItemName))
                    {
                        qualified = true;
                        break;
                    }
                }
            }

            if (qualified && !Traits.Instance.EscapeHatches.DoNotExpandQualifiedMetadataInUpdateOperation)
            {
                var itemReferences = ImmutableArray.CreateBuilder<ItemSpec<P, I>.ItemExpressionFragment>();
                var other = ImmutableArray.CreateBuilder<ItemSpecFragment>();
                foreach (ItemSpecFragment fragment in _itemSpec.Fragments)
                {
                    if (fragment is ItemSpec<P, I>.ItemExpressionFragment itemReference)
                    {
                        itemReferences.Add(itemReference);
                    }
                    else
                    {
                        other.Add(fragment);
                    }
                }
                if (itemReferences.Count > 0)
                {
                    _referenceFragments = itemReferences.ToImmutable();
                    _otherFragments = other.ToImmutable();
                    _matchKind = MatchKind.Capture;
                    return;
                }
            }
            _matchKind = MatchKind.Specification;
        }

        /// <summary>
        ///  Matches an item and captures qualified source metadata only when that mode requires it.
        /// </summary>
        /// <param name="item">The candidate item.</param>
        /// <param name="captured">The last matching source of each referenced item type.</param>
        /// <returns>
        ///  Whether the item matches the Update specification.
        /// </returns>
        private bool Matches(I item, out Dictionary<string, I> captured)
        {
            captured = null;
            if (_matchKind == MatchKind.All)
            {
                return true;
            }
            if (_matchKind == MatchKind.Specification)
            {
                return _itemSpec.MatchesItem(item);
            }

            bool matches = false;
            foreach (ItemSpecFragment fragment in _otherFragments)
            {
                if (fragment.IsMatch(item.EvaluatedInclude))
                {
                    matches = true;
                    break;
                }
            }
            PrepareSourceIndex();
            if (_matchingSources is not null)
            {
                string key = FileUtilities.NormalizePathForComparisonNoThrow(item.EvaluatedInclude, _referenceFragments[0].ProjectDirectory);
                if (_matchingSources.TryGetValue(key, out captured))
                {
                    matches = true;
                }
                return matches;
            }

            foreach (ItemSpec<P, I>.ItemExpressionFragment fragment in _referenceFragments)
            {
                foreach (ItemSpec<P, I>.ReferencedItem source in fragment.ReferencedItems)
                {
                    if (source.ItemAsValueFragment.IsMatch(item.EvaluatedInclude))
                    {
                        matches = true;
                        (captured ??= new(StringComparer.OrdinalIgnoreCase))[source.Item.Key] = source.Item;
                    }
                }
            }
            return matches;
        }

        /// <summary>
        ///  Indexes literal source identities once while retaining the last matching item per type.
        /// </summary>
        /// <remarks>
        ///  Transforms may yield wildcard values that match more than one identity. Those keep the
        ///  specification-matching path so indexing never changes their matching or winner semantics.
        ///  Indexed metadata tables are never mutated after construction and can be shared by matches.
        /// </remarks>
        private void PrepareSourceIndex()
        {
            if (_sourceIndexPrepared)
            {
                return;
            }
            _sourceIndexPrepared = true;
            foreach (ItemSpec<P, I>.ItemExpressionFragment fragment in _referenceFragments)
            {
                foreach (ItemSpec<P, I>.ReferencedItem source in fragment.ReferencedItems)
                {
                    if (EngineFileUtilities.FilespecHasWildcards(source.ItemAsValueFragment.TextFragment))
                    {
                        return;
                    }
                }
            }

            _matchingSources = new(StringComparer.OrdinalIgnoreCase);
            foreach (ItemSpec<P, I>.ItemExpressionFragment fragment in _referenceFragments)
            {
                foreach (ItemSpec<P, I>.ReferencedItem source in fragment.ReferencedItems)
                {
                    string key = FileUtilities.NormalizePathForComparisonNoThrow(
                        EscapingUtilities.UnescapeAll(source.ItemAsValueFragment.TextFragment), fragment.ProjectDirectory);
                    if (!_matchingSources.TryGetValue(key, out Dictionary<string, I> sources))
                    {
                        sources = new(StringComparer.OrdinalIgnoreCase);
                        _matchingSources.Add(key, sources);
                    }
                    sources[source.Item.Key] = source.Item;
                }
            }
        }
    }
}
