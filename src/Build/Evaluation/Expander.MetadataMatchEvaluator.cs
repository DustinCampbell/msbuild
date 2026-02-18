// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// A functor that returns the value of the metadata in the match
    /// that is contained in the metadata dictionary it was created with.
    /// </summary>
    private readonly ref struct MetadataMatchEvaluator
    {
        /// <summary>
        /// Source of the metadata.
        /// </summary>
        private readonly IMetadataTable _metadata;

        /// <summary>
        /// Whether to expand built-in metadata, custom metadata, or both kinds.
        /// </summary>
        private readonly ExpanderOptions _options;

        private readonly IElementLocation _elementLocation;

        private readonly LoggingContext? _loggingContext;

        public MetadataMatchEvaluator(
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext)
        {
            _metadata = metadata;
            _options = options & (ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.LogOnItemMetadataSelfReference);
            _elementLocation = elementLocation;
            _loggingContext = loggingContext;

            ErrorUtilities.VerifyThrow(options != ExpanderOptions.Invalid, "Must be expanding metadata of some kind");
        }

        /// <summary>
        /// Expands a single item metadata, which may be qualified with an item type.
        /// </summary>
        public string ExpandSingleMetadata(Match itemMetadataMatch)
        {
            ErrorUtilities.VerifyThrow(itemMetadataMatch.Success, "Need a valid item metadata.");

            string metadataName = itemMetadataMatch.Groups[RegularExpressions.NameGroup].Value;

            bool isBuiltInMetadata = FileUtilities.ItemSpecModifiers.IsItemSpecModifier(metadataName);

            if ((isBuiltInMetadata && ((_options & ExpanderOptions.ExpandBuiltInMetadata) != 0)) ||
                (!isBuiltInMetadata && ((_options & ExpanderOptions.ExpandCustomMetadata) != 0)))
            {
                string? itemType = null;

                // check if the metadata is qualified with the item type
                if (itemMetadataMatch.Groups[RegularExpressions.ItemSpecificationGroup].Length > 0)
                {
                    itemType = itemMetadataMatch.Groups[RegularExpressions.ItemTypeGroup].Value;
                }

                string? metadataValue = _metadata.GetEscapedValue(itemType, metadataName);

                if (ShouldLogOnItemMetadataSelfReference() &&
                    !metadataName.IsNullOrEmpty() &&
                    _metadata is IItemTypeDefinition itemMetadata &&
                    (itemType.IsNullOrEmpty() || itemType == itemMetadata.ItemType))
                {
                    _loggingContext.LogComment(
                        MessageImportance.Low,
                        new BuildEventFileInfo(_elementLocation),
                        "ItemReferencingSelfInTarget",
                        itemMetadata.ItemType,
                        metadataName);
                }

                return ShouldTruncate(metadataValue)
                    ? TruncateString(metadataValue)
                    : metadataValue;
            }

            // look up the metadata - we may not have a value for it
            return itemMetadataMatch.Value;
        }

        [MemberNotNullWhen(true, nameof(_loggingContext))]
        private bool ShouldLogOnItemMetadataSelfReference()
            => (_options & ExpanderOptions.LogOnItemMetadataSelfReference) != 0 && _loggingContext != null;

        private bool ShouldTruncate(string value)
            => IsTruncationEnabled(_options) && value.Length > CharacterLimitPerExpansion;
    }
}
