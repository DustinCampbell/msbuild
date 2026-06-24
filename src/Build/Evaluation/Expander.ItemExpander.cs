// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
{
    /// <summary>
    /// Expands item expressions, like @(Compile), possibly with transforms and/or separators.
    ///
    /// Item vectors are composed of a name, an optional transform, and an optional separator i.e.
    ///
    ///     @(&lt;name&gt;->'&lt;transform&gt;','&lt;separator&gt;')
    ///
    /// If a separator is not specified it defaults to a semi-colon. The transform expression is also optional, but if
    /// specified, it allows each item in the vector to have its item-spec converted to a different form. The transform
    /// expression can reference any custom metadata defined on the item, as well as the pre-defined item-spec modifiers.
    ///
    /// NOTE:
    /// 1) white space between &lt;name&gt;, &lt;transform&gt; and &lt;separator&gt; is ignored
    ///    i.e. @(&lt;name&gt;, '&lt;separator&gt;') is valid
    /// 2) the separator is not restricted to be a single character, it can be a string
    /// 3) the separator can be an empty string i.e. @(&lt;name&gt;,'')
    /// 4) specifying an empty transform is NOT the same as specifying no transform -- the former will reduce all item-specs
    ///    to empty strings
    ///
    /// if @(files) is a vector for the files a.txt and b.txt, then:
    ///
    ///     "my list: @(files)"                                 expands to string     "my list: a.txt;b.txt"
    ///
    ///     "my list: @(files,' ')"                             expands to string      "my list: a.txt b.txt"
    ///
    ///     "my list: @(files, '')"                             expands to string      "my list: a.txtb.txt"
    ///
    ///     "my list: @(files, '; ')"                           expands to string      "my list: a.txt; b.txt"
    ///
    ///     "my list: @(files->'%(Filename)')"                  expands to string      "my list: a;b"
    ///
    ///     "my list: @(files -> 'temp\%(Filename).xml', ' ')   expands to string      "my list: temp\a.xml temp\b.xml"
    ///
    ///     "my list: @(files->'')                              expands to string      "my list: ;".
    /// </summary>
    /// <remarks>
    /// This is a private nested class, exposed only through the Expander class.
    /// That allows it to hide its private methods even from Expander.
    /// </remarks>
    private static partial class ItemExpander
    {
        private static readonly FrozenDictionary<string, TransformKind> s_intrinsicTransforms = new Dictionary<string, TransformKind>(StringComparer.OrdinalIgnoreCase)
        {
            { "Count", TransformKind.Count },
            { "Exists", TransformKind.Exists },
            { "Combine", TransformKind.Combine },
            { "GetPathsOfAllDirectoriesAbove", TransformKind.GetPathsOfAllDirectoriesAbove },
            { "DirectoryName", TransformKind.DirectoryName },
            { "Metadata", TransformKind.Metadata },
            { "DistinctWithCase", TransformKind.DistinctWithCase },
            { "Distinct", TransformKind.Distinct },
            { "Reverse", TransformKind.Reverse },
            { "ExpandQuotedExpressionFunction", TransformKind.ExpandQuotedExpressionFunction },
            { "ExecuteStringFunction", TransformKind.ExecuteStringFunction },
            { "ClearMetadata", TransformKind.ClearMetadata },
            { "HasMetadata", TransformKind.HasMetadata },
            { "WithMetadataValue", TransformKind.WithMetadataValue },
            { "WithoutMetadataValue", TransformKind.WithoutMetadataValue },
            { "AnyHaveMetadataValue", TransformKind.AnyHaveMetadataValue },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///  Executes the list of transform functions.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Each captured transform function will be mapped to either a static method on
        ///   <see cref="Transforms"/> or a known item spec modifier which operates on the item path.
        ///  </para>
        ///  <para>
        ///   For each function, the full list of items will be iteratively transformed using the
        ///   output of the previous. E.g. given functions f, g, h, the order of operations will
        ///   look like: <c>results = h(g(f(items)))</c>.
        ///  </para>
        ///  <para>
        ///   If no function name is found, we default to
        ///   <see cref="Transforms.ExpandQuotedExpressionFunction"/>.
        ///  </para>
        /// </remarks>
        /// <returns>
        ///  The list of <see cref="TransformEntry"/> values produced by applying every transform in
        ///  <paramref name="transforms"/> in sequence.
        /// </returns>
        private static List<TransformEntry> TransformItems(
            ICollection<I> items,
            List<ItemTransform> transforms,
            Expander<P, I> expander,
            IElementLocation elementLocation,
            bool includeNullEntries)
        {
            // Each transform runs on the full set of transformed items from the previous result.
            // We can reuse our buffers by just swapping the references after each transform.
            List<TransformEntry> input = CreateEntries(items);
            List<TransformEntry> output = new(items.Count);

            // Create a TransformFunction for each transform in the chain by extracting the relevant information
            // from the regex parsing results
            for (int i = 0; i < transforms.Count; i++)
            {
                ItemTransform transform = transforms[i];
                string function = transform.Text;
                string functionName = transform.FunctionName;
                string argumentsExpression = transform.FunctionArguments;

                string[] arguments = null;

                if (functionName == null)
                {
                    functionName = "ExpandQuotedExpressionFunction";
                    arguments = [function];
                }
                else if (argumentsExpression != null)
                {
                    arguments = ExtractFunctionArguments(elementLocation, argumentsExpression, argumentsExpression.AsMemory());
                }

                TransformKind kind;

                if (ItemSpecModifiers.IsDerivableItemSpecModifier(functionName))
                {
                    kind = TransformKind.ItemSpecModifierFunction;
                }
                else if (!s_intrinsicTransforms.TryGetValue(functionName, out kind))
                {
                    kind = TransformKind.ExecuteStringFunction;
                }

                switch (kind)
                {
                    case TransformKind.ItemSpecModifierFunction:
                        Transforms.ItemSpecModifierFunction(input, output, arguments, includeNullEntries, functionName, elementLocation);
                        break;
                    case TransformKind.Count:
                        Transforms.Count(input, output);
                        break;
                    case TransformKind.Exists:
                        Transforms.Exists(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.Combine:
                        Transforms.Combine(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.GetPathsOfAllDirectoriesAbove:
                        Transforms.GetPathsOfAllDirectoriesAbove(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.DirectoryName:
                        Transforms.DirectoryName(input, output, arguments, includeNullEntries, functionName, elementLocation);
                        break;
                    case TransformKind.Metadata:
                        Transforms.Metadata(input, output, arguments, includeNullEntries, functionName, elementLocation);
                        break;
                    case TransformKind.DistinctWithCase:
                        Transforms.DistinctWithCase(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.Distinct:
                        Transforms.Distinct(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.Reverse:
                        Transforms.Reverse(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.ExpandQuotedExpressionFunction:
                        Transforms.ExpandQuotedExpressionFunction(input, output, arguments, includeNullEntries, functionName, elementLocation);
                        break;
                    case TransformKind.ExecuteStringFunction:
                        Transforms.ExecuteStringFunction(expander, input, output, arguments, includeNullEntries, functionName, elementLocation);
                        break;
                    case TransformKind.ClearMetadata:
                        Transforms.ClearMetadata(input, output, arguments, includeNullEntries, functionName, elementLocation);
                        break;
                    case TransformKind.HasMetadata:
                        Transforms.HasMetadata(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.WithMetadataValue:
                        Transforms.WithMetadataValue(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.WithoutMetadataValue:
                        Transforms.WithoutMetadataValue(input, output, arguments, functionName, elementLocation);
                        break;
                    case TransformKind.AnyHaveMetadataValue:
                        Transforms.AnyHaveMetadataValue(input, output, arguments, functionName, elementLocation);
                        break;
                    default:
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnknownItemFunction", functionName);
                        break;
                }

                // If we have another transform, swap the source and transform lists.
                if (i < transforms.Count - 1)
                {
                    (output, input) = (input, output);
                    output.Clear();
                }
            }

            return output;
        }

        /// <summary>
        ///  Creates transform entries from the given items, pairing each with its evaluated include.
        /// </summary>
        private static List<TransformEntry> CreateEntries(ICollection<I> items)
        {
            List<TransformEntry> entries = new(items.Count);

            foreach (I item in items)
            {
                if (Traits.Instance.UseLazyWildCardEvaluation)
                {
                    foreach (var resultantItem in
                        EngineFileUtilities.GetFileListEscaped(
                            item.ProjectDirectory,
                            item.EvaluatedIncludeEscaped,
                            forceEvaluate: true))
                    {
                        entries.Add(new TransformEntry(resultantItem, item));
                    }
                }
                else
                {
                    entries.Add(new TransformEntry(item.EvaluatedIncludeEscaped, item));
                }
            }

            return entries;
        }

        /// <summary>
        /// Expands any item vector in the expression into items.
        ///
        /// For example, expands @(Compile->'%(foo)') to a set of items derived from the items in the "Compile" list.
        ///
        /// If there is no item vector in the expression (for example a literal "foo.cpp"), returns null.
        /// If the item vector expression expands to no items, returns an empty list.
        /// If item expansion is not allowed by the provided options, returns null.
        /// If there is an item vector but concatenated with something else, throws InvalidProjectFileException.
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        ///
        /// If the expression is a transform, any transformations to an expression that evaluates to nothing (i.e., because
        /// an item has no value for a piece of metadata) are optionally indicated with a null entry in the list. This means
        /// that the length of the returned list is always the same as the length of the referenced item list in the input string.
        /// That's important for any correlation the caller wants to do.
        ///
        /// If expression was a transform, 'isTransformExpression' is true, otherwise false.
        ///
        /// Item type of the items returned is determined by the IItemFactory passed in; if the IItemFactory does not
        /// have an item type set on it, it will be given the item type of the item vector to use.
        /// </summary>
        /// <typeparam name="T">Type of the items that should be returned.</typeparam>
        internal static IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(
            Expander<P, I> expander, string expression, IItemProvider<I> items, IItemFactory<I, T> itemFactory, ExpanderOptions options,
            bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where T : class, IItem
        {
            isTransformExpression = false;

            if (!ExpressionShredder.TryGetItemVectorExpression(expression, elementLocation, out ItemVectorExpression itemVector))
            {
                return null;
            }

            return ExpandExpressionCaptureIntoItems(itemVector, expander, items, itemFactory, options, includeNullEntries,
                out isTransformExpression, elementLocation);
        }

        internal static IList<T> ExpandExpressionCaptureIntoItems<T>(
            ItemVectorExpression itemVector, Expander<P, I> expander, IItemProvider<I> items, IItemFactory<I, T> itemFactory,
            ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where T : class, IItem
        {
            Assumed.NotNull(items, "Cannot expand items without providing items");
            isTransformExpression = false;

            // If the incoming factory doesn't have an item type that it can use to
            // create items, it's our indication that the caller wants its items to have the type of the
            // expression being expanded. For example, items from expanding "@(Compile") should
            // have the item type "Compile".
            if (itemFactory.ItemType == null)
            {
                itemFactory.ItemType = itemVector.ItemType;
            }

            IList<T> result;

            if (itemVector.HasSeparator)
            {
                // Reference contains a separator, for example @(Compile, ';').
                // We need to flatten the list into
                // a scalar and then create a single item. Basically we need this
                // to be able to convert item lists with user specified separators into properties.
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

                if (!TryAppendItemVector(itemVector, items, builder, options, expander, elementLocation))
                {
                    return null;
                }

                string expandedItemVector = builder.ToString();

                result = Array.Empty<T>();

                if (expandedItemVector.Length > 0)
                {
                    T newItem = itemFactory.CreateItem(expandedItemVector, elementLocation.File);

                    result = [newItem];
                }

                return result;
            }

            var expandItemVectorResult = ExpandItemVector(itemVector, items, expander, elementLocation, options, includeNullEntries: true);

            isTransformExpression = expandItemVectorResult.HasTransforms;

            if (expandItemVectorResult.StoppedEarly)
            {
                return null;
            }

            var entries = expandItemVectorResult.Entries;

            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<T>();
            }

            result = new List<T>(entries.Count);

            foreach (var (itemSpec, originalItem) in entries)
            {
                if (itemSpec != null && originalItem == null)
                {
                    // We have an itemspec, but no base item
                    result.Add(itemFactory.CreateItem(itemSpec, elementLocation.File));
                }
                else if (itemSpec != null && originalItem != null)
                {
                    result.Add(itemSpec.Equals(originalItem.EvaluatedIncludeEscaped)
                        ? itemFactory.CreateItem(originalItem, elementLocation.File) // itemspec came from direct item reference, no transforms
                        : itemFactory.CreateItem(itemSpec, originalItem, elementLocation.File)); // itemspec came from a transform and is different from its original item
                }
                else if (includeNullEntries)
                {
                    // The itemspec is null and the base item doesn't matter
                    result.Add(null);
                }
            }

            return result;
        }

        /// <summary>
        ///  The result of expanding an item vector expression via <see cref="ExpandItemVector"/>.
        /// </summary>
        internal readonly struct ExpandItemVectorResult
        {
            /// <summary>
            ///  The expanded items, or <see langword="null"/> if there were no items to expand
            ///  (or if expansion <see cref="StoppedEarly"/>).
            /// </summary>
            /// <remarks>
            ///  <see cref="TransformEntry.Value"/> is the item string, escaped, and
            ///  <see cref="TransformEntry.Item"/> is the original item. The value differs from the item's
            ///  string when it is produced by a transform.
            /// </remarks>
            public List<TransformEntry> Entries { get; }

            /// <summary>
            ///  <see langword="true"/> if <see cref="ExpanderOptions.BreakOnNotEmpty"/> was passed and the
            ///  expression was going to be non-empty, so expansion broke out early. When <see langword="true"/>,
            ///  <see cref="Entries"/> must not be relied upon.
            /// </summary>
            public bool StoppedEarly { get; }

            /// <summary>
            ///  <see langword="true"/> if the expression was a transform (for example <c>@(Foo-&gt;'%(Bar)')</c>)
            ///  rather than a plain item reference such as <c>@(Foo)</c>.
            /// </summary>
            public bool HasTransforms { get; }

            public ExpandItemVectorResult(List<TransformEntry> entries, bool stoppedEarly, bool hasTransforms)
            {
                Entries = entries;
                StoppedEarly = stoppedEarly;
                HasTransforms = hasTransforms;
            }
        }

        /// <summary>
        ///  Expands an item vector expression into a list of items.
        ///  If the expression uses a separator, then all the items are concatenated into one string using that separator.
        /// </summary>
        /// <param name="itemVector">The <see cref="ItemVectorExpression"/> representing the structure of an item expression.</param>
        /// <param name="itemProvider">
        ///  <see cref="IItemProvider{T}"/> to provide the initial items (which may get subsequently transformed,
        ///  if <paramref name="itemVector"/> is a transform expression).
        /// </param>
        /// <param name="expander">The expander whose state will be used to expand any transforms.</param>
        /// <param name="elementLocation">Location of the xml element containing the <paramref name="itemVector"/>.</param>
        /// <param name="options">expander options.</param>
        /// <param name="includeNullEntries">Whether to include items that evaluated to empty / null.</param>
        /// <returns>
        ///  An <see cref="ExpandItemVectorResult"/> describing the expanded items. If
        ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> was passed and the expression was going to be non-empty,
        ///  expansion breaks out early and <see cref="ExpandItemVectorResult.StoppedEarly"/> is <see langword="true"/>.
        /// </returns>
        internal static ExpandItemVectorResult ExpandItemVector(
            ItemVectorExpression itemVector,
            IItemProvider<I> itemProvider,
            Expander<P, I> expander,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries)
        {
            Assumed.NotNull(itemProvider, "Cannot expand items without providing items");

            // There's something wrong with the expression, and we ended up with a blank item type
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(itemVector.ItemType), elementLocation, "InvalidFunctionPropertyExpression");

            ICollection<I> items = itemProvider.GetItems(itemVector.ItemType);

            // If there are no items of the given type, then bail out early
            if (items.Count == 0)
            {
                // ...but only if there isn't a "Count" function (which returns 0 for an empty list)
                // or an "AnyHaveMetadataValue" function (which returns false for an empty list).
                bool hasEmptyListFunction = false;
                if (itemVector.HasTransforms)
                {
                    foreach (ItemTransform transform in itemVector.Transforms)
                    {
                        if (transform.FunctionName is string functionName &&
                            (functionName.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                             functionName.Equals("AnyHaveMetadataValue", StringComparison.OrdinalIgnoreCase)))
                        {
                            hasEmptyListFunction = true;
                            break;
                        }
                    }
                }

                if (!hasEmptyListFunction)
                {
                    return default;
                }
            }

            List<TransformEntry> entries = null;

            if (!itemVector.HasTransforms)
            {
                // No transform: expression is like @(Compile), so include the item spec without a transform base item
                foreach (I item in items)
                {
                    string evaluatedIncludeEscaped = item.EvaluatedIncludeEscaped;
                    if ((evaluatedIncludeEscaped.Length > 0) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return new(entries: null, stoppedEarly: true, hasTransforms: false);
                    }

                    entries ??= new List<TransformEntry>(items.Count);
                    entries.Add(new TransformEntry(evaluatedIncludeEscaped, item));
                }
            }
            else
            {
                // There's something wrong with the expression, and we ended up with no function names
                ProjectErrorUtilities.VerifyThrowInvalidProject(itemVector.Transforms.Count > 0, elementLocation, "InvalidFunctionPropertyExpression");

                entries = TransformItems(items, itemVector.Transforms, expander, elementLocation, includeNullEntries);

                // Check for break on non-empty only after ALL transforms are complete
                if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                {
                    foreach (TransformEntry entry in entries)
                    {
                        if (!entry.Value.IsNullOrEmpty())
                        {
                            return new(entries, stoppedEarly: true, hasTransforms: true);
                        }
                    }
                }
            }

            if (itemVector.HasSeparator)
            {
                var joinedItems = string.Join(itemVector.Separator, entries.Select(i => i.Value));
                entries.Clear();
                entries.Add(new TransformEntry(value: joinedItems, item: null));
            }

            return new(entries, stoppedEarly: false, itemVector.HasTransforms);
        }

        /// <summary>
        /// Expands all item vectors embedded in the given expression into a single string.
        /// If the expression is empty, returns empty string.
        /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
        /// </summary>
        internal static string ExpandItemVectorsIntoString(Expander<P, I> expander, string expression, IItemProvider<I> items, ExpanderOptions options, IElementLocation elementLocation)
        {
            if ((options & ExpanderOptions.ExpandItems) == 0 || expression.Length == 0)
            {
                return expression;
            }

            Assumed.NotNull(items, "Cannot expand items without providing items");

            ExpressionShredder.ReferencedItemExpressionsEnumerator matchesEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

            if (!matchesEnumerator.MoveNext())
            {
                return expression;
            }

            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

            // As we walk through the matches, we need to copy out the original parts of the string which
            // are not covered by the match.  This preserves original behavior which did not trim whitespace
            // from between separators.
            int startIndex = 0;
            do
            {
                ItemVectorExpression itemVector = matchesEnumerator.Current;

                if (itemVector.Index > startIndex)
                {
                    if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return null;
                    }

                    builder.Append(expression, startIndex, itemVector.Index - startIndex);
                }

                if (!TryAppendItemVector(itemVector, items, builder, options, expander, elementLocation))
                {
                    return null;
                }

                startIndex = itemVector.Index + itemVector.Length;
            }
            while (matchesEnumerator.MoveNext());

            builder.Append(expression, startIndex, expression.Length - startIndex);

            return builder.ToString();
        }

        /// <summary>
        ///  Expands the provided item vector expression and appends the result to <paramref name="builder"/>.
        /// </summary>
        /// <param name="itemVector">The <see cref="ItemVectorExpression"/> representing the structure of an item expression.</param>
        /// <param name="itemProvider">
        ///  <see cref="IItemProvider{T}"/> to provide the initial items (which may get subsequently transformed, if
        ///  <paramref name="itemVector"/> is a transform expression).
        /// </param>
        /// <param name="builder">The builder to append the expanded item vector to.</param>
        /// <param name="options">expander options.</param>
        /// <param name="expander">The expander whose state will be used to expand any transforms.</param>
        /// <param name="elementLocation">Location of the xml element containing the <paramref name="itemVector"/>.</param>
        /// <returns>
        ///  <see langword="false"/> if <see cref="ExpanderOptions.BreakOnNotEmpty"/> was passed and the expression
        ///  was going to be non-empty, so expansion broke out early; otherwise <see langword="true"/>.
        /// </returns>
        private static bool TryAppendItemVector(
            ItemVectorExpression itemVector,
            IItemProvider<I> itemProvider,
            SpanBasedStringBuilder builder,
            ExpanderOptions options,
            Expander<P, I> expander,
            IElementLocation elementLocation)
        {
            var expandItemVectorResult = ExpandItemVector(itemVector, itemProvider, expander, elementLocation, options, includeNullEntries: true);
            if (expandItemVectorResult.StoppedEarly)
            {
                return false;
            }

            List<TransformEntry> entries = expandItemVectorResult.Entries;

            if (entries == null)
            {
                // No items to expand.
                return true;
            }

            int startLength = builder.Length;
            bool truncate = IsTruncationEnabled(options);

            // if the Separator is not null, then ExpandItemVector would have joined the items using that separator itself
            for (int i = 0; i < entries.Count; i++)
            {
                var (value, _) = entries[i];

                if (truncate)
                {
                    if (i >= ItemLimitPerExpansion)
                    {
                        builder.Append("...");
                        return true;
                    }

                    int currentLength = builder.Length - startLength;
                    if (!value.IsNullOrEmpty() && currentLength + value.Length > CharacterLimitPerExpansion)
                    {
                        int truncateIndex = CharacterLimitPerExpansion - currentLength - 3;
                        if (truncateIndex > 0)
                        {
                            builder.Append(value, 0, truncateIndex);
                        }

                        builder.Append("...");
                        return true;
                    }
                }

                builder.Append(value);

                if (i < entries.Count - 1)
                {
                    builder.Append(";");
                }
            }

            return true;
        }
    }
}
