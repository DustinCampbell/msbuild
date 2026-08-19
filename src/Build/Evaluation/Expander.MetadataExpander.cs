// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    /// <summary>
    ///  Expands bare metadata expressions, like %(Compile.WarningLevel), or unqualified, like %(Compile).
    /// </summary>
    /// <remarks>
    ///  This is a private nested ref struct exposed only through its static expansion entry points.
    /// </remarks>
    private readonly ref struct MetadataExpander
    {
        private readonly IMetadataTable _metadata;
        private readonly ExpanderOptions _options;
        private readonly IElementLocation _elementLocation;
        private readonly LoggingContext? _loggingContext;
        private readonly SpanBasedStringBuilder _builder;

        private MetadataExpander(
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext,
            SpanBasedStringBuilder builder)
        {
            _metadata = metadata;
            _options = options & (ExpanderOptions.ExpandMetadata | ExpanderOptions.Truncate | ExpanderOptions.LogOnItemMetadataSelfReference);
            _elementLocation = elementLocation;
            _loggingContext = loggingContext;
            _builder = builder;
        }

        /// <summary>
        ///  Expands all embedded item metadata in the given string, using the bucketed items.
        ///  Metadata may be qualified, like %(Compile.WarningLevel), or unqualified, like %(Compile).
        /// </summary>
        /// <param name="expression">The expression containing item metadata references.</param>
        /// <param name="metadata">The metadata to be expanded.</param>
        /// <param name="options">Used to specify what to expand.</param>
        /// <param name="elementLocation">The location information for error reporting purposes.</param>
        /// <param name="loggingContext">The logging context for this operation.</param>
        /// <returns>
        ///  The string with item metadata expanded in-place, escaped.
        /// </returns>
        internal static string ExpandMetadataLeaveEscaped(
            string expression,
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext = null)
            => ExpandMetadataLeaveEscaped(
                expression.AsSegment(),
                metadata,
                options,
                elementLocation,
                loggingContext);

        /// <inheritdoc cref="ExpandMetadataLeaveEscaped(string, IMetadataTable, ExpanderOptions, IElementLocation, LoggingContext?)"/>
        /// <param name="expression">The expression segment containing item metadata references.</param>
        /// <param name="metadata">The metadata to be expanded.</param>
        /// <param name="options">Used to specify what to expand.</param>
        /// <param name="elementLocation">The location information for error reporting purposes.</param>
        /// <param name="loggingContext">The logging context for this operation.</param>
        internal static string ExpandMetadataLeaveEscaped(
            StringSegment expression,
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext = null)
        {
            if ((options & ExpanderOptions.ExpandMetadata) == 0)
            {
                return expression.ValueOrEmpty;
            }

            Assumed.NotNull(metadata, "Cannot expand metadata without providing metadata");

            // PERF NOTE: pre-scanning the string for "%(" is cheaper than a full scan.
            if (!ExpressionShredder.ContainsMetadataMarker(expression))
            {
                return expression.ValueOrEmpty;
            }

            try
            {
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
                MetadataExpander expander = new(metadata, options, elementLocation, loggingContext, builder);

                return expander.Expand(expression);
            }
            catch (InvalidOperationException ex)
            {
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotExpandItemMetadata", expression.ValueOrEmpty, ex.Message);
            }

            return Assumed.Unreachable<string>();
        }

        private string Expand(StringSegment expression)
        {
            if (!ExpressionShredder.TryGetNextItemVectorExpression(expression, out ExpressionShredder.ItemExpressionCapture itemVector))
            {
                // No well-formed item vectors in the string — scan for metadata references directly.
                ScanAndExpandMetadata(expression);
            }
            else if (itemVector.Index == 0 && itemVector.Length == expression.Length && !itemVector.SeparatorSegment.HasValue)
            {
                // The entire expression is a single item vector with no separator, so there are
                // no gaps to expand metadata in — return the expression unchanged.
                return expression.ValueOrEmpty;
            }
            else
            {
                ScanAndExpandMetadataInGaps(expression, itemVector);
            }

            return _builder.Equals(expression.AsSpan())
                ? expression.ValueOrEmpty
                : _builder.ToString();
        }

        /// <summary>
        ///  Expands metadata in the gaps between item vector expressions and within their separators.
        /// </summary>
        private void ScanAndExpandMetadataInGaps(StringSegment expression, ExpressionShredder.ItemExpressionCapture itemVector)
        {
            int start = 0;

            do
            {
                start = ProcessItemExpressionCapture(expression, start, itemVector);
            }
            while (ExpressionShredder.TryGetNextItemVectorExpression(expression, start, out itemVector));

            // Expand metadata in any trailing text after the last item vector expression.
            if (start < expression.Length)
            {
                ScanAndExpandMetadata(expression, start, expression.Length);
            }
        }

        private int ProcessItemExpressionCapture(StringSegment expression, int start, ExpressionShredder.ItemExpressionCapture itemVector)
        {
            // Expand metadata in the gap before this item vector expression.
            if (itemVector.Index > start)
            {
                ScanAndExpandMetadata(expression, start, itemVector.Index);
            }

            // Expand metadata that appears in the item vector expression's separator.
            if (itemVector.SeparatorSegment.HasValue)
            {
                // Append the portion before the separator verbatim, then expand within the separator portion.
                StringSegment value = itemVector.ValueSegment;
                int separatorStart = itemVector.SeparatorStart;

                _builder.Append(value.AsMemory(0, separatorStart));
                ScanAndExpandMetadata(value, separatorStart, value.Length);
            }
            else
            {
                // Append the item vector expression as-is.
                _builder.Append(itemVector.ValueSegment.AsMemory());
            }

            // Advance past this item vector expression.
            return itemVector.Index + itemVector.Length;
        }

        /// <inheritdoc cref="ScanAndExpandMetadata(StringSegment, int, int)" />
        private void ScanAndExpandMetadata(StringSegment input)
            => ScanAndExpandMetadata(input, 0, input.Length);

        /// <summary>
        ///  Scans the specified range of <paramref name="input"/> for item metadata references
        ///  of the form <c>%(Name)</c> or <c>%(ItemType.Name)</c>, expands them using the
        ///  provided metadata table, and appends the results to the builder.
        /// </summary>
        /// <remarks>
        ///  A valid metadata name starts with a letter or underscore, followed by zero or more
        ///  letters, digits, underscores, or hyphens: <c>[A-Za-z_][A-Za-z_0-9\-]*</c>. This grammar is
        ///  enforced by <see cref="ExpressionShredder.TryParseMetadataExpression"/> and must stay in sync
        ///  with <see cref="ProjectWriter.itemTypeOrMetadataNameSpecification"/>.
        ///  Whitespace is allowed around the parentheses and the dot separator.
        ///  If a <c>%(</c> sequence does not form a valid metadata reference, it is appended
        ///  to the output verbatim.
        /// </remarks>
        private void ScanAndExpandMetadata(StringSegment input, int startIndex, int endIndex)
        {
            int lastCopied = startIndex;

            int i = ExpressionShredder.IndexOfMetadataMarker(input, startIndex);

            while (i >= 0 && i < endIndex - 1)
            {
                int pos = i + 2;

                if (!ExpressionShredder.TryParseMetadataExpression(
                    input,
                    ref pos,
                    endIndex,
                    out StringSegment itemType,
                    out StringSegment metadataName))
                {
                    // Not a valid metadata reference — skip past '%(' and keep scanning.
                    i = ExpressionShredder.IndexOfMetadataMarker(input, i + 2);
                    continue;
                }

                // Append everything before this reference.
                if (i > lastCopied)
                {
                    _builder.Append(input.AsMemory(lastCopied, i - lastCopied));
                }

                // Determine whether to expand this metadata reference.
                string metadataNameString = metadataName.ValueOrEmpty;
                bool isBuiltInMetadata = ItemSpecModifiers.IsItemSpecModifier(metadataNameString);

                if ((isBuiltInMetadata && ((_options & ExpanderOptions.ExpandBuiltInMetadata) != 0)) ||
                   (!isBuiltInMetadata && ((_options & ExpanderOptions.ExpandCustomMetadata) != 0)))
                {
                    string? itemTypeString = itemType.Value;
                    string expanded = _metadata.GetEscapedValue(itemTypeString, metadataNameString);

                    if ((_options & ExpanderOptions.LogOnItemMetadataSelfReference) != 0 &&
                        _loggingContext != null &&
                        metadataName.Length > 0 &&
                        _metadata is IItemTypeDefinition itemMetadata &&
                        (!itemType.HasValue || itemType.Equals(itemMetadata.ItemType, StringComparison.Ordinal)))
                    {
                        _loggingContext.LogComment(
                            MessageImportance.Low,
                            new BuildEventFileInfo(_elementLocation),
                            "ItemReferencingSelfInTarget",
                            itemMetadata.ItemType,
                            metadataNameString);
                    }

                    if (IsTruncationEnabled(_options) && expanded.Length > CharacterLimitPerExpansion)
                    {
                        expanded = TruncateString(expanded);
                    }

                    _builder.Append(expanded);
                }
                else
                {
                    _builder.Append(input.AsMemory(i, pos - i));
                }

                lastCopied = pos;

                // Continue scanning after this reference.
                i = ExpressionShredder.IndexOfMetadataMarker(input, pos);
            }

            // Append any remaining text after the last reference.
            if (lastCopied < endIndex)
            {
                _builder.Append(input.AsMemory(lastCopied, endIndex - lastCopied));
            }
        }
    }
}
