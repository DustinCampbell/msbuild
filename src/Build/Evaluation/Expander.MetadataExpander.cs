// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Expands bare metadata expressions, like %(Compile.WarningLevel), or unqualified, like %(Compile).
    /// </summary>
    /// <remarks>
    /// This is a private nested class, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private static class MetadataExpander
    {
        /// <summary>
        /// Expands all embedded item metadata in the given string, using the bucketed items.
        /// Metadata may be qualified, like %(Compile.WarningLevel), or unqualified, like %(Compile).
        /// </summary>
        /// <param name="expression">The expression containing item metadata references.</param>
        /// <param name="metadata">The metadata to be expanded.</param>
        /// <param name="options">Used to specify what to expand.</param>
        /// <param name="elementLocation">The location information for error reporting purposes.</param>
        /// <param name="loggingContext">The logging context for this operation.</param>
        /// <returns>The string with item metadata expanded in-place, escaped.</returns>
        public static string ExpandMetadataLeaveEscaped(
            string expression,
            IMetadataTable metadata,
            ExpanderOptions options,
            IElementLocation elementLocation,
            LoggingContext? loggingContext = null)
        {
            if ((options & ExpanderOptions.ExpandMetadata) == 0)
            {
                return expression;
            }

            ErrorUtilities.VerifyThrow(metadata != null, "Cannot expand metadata without providing metadata");

            // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item metadata references, just bail
            // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!
            if (!expression.Contains("%("))
            {
                return expression;
            }

            var matchEvaluator = new MetadataMatchEvaluator(metadata, options, elementLocation, loggingContext);

            try
            {
                // If there are no item vectors in the string, we can run a simpler Regex to find item metadata references.
                // Otherwise, we must run the more complex Regex to find item metadata references that aren't contained in transforms.
                return !expression.Contains("@(")
                    ? ExpandSimpleItemMetadata(expression, in matchEvaluator)
                    : ExpandComplexItemMetadata(expression, in matchEvaluator);
            }
            catch (InvalidOperationException ex)
            {
                ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotExpandItemMetadata", expression, ex.Message);
            }

            return string.Empty;
        }

        private static string ExpandSimpleItemMetadata(string expression, ref readonly MetadataMatchEvaluator matchEvaluator)
        {
            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

            RegularExpressions.ReplaceAndAppend(
                expression,
                in matchEvaluator,
                builder,
                RegularExpressions.ItemMetadataRegex);

            // If the final result is the same as the original expression, then just return the original expression,
            // Otherwise, convert the final result to a string and return that.
            return builder.Equals(expression)
                ? expression
                : builder.ToString();
        }

        private static string ExpandComplexItemMetadata(string expression, ref readonly MetadataMatchEvaluator matchEvaluator)
        {
            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

            ExpressionShredder.ReferencedItemExpressionsEnumerator enumerator = ExpressionShredder.GetReferencedItemExpressions(expression);
            int start = 0;

            if (enumerator.MoveNext())
            {
                ExpressionShredder.ItemExpressionCapture first = enumerator.Current;

                if (enumerator.MoveNext())
                {
                    ExpressionShredder.ItemExpressionCapture second = enumerator.Current;

                    // we're in the uncommon case with a partially enumerated enumerator. We need to process the first two items we enumerated and the remaining ones.
                    // Move over the expression, skipping those that have been recognized as an item vector expression
                    // Anything other than an item vector expression we want to expand bare metadata in.
                    start = ProcessItemExpressionCapture(expression, in matchEvaluator, builder, start, in first);
                    start = ProcessItemExpressionCapture(expression, in matchEvaluator, builder, start, in second);

                    while (enumerator.MoveNext())
                    {
                        ExpressionShredder.ItemExpressionCapture current = enumerator.Current;
                        start = ProcessItemExpressionCapture(expression, in matchEvaluator, builder, start, in current);
                    }
                }
                else
                {
                    // There is only one item. Check to see if we're in the common case.
                    if (first.Value == expression && first.Separator == null)
                    {
                        // The most common case is where the transform is the whole expression
                        // Also if there were no valid item vector expressions found, then go ahead and do the replacement on
                        // the whole expression (which is what Orcas did).
                        return expression;
                    }

                    start = ProcessItemExpressionCapture(expression, in matchEvaluator, builder, start, in first);
                }
            }

            // If there's anything left after the last item vector expression
            // then we need to metadata replace and then append that
            if (start < expression.Length)
            {
                RegularExpressions.ReplaceAndAppend(
                    expression.Substring(start),
                    in matchEvaluator,
                    builder,
                    RegularExpressions.NonTransformItemMetadataRegex);
            }

            // If the final result is the same as the original expression, then just return the original expression,
            // Otherwise, convert the final result to a string and return that.
            return builder.Equals(expression)
                ? expression
                : builder.ToString();

            static int ProcessItemExpressionCapture(
                string expression,
                ref readonly MetadataMatchEvaluator matchEvaluator,
                SpanBasedStringBuilder builder,
                int start,
                in ExpressionShredder.ItemExpressionCapture capture)
            {
                // Extract the part of the expression that appears before the item vector expression
                // e.g. the ABC in ABC@(foo->'%(FullPath)')
                RegularExpressions.ReplaceAndAppend(
                    expression.Substring(start, capture.Index - start),
                    in matchEvaluator,
                    builder,
                    RegularExpressions.NonTransformItemMetadataRegex);

                // Expand any metadata that appears in the item vector expression's separator
                if (capture.Separator != null)
                {
                    RegularExpressions.ReplaceAndAppend(
                        capture.Value,
                        in matchEvaluator,
                        count: -1,
                        capture.SeparatorStart,
                        builder,
                        RegularExpressions.NonTransformItemMetadataRegex);
                }
                else
                {
                    // Append the item vector expression as is
                    // e.g. the @(foo->'%(FullPath)') in ABC@(foo->'%(FullPath)')
                    builder.Append(capture.Value);
                }

                // Move onto the next part of the expression that isn't an item vector expression
                return capture.Index + capture.Length;
            }
        }
    }
}
