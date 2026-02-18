// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.RegularExpressions;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    /// <summary>
    /// Regular expressions used by the expander.
    /// The expander currently uses regular expressions rather than a parser to do its work.
    /// </summary>
    private static partial class RegularExpressions
    {
        /**************************************************************************************************************************
        * WARNING: The regular expressions below MUST be kept in sync with the expressions in the ProjectWriter class -- if the
        * description of an item vector changes, the expressions must be updated in both places.
        *************************************************************************************************************************/

#if NET
        [GeneratedRegex(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
        internal static partial Regex ItemMetadataRegex { get; }
#else
        /// <summary>
        /// Regular expression used to match item metadata references embedded in strings.
        /// For example, %(Compile.DependsOn) or %(DependsOn).
        /// </summary>
        internal static Regex ItemMetadataRegex
            => field ??= new(ItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
#endif

        /// <summary>
        /// Name of the group matching the "name" of a metadatum.
        /// </summary>
        internal const string NameGroup = "NAME";

        /// <summary>
        /// Name of the group matching the prefix on a metadata expression, for example "Compile." in "%(Compile.Object)".
        /// </summary>
        internal const string ItemSpecificationGroup = "ITEM_SPECIFICATION";

        /// <summary>
        /// Name of the group matching the item type in an item expression or metadata expression.
        /// </summary>
        internal const string ItemTypeGroup = "ITEM_TYPE";

        internal const string NonTransformItemMetadataSpecification = $"""
            ((?<={ItemVectorWithTransformLHS}){ItemMetadataSpecification}(?!{ItemVectorWithTransformRHS})) |
            ((?<!{ItemVectorWithTransformLHS}){ItemMetadataSpecification}(?={ItemVectorWithTransformRHS})) |
            ((?<!{ItemVectorWithTransformLHS}){ItemMetadataSpecification}(?!{ItemVectorWithTransformRHS}))
            """;

#if NET
        [GeneratedRegex(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture)]
        internal static partial Regex NonTransformItemMetadataRegex { get; }
#else
        /// <summary>
        /// regular expression used to match item metadata references outside of item vector transforms.
        /// </summary>
        /// <remarks>PERF WARNING: this Regex is complex and tends to run slowly.</remarks>
        internal static Regex NonTransformItemMetadataRegex
            => field ??= new(NonTransformItemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.Compiled);
#endif

        private const string NameSpecification = ProjectWriter.itemTypeOrMetadataNameSpecification;

        /// <summary>
        /// Complete description of an item metadata reference, including the optional qualifying item type.
        /// For example, %(Compile.DependsOn) or %(DependsOn).
        /// </summary>
        private const string ItemMetadataSpecification = $"""
            %\(\s*
              (?<ITEM_SPECIFICATION>(?<ITEM_TYPE>{NameSpecification})\s*\.\s*)?
              (?<NAME>{NameSpecification}) \s*\)
            """;

        /// <summary>
        /// description of an item vector with a transform, left hand side.
        /// </summary>
        private const string ItemVectorWithTransformLHS = $@"@\(\s*{NameSpecification}\s*->\s*'[^']*";

        /// <summary>
        /// description of an item vector with a transform, right hand side.
        /// </summary>
        private const string ItemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

        /**************************************************************************************************************************
         * WARNING: The regular expressions above MUST be kept in sync with the expressions in the ProjectWriter class.
         *************************************************************************************************************************/

        /// <summary>
        /// Copied from <see cref="Regex.Replace(string, MatchEvaluator, int, int)"/> and modified to use a <see cref="SpanBasedStringBuilder"/> rather than repeatedly allocating a <see cref="System.Text.StringBuilder"/>. This
        /// allows us to avoid intermediate string allocations when repeatedly doing replacements. 
        /// </summary>
        /// <param name="input">The string to operate on.</param>
        /// <param name="metadataMatchEvaluator">State used in the transform function.</param>
        /// <param name="stringBuilder">The <see cref="SpanBasedStringBuilder"/> that will accumulate the results.</param>
        /// <param name="regex">The <see cref="Regex"/> that will perform the matching.</param>
        public static void ReplaceAndAppend(
            string input,
            ref readonly MetadataMatchEvaluator metadataMatchEvaluator,
            SpanBasedStringBuilder stringBuilder,
            Regex regex)
            => ReplaceAndAppend(input, in metadataMatchEvaluator, count: -1, startat: regex.RightToLeft ? input.Length : 0, stringBuilder, regex);

        /// <summary>
        /// Copied from <see cref="Regex.Replace(string, MatchEvaluator, int, int)"/> and modified to use a <see cref="SpanBasedStringBuilder"/> rather than repeatedly allocating a <see cref="System.Text.StringBuilder"/>. This
        /// allows us to avoid intermediate string allocations when repeatedly doing replacements.
        /// </summary>
        /// <param name="input">The string to operate on.</param>
        /// <param name="matchEvaluator">Evaluator used in the transform function.</param>
        /// <param name="count">The number of replacements.</param>
        /// <param name="startat">Index to start when doing replacements.</param>
        /// <param name="stringBuilder">The <see cref="SpanBasedStringBuilder"/> that will accumulate the results.</param>
        /// <param name="regex">The <see cref="Regex"/> that will perform the matching.</param>
        public static void ReplaceAndAppend(
            string input,
            ref readonly MetadataMatchEvaluator matchEvaluator,
            int count,
            int startat,
            SpanBasedStringBuilder stringBuilder,
            Regex regex)
        {
            ArgumentNullException.ThrowIfNull(stringBuilder);
            ArgumentOutOfRangeException.ThrowIfLessThan(count, -1);
            ArgumentOutOfRangeException.ThrowIfNegative(startat);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(startat, input.Length);
            ArgumentNullException.ThrowIfNull(regex);

            if (count == 0)
            {
                stringBuilder.Append(input);
                return;
            }

            Match match = regex.Match(input, startat);

            if (!match.Success)
            {
                stringBuilder.Append(input);
                return;
            }

            if (!regex.RightToLeft)
            {
                int prevat = 0;
                do
                {
                    if (match.Index != prevat)
                    {
                        stringBuilder.Append(input, prevat, match.Index - prevat);
                    }

                    prevat = match.Index + match.Length;
                    stringBuilder.Append(matchEvaluator.ExpandSingleMetadata(match));
                    if (--count == 0)
                    {
                        break;
                    }

                    match = match.NextMatch();
                }
                while (match.Success);
                if (prevat < input.Length)
                {
                    stringBuilder.Append(input, prevat, input.Length - prevat);
                }
            }
            else
            {
                using var stack = new RefStack<ReadOnlyMemory<char>>();
                int prevat = input.Length;
                ReadOnlyMemory<char> memory = input.AsMemory();

                do
                {
                    if (match.Index + match.Length != prevat)
                    {
                        stack.Push(memory.Slice(match.Index + match.Length, prevat - match.Index - match.Length));
                    }

                    prevat = match.Index;
                    stack.Push(matchEvaluator.ExpandSingleMetadata(match).AsMemory());
                    if (--count == 0)
                    {
                        break;
                    }

                    match = match.NextMatch();
                }
                while (match.Success);

                if (prevat > 0)
                {
                    stringBuilder.Append(input, 0, prevat);
                }

                while (stack.TryPop(out ReadOnlyMemory<char> item))
                {
                    stringBuilder.Append(item);
                }
            }
        }
    }
}
