// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
#if NET
using System.IO;
#else
using Microsoft.IO;
#endif
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.NET.StringTools;
using ItemSpecModifiers = Microsoft.Build.Shared.FileUtilities.ItemSpecModifiers;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal partial class Expander<P, I>
    where P : class, IProperty
    where I : class, IItem
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
    private static class ItemExpander
    {
        private static readonly FrozenDictionary<string, ItemTransformFunctions> s_intrinsicItemFunctions = new Dictionary<string, ItemTransformFunctions>(StringComparer.OrdinalIgnoreCase)
        {
            { "Count", ItemTransformFunctions.Count },
            { "Exists", ItemTransformFunctions.Exists },
            { "Combine", ItemTransformFunctions.Combine },
            { "GetPathsOfAllDirectoriesAbove", ItemTransformFunctions.GetPathsOfAllDirectoriesAbove },
            { "DirectoryName", ItemTransformFunctions.DirectoryName },
            { "Metadata", ItemTransformFunctions.Metadata },
            { "DistinctWithCase", ItemTransformFunctions.DistinctWithCase },
            { "Distinct", ItemTransformFunctions.Distinct },
            { "Reverse", ItemTransformFunctions.Reverse },
            { "ExpandQuotedExpressionFunction", ItemTransformFunctions.ExpandQuotedExpressionFunction },
            { "ExecuteStringFunction", ItemTransformFunctions.ExecuteStringFunction },
            { "ClearMetadata", ItemTransformFunctions.ClearMetadata },
            { "HasMetadata", ItemTransformFunctions.HasMetadata },
            { "WithMetadataValue", ItemTransformFunctions.WithMetadataValue },
            { "WithoutMetadataValue", ItemTransformFunctions.WithoutMetadataValue },
            { "AnyHaveMetadataValue", ItemTransformFunctions.AnyHaveMetadataValue },
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        private enum ItemTransformFunctions
        {
            ItemSpecModifierFunction,
            Count,
            Exists,
            Combine,
            GetPathsOfAllDirectoriesAbove,
            DirectoryName,
            Metadata,
            DistinctWithCase,
            Distinct,
            Reverse,
            ExpandQuotedExpressionFunction,
            ExecuteStringFunction,
            ClearMetadata,
            HasMetadata,
            WithMetadataValue,
            WithoutMetadataValue,
            AnyHaveMetadataValue,
        }

        /// <summary>
        /// Execute the list of transform functions.
        /// </summary>
        /// <remarks>
        /// Each captured transform function will be mapped to to a either static method on
        /// <see cref="IntrinsicItemFunctions{S}"/> or a known item spec modifier which operates on the item path.
        ///
        /// For each function, the full list of items will be iteratvely tranformed using the output of the previous.
        ///
        /// E.g. given functions f, g, h, the order of operations will look like:
        /// results = h(g(f(items)))
        ///
        /// If no function name is found, we default to <see cref="IntrinsicItemFunctions{S}.ExpandQuotedExpressionFunction"/>.
        /// </remarks>
        /// <typeparam name="S">class, IItem.</typeparam>
        internal static List<KeyValuePair<string, S>> Transform<S>(
                Expander<P, I> expander,
                IElementLocation elementLocation,
                ExpanderOptions options,
                bool includeNullEntries,
                List<ExpressionShredder.ItemExpressionCapture> captures,
                ICollection<S> itemsOfType,
                out bool brokeEarly)
            where S : class, IItem
        {
            // Each transform runs on the full set of transformed items from the previous result.
            // We can reuse our buffers by just swapping the references after each transform.
            List<KeyValuePair<string, S>> sourceItems = IntrinsicItemFunctions<S>.GetItemPairs(itemsOfType);
            List<KeyValuePair<string, S>> transformedItems = new(itemsOfType.Count);

            // Create a TransformFunction for each transform in the chain by extracting the relevant information
            // from the regex parsing results
            for (int i = 0; i < captures.Count; i++)
            {
                ExpressionShredder.ItemExpressionCapture capture = captures[i];
                string function = capture.Value;
                string functionName = capture.FunctionName;
                string argumentsExpression = capture.FunctionArguments;

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

                ItemTransformFunctions functionType;

                if (ItemSpecModifiers.IsDerivableItemSpecModifier(functionName))
                {
                    functionType = ItemTransformFunctions.ItemSpecModifierFunction;
                }
                else if (!s_intrinsicItemFunctions.TryGetValue(functionName, out functionType))
                {
                    functionType = ItemTransformFunctions.ExecuteStringFunction;
                }

                switch (functionType)
                {
                    case ItemTransformFunctions.ItemSpecModifierFunction:
                        IntrinsicItemFunctions<S>.ItemSpecModifierFunction(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Count:
                        IntrinsicItemFunctions<S>.Count(sourceItems, transformedItems);
                        break;
                    case ItemTransformFunctions.Exists:
                        IntrinsicItemFunctions<S>.Exists(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Combine:
                        IntrinsicItemFunctions<S>.Combine(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.GetPathsOfAllDirectoriesAbove:
                        IntrinsicItemFunctions<S>.GetPathsOfAllDirectoriesAbove(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.DirectoryName:
                        IntrinsicItemFunctions<S>.DirectoryName(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Metadata:
                        IntrinsicItemFunctions<S>.Metadata(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.DistinctWithCase:
                        IntrinsicItemFunctions<S>.DistinctWithCase(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Distinct:
                        IntrinsicItemFunctions<S>.Distinct(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.Reverse:
                        IntrinsicItemFunctions<S>.Reverse(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.ExpandQuotedExpressionFunction:
                        IntrinsicItemFunctions<S>.ExpandQuotedExpressionFunction(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.ExecuteStringFunction:
                        IntrinsicItemFunctions<S>.ExecuteStringFunction(expander, elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.ClearMetadata:
                        IntrinsicItemFunctions<S>.ClearMetadata(elementLocation, includeNullEntries, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.HasMetadata:
                        IntrinsicItemFunctions<S>.HasMetadata(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.WithMetadataValue:
                        IntrinsicItemFunctions<S>.WithMetadataValue(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.WithoutMetadataValue:
                        IntrinsicItemFunctions<S>.WithoutMetadataValue(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    case ItemTransformFunctions.AnyHaveMetadataValue:
                        IntrinsicItemFunctions<S>.AnyHaveMetadataValue(elementLocation, functionName, sourceItems, arguments, transformedItems);
                        break;
                    default:
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnknownItemFunction", functionName);
                        break;
                }

                foreach (KeyValuePair<string, S> itemTuple in transformedItems)
                {
                    if (!string.IsNullOrEmpty(itemTuple.Key) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        brokeEarly = true;
                        return transformedItems; // break out early
                    }
                }

                // If we have another transform, swap the source and transform lists.
                if (i < captures.Count - 1)
                {
                    (transformedItems, sourceItems) = (sourceItems, transformedItems);
                    transformedItems.Clear();
                }
            }

            brokeEarly = false;
            return transformedItems;
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

            var expressionCapture = ExpandSingleItemVectorExpressionIntoExpressionCapture(expression, options, elementLocation);
            if (expressionCapture == null)
            {
                return null;
            }

            return ExpandExpressionCaptureIntoItems(expressionCapture.Value, expander, items, itemFactory, options, includeNullEntries,
                out isTransformExpression, elementLocation);
        }

        internal static ExpressionShredder.ItemExpressionCapture? ExpandSingleItemVectorExpressionIntoExpressionCapture(
                string expression, ExpanderOptions options, IElementLocation elementLocation)
        {
            if (((options & ExpanderOptions.ExpandItems) == 0) || (expression.Length == 0))
            {
                return null;
            }

            if (!expression.Contains('@'))
            {
                return null;
            }

            ExpressionShredder.ReferencedItemExpressionsEnumerator matchesEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

            if (!matchesEnumerator.MoveNext())
            {
                return null;
            }

            ExpressionShredder.ItemExpressionCapture match = matchesEnumerator.Current;

            // We have a single valid @(itemlist) reference in the given expression.
            // If the passed-in expression contains exactly one item list reference,
            // with nothing else concatenated to the beginning or end, then proceed
            // with itemizing it, otherwise error.
            ProjectErrorUtilities.VerifyThrowInvalidProject(match.Value == expression, elementLocation, "EmbeddedItemVectorCannotBeItemized", expression);
            ErrorUtilities.VerifyThrow(!matchesEnumerator.MoveNext(), "Expected just one item vector");

            return match;
        }

        internal static IList<T> ExpandExpressionCaptureIntoItems<T>(
            ExpressionShredder.ItemExpressionCapture expressionCapture, Expander<P, I> expander, IItemProvider<I> items, IItemFactory<I, T> itemFactory,
            ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where T : class, IItem
        {
            ErrorUtilities.VerifyThrow(items != null, "Cannot expand items without providing items");
            isTransformExpression = false;
            bool brokeEarlyNonEmpty;

            // If the incoming factory doesn't have an item type that it can use to
            // create items, it's our indication that the caller wants its items to have the type of the
            // expression being expanded. For example, items from expanding "@(Compile") should
            // have the item type "Compile".
            if (itemFactory.ItemType == null)
            {
                itemFactory.ItemType = expressionCapture.ItemType;
            }


            IList<T> result;
            if (expressionCapture.Separator != null)
            {
                // Reference contains a separator, for example @(Compile, ';').
                // We need to flatten the list into
                // a scalar and then create a single item. Basically we need this
                // to be able to convert item lists with user specified separators into properties.
                string expandedItemVector;
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
                brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(expander, expressionCapture, items, elementLocation, builder, options);

                if (brokeEarlyNonEmpty)
                {
                    return null;
                }

                expandedItemVector = builder.ToString();

                result = Array.Empty<T>();

                if (expandedItemVector.Length > 0)
                {
                    T newItem = itemFactory.CreateItem(expandedItemVector, elementLocation.File);

                    result = [newItem];
                }

                return result;
            }

            List<KeyValuePair<string, I>> itemsFromCapture;
            brokeEarlyNonEmpty = ExpandExpressionCapture(expander, expressionCapture, items, elementLocation, options, includeNullEntries: true, out isTransformExpression, out itemsFromCapture);

            if (brokeEarlyNonEmpty)
            {
                return null;
            }

            if (itemsFromCapture == null || itemsFromCapture.Count == 0)
            {
                return Array.Empty<T>();
            }

            result = new List<T>(itemsFromCapture.Count);

            foreach (var itemTuple in itemsFromCapture)
            {
                var itemSpec = itemTuple.Key;
                var originalItem = itemTuple.Value;

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
        /// Expands an expression capture into a list of items
        /// If the capture uses a separator, then all the items are concatenated into one string using that separator.
        ///
        /// Returns true if ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and so it broke out early.
        /// </summary>
        /// <param name="isTransformExpression"></param>
        /// <param name="itemsFromCapture">
        /// List of items.
        ///
        /// Item1 represents the item string, escaped
        /// Item2 represents the original item.
        ///
        /// Item1 differs from Item2's string when it is coming from a transform.
        ///
        /// </param>
        /// <param name="expander">The expander whose state will be used to expand any transforms.</param>
        /// <param name="expressionCapture">The <see cref="ExpandSingleItemVectorExpressionIntoExpressionCapture"/> representing the structure of an item expression.</param>
        /// <param name="evaluatedItems"><see cref="IItemProvider{T}"/> to provide the inital items (which may get subsequently transformed, if <paramref name="expressionCapture"/> is a transform expression)>.</param>
        /// <param name="elementLocation">Location of the xml element containing the <paramref name="expressionCapture"/>.</param>
        /// <param name="options">expander options.</param>
        /// <param name="includeNullEntries">Wether to include items that evaluated to empty / null.</param>
        internal static bool ExpandExpressionCapture<S>(
            Expander<P, I> expander,
            ExpressionShredder.ItemExpressionCapture expressionCapture,
            IItemProvider<S> evaluatedItems,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            out List<KeyValuePair<string, S>> itemsFromCapture)
            where S : class, IItem
        {
            ErrorUtilities.VerifyThrow(evaluatedItems != null, "Cannot expand items without providing items");
            // There's something wrong with the expression, and we ended up with a blank item type
            ProjectErrorUtilities.VerifyThrowInvalidProject(!string.IsNullOrEmpty(expressionCapture.ItemType), elementLocation, "InvalidFunctionPropertyExpression");

            isTransformExpression = false;

            ICollection<S> itemsOfType = evaluatedItems.GetItems(expressionCapture.ItemType);
            List<ExpressionShredder.ItemExpressionCapture> captures = expressionCapture.Captures;

            // If there are no items of the given type, then bail out early
            if (itemsOfType.Count == 0)
            {
                // ... but only if there isn't a function "Count", since that will want to return something (zero) for an empty list
                if (captures?.Any(capture => string.Equals(capture.FunctionName, "Count", StringComparison.OrdinalIgnoreCase)) != true)
                {
                    // ...or a function "AnyHaveMetadataValue", since that will want to return false for an empty list.
                    if (captures?.Any(capture => string.Equals(capture.FunctionName, "AnyHaveMetadataValue", StringComparison.OrdinalIgnoreCase)) != true)
                    {
                        itemsFromCapture = null;
                        return false;
                    }
                }
            }

            if (captures != null)
            {
                isTransformExpression = true;
            }

            if (!isTransformExpression)
            {
                itemsFromCapture = null;

                // No transform: expression is like @(Compile), so include the item spec without a transform base item
                foreach (S item in itemsOfType)
                {
                    string evaluatedIncludeEscaped = item.EvaluatedIncludeEscaped;
                    if ((evaluatedIncludeEscaped.Length > 0) && (options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return true;
                    }

                    itemsFromCapture ??= new List<KeyValuePair<string, S>>(itemsOfType.Count);
                    itemsFromCapture.Add(new KeyValuePair<string, S>(evaluatedIncludeEscaped, item));
                }
            }
            else
            {
                // There's something wrong with the expression, and we ended up with no function names
                ProjectErrorUtilities.VerifyThrowInvalidProject(captures.Count > 0, elementLocation, "InvalidFunctionPropertyExpression");

                itemsFromCapture = Transform(expander, elementLocation, options, includeNullEntries, captures, itemsOfType, out bool brokeEarly);

                if (brokeEarly)
                {
                    return true;
                }
            }

            if (expressionCapture.Separator != null)
            {
                var joinedItems = string.Join(expressionCapture.Separator, itemsFromCapture.Select(i => i.Key));
                itemsFromCapture.Clear();
                itemsFromCapture.Add(new KeyValuePair<string, S>(joinedItems, null));
            }

            return false; // did not break early
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

            ErrorUtilities.VerifyThrow(items != null, "Cannot expand items without providing items");

            ExpressionShredder.ReferencedItemExpressionsEnumerator matchesEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

            if (!matchesEnumerator.MoveNext())
            {
                return expression;
            }

            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

            // As we walk through the matches, we need to copy out the original parts of the string which
            // are not covered by the match.  This preserves original behavior which did not trim whitespace
            // from between separators.
            int lastStringIndex = 0;
            do
            {
                ExpressionShredder.ItemExpressionCapture currentItem = matchesEnumerator.Current;
                if (currentItem.Index > lastStringIndex)
                {
                    if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return null;
                    }

                    builder.Append(expression, lastStringIndex, currentItem.Index - lastStringIndex);
                }

                bool brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(expander, currentItem, items, elementLocation, builder, options);

                if (brokeEarlyNonEmpty)
                {
                    return null;
                }

                lastStringIndex = currentItem.Index + currentItem.Length;
            }
            while (matchesEnumerator.MoveNext());

            builder.Append(expression, lastStringIndex, expression.Length - lastStringIndex);

            return builder.ToString();
        }

        /// <summary>
        /// Expand the match provided into a string, and append that to the provided InternableString.
        /// Returns true if ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and so it broke out early.
        /// </summary>
        /// <typeparam name="S">Type of source items.</typeparam>
        private static bool ExpandExpressionCaptureIntoStringBuilder<S>(
            Expander<P, I> expander,
            ExpressionShredder.ItemExpressionCapture capture,
            IItemProvider<S> evaluatedItems,
            IElementLocation elementLocation,
            SpanBasedStringBuilder builder,
            ExpanderOptions options)
            where S : class, IItem
        {
            List<KeyValuePair<string, S>> itemsFromCapture;
            bool throwaway;
            var brokeEarlyNonEmpty = ExpandExpressionCapture(expander, capture, evaluatedItems, elementLocation /* including null items */, options, true, out throwaway, out itemsFromCapture);

            if (brokeEarlyNonEmpty)
            {
                return true;
            }

            if (itemsFromCapture == null)
            {
                // No items to expand.
                return false;
            }

            int startLength = builder.Length;
            bool truncate = IsTruncationEnabled(options);

            // if the capture.Separator is not null, then ExpandExpressionCapture would have joined the items using that separator itself
            for (int i = 0; i < itemsFromCapture.Count; i++)
            {
                var item = itemsFromCapture[i];
                if (truncate)
                {
                    if (i >= ItemLimitPerExpansion)
                    {
                        builder.Append("...");
                        return false;
                    }
                    int currentLength = builder.Length - startLength;
                    if (!string.IsNullOrEmpty(item.Key) && currentLength + item.Key.Length > CharacterLimitPerExpansion)
                    {
                        int truncateIndex = CharacterLimitPerExpansion - currentLength - 3;
                        if (truncateIndex > 0)
                        {
                            builder.Append(item.Key, 0, truncateIndex);
                        }
                        builder.Append("...");
                        return false;
                    }
                }
                builder.Append(item.Key);
                if (i < itemsFromCapture.Count - 1)
                {
                    builder.Append(";");
                }
            }

            return false;
        }

        /// <summary>
        /// The set of functions that called during an item transformation, e.g. @(CLCompile->ContainsMetadata('MetaName', 'metaValue')).
        /// </summary>
        /// <typeparam name="S">class, IItem.</typeparam>
        internal static class IntrinsicItemFunctions<S>
            where S : class, IItem
        {
            /// <summary>
            /// The number of characters added by a quoted expression.
            /// 3 characters for
            ///  </summary>
            private const int QuotedExpressionSurroundCharCount = 3;

            /// <summary>
            /// A precomputed lookup of item spec modifiers wrapped in regex strings.
            /// This allows us to completely skip of Regex parsing when the inner string matches a known modifier.
            /// IsDerivableItemSpecModifier doesn't currently support Span lookups, so we have to manually map these.
            /// </summary>
            private static readonly FrozenDictionary<string, string> s_itemSpecModifiers = new Dictionary<string, string>()
            {
                [$"%({ItemSpecModifiers.FullPath})"] = ItemSpecModifiers.FullPath,
                [$"%({ItemSpecModifiers.RootDir})"] = ItemSpecModifiers.RootDir,
                [$"%({ItemSpecModifiers.Filename})"] = ItemSpecModifiers.Filename,
                [$"%({ItemSpecModifiers.Extension})"] = ItemSpecModifiers.Extension,
                [$"%({ItemSpecModifiers.RelativeDir})"] = ItemSpecModifiers.RelativeDir,
                [$"%({ItemSpecModifiers.Directory})"] = ItemSpecModifiers.Directory,
                [$"%({ItemSpecModifiers.RecursiveDir})"] = ItemSpecModifiers.RecursiveDir,
                [$"%({ItemSpecModifiers.Identity})"] = ItemSpecModifiers.Identity,
                [$"%({ItemSpecModifiers.ModifiedTime})"] = ItemSpecModifiers.ModifiedTime,
                [$"%({ItemSpecModifiers.CreatedTime})"] = ItemSpecModifiers.CreatedTime,
                [$"%({ItemSpecModifiers.AccessedTime})"] = ItemSpecModifiers.AccessedTime,
                [$"%({ItemSpecModifiers.DefiningProjectFullPath})"] = ItemSpecModifiers.DefiningProjectFullPath,
                [$"%({ItemSpecModifiers.DefiningProjectDirectory})"] = ItemSpecModifiers.DefiningProjectDirectory,
                [$"%({ItemSpecModifiers.DefiningProjectName})"] = ItemSpecModifiers.DefiningProjectName,
                [$"%({ItemSpecModifiers.DefiningProjectExtension})"] = ItemSpecModifiers.DefiningProjectExtension,
            }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// A thread-static string builder for use in ExpandQuotedExpressionFunction.
            /// In theory we should be able to use shared instance, but in a profile it appears something higher in
            /// the call-stack is already borrowing the instance, so it ends up always allocating.
            /// This should not be used outside of ExpandQuotedExpressionFunction unless validated to not conflict.
            /// </summary>
            [ThreadStatic]
            private static SpanBasedStringBuilder s_includeBuilder;

            /// <summary>
            /// A reference to the last extracted expression function to save on Regex-related allocations.
            /// In many cases, the expression is exactly the same as the previous.
            /// </summary>
            private static string s_lastParsedQuotedExpression;

            /// <summary>
            /// Create an enumerator from a base IEnumerable of items into an enumerable
            /// of transformation result which includes the new itemspec and the base item.
            /// </summary>
            internal static List<KeyValuePair<string, S>> GetItemPairs(ICollection<S> itemsOfType)
            {
                List<KeyValuePair<string, S>> itemsFromCapture = new(itemsOfType.Count);

                // iterate over the items, and add items in the tuple format
                foreach (S item in itemsOfType)
                {
                    if (Traits.Instance.UseLazyWildCardEvaluation)
                    {
                        foreach (var resultantItem in
                            EngineFileUtilities.GetFileListEscaped(
                                item.ProjectDirectory,
                                item.EvaluatedIncludeEscaped,
                                forceEvaluate: true))
                        {
                            itemsFromCapture.Add(new KeyValuePair<string, S>(resultantItem, item));
                        }
                    }
                    else
                    {
                        itemsFromCapture.Add(new KeyValuePair<string, S>(item.EvaluatedIncludeEscaped, item));
                    }
                }

                return itemsFromCapture;
            }

            /// <summary>
            /// Intrinsic function that adds the number of items in the list.
            /// </summary>
            internal static void Count(List<KeyValuePair<string, S>> itemsOfType, List<KeyValuePair<string, S>> transformedItems)
            {
                transformedItems.Add(new KeyValuePair<string, S>(Convert.ToString(itemsOfType.Count, CultureInfo.InvariantCulture), null /* no base item */));
            }

            /// <summary>
            /// Intrinsic function that adds the specified built-in modifer value of the items in itemsOfType
            /// Tuple is {current item include, item under transformation}.
            /// </summary>
            internal static void ItemSpecModifierFunction(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    // If the item include has become empty,
                    // this is the end of the pipeline for this item
                    if (String.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }

                    string result = null;

                    try
                    {
                        // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                        // In that case,
                        // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                        // only exist within a target where we can trust the current directory
                        // 2. in single process mode we get the project directory set for the thread
                        string directoryToUse = item.Value.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                        string definingProjectEscaped = item.Value.GetMetadataValueEscaped(ItemSpecModifiers.DefiningProjectFullPath);

                        result = ItemSpecModifiers.GetItemSpecModifier(directoryToUse, item.Key, definingProjectEscaped, functionName);
                    }
                    // InvalidOperationException is how GetItemSpecModifier communicates invalid conditions upwards, so
                    // we do not want to rethrow in that case.
                    catch (Exception e) when (!ExceptionHandling.NotExpectedException(e) || e is InvalidOperationException)
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                    }

                    if (!String.IsNullOrEmpty(result))
                    {
                        // GetItemSpecModifier will have returned us an escaped string
                        // there is nothing more to do than yield it into the pipeline
                        transformedItems.Add(new KeyValuePair<string, S>(result, item.Value));
                    }
                    else if (includeNullEntries)
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the subset of items that actually exist on disk.
            /// </summary>
            internal static void Exists(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (String.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);

                    string rootedPath = null;
                    try
                    {
                        // If we're a projectitem instance then we need to get
                        // the project directory and be relative to that
                        if (Path.IsPathRooted(unescapedPath))
                        {
                            rootedPath = unescapedPath;
                        }
                        else
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case,
                            // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            // 2. in single process mode we get the project directory set for the thread
                            string baseDirectoryToUse = item.Value.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                            rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                        }
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                    }

                    if (FileSystems.Default.FileOrDirectoryExists(rootedPath))
                    {
                        transformedItems.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that combines the existing paths of the input items with a given relative path.
            /// </summary>
            internal static void Combine(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string relativePath = arguments[0];

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (String.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);
                    string combinedPath = Path.Combine(unescapedPath, relativePath);
                    string escapedPath = EscapingUtilities.Escape(combinedPath);
                    transformedItems.Add(new KeyValuePair<string, S>(escapedPath, null));
                }
            }

            /// <summary>
            /// Intrinsic function that adds all ancestor directories of the given items.
            /// </summary>
            internal static void GetPathsOfAllDirectoriesAbove(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                // Phase 1: find all the applicable directories.

                SortedSet<string> directories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (String.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }

                    string directoryName = null;

                    // Unescape as we are passing to the file system
                    string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);

                    try
                    {
                        string rootedPath;

                        // If we're a projectitem instance then we need to get
                        // the project directory and be relative to that
                        if (Path.IsPathRooted(unescapedPath))
                        {
                            rootedPath = unescapedPath;
                        }
                        else
                        {
                            // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                            // In that case,
                            // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                            // only exist within a target where we can trust the current directory
                            // 2. in single process mode we get the project directory set for the thread
                            string baseDirectoryToUse = item.Value.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                            rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                        }

                        // Normalize the path to remove elements like "..".
                        // Otherwise we run the risk of returning two or more different paths that represent the
                        // same directory.
                        rootedPath = FileUtilities.NormalizePath(rootedPath);
                        directoryName = Path.GetDirectoryName(rootedPath);
                    }
                    catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                    {
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                    }

                    while (!String.IsNullOrEmpty(directoryName))
                    {
                        if (directories.Contains(directoryName))
                        {
                            // We've already got this directory (and all its ancestors) in the set.
                            break;
                        }

                        directories.Add(directoryName);
                        directoryName = Path.GetDirectoryName(directoryName);
                    }
                }

                // Phase 2: Go through the directories and return them in order

                foreach (string directoryPath in directories)
                {
                    string escapedDirectoryPath = EscapingUtilities.Escape(directoryPath);
                    transformedItems.Add(new KeyValuePair<string, S>(escapedDirectoryPath, null));
                }
            }

            /// <summary>
            /// Intrinsic function that adds the DirectoryName of the items in itemsOfType
            /// UNDONE: This can be removed in favor of a built-in %(DirectoryName) metadata in future.
            /// </summary>
            internal static void DirectoryName(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                Dictionary<string, string> directoryNameTable = new Dictionary<string, string>(itemsOfType.Count, StringComparer.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    // If the item include has become empty,
                    // this is the end of the pipeline for this item
                    if (String.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }

                    string directoryName;
                    if (!directoryNameTable.TryGetValue(item.Key, out directoryName))
                    {
                        // Unescape as we are passing to the file system
                        string unescapedPath = EscapingUtilities.UnescapeAll(item.Key);

                        try
                        {
                            string rootedPath;

                            // If we're a projectitem instance then we need to get
                            // the project directory and be relative to that
                            if (Path.IsPathRooted(unescapedPath))
                            {
                                rootedPath = unescapedPath;
                            }
                            else
                            {
                                // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                                // In that case,
                                // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                                // only exist within a target where we can trust the current directory
                                // 2. in single process mode we get the project directory set for the thread
                                string baseDirectoryToUse = item.Value.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? String.Empty;
                                rootedPath = Path.Combine(baseDirectoryToUse, unescapedPath);
                            }

                            directoryName = Path.GetDirectoryName(rootedPath);
                        }
                        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                        {
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "InvalidItemFunctionExpression", functionName, item.Key, e.Message);
                        }

                        // Escape as this is going back into the engine
                        directoryName = EscapingUtilities.Escape(directoryName);
                        directoryNameTable[unescapedPath] = directoryName;
                    }

                    if (!String.IsNullOrEmpty(directoryName))
                    {
                        // return a result through the enumerator
                        transformedItems.Add(new KeyValuePair<string, S>(directoryName, item.Value));
                    }
                    else if (includeNullEntries)
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the contents of the metadata in specified in argument[0].
            /// </summary>
            internal static void Metadata(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (item.Value != null)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (!String.IsNullOrEmpty(metadataValue))
                        {
                            // It may be that the itemspec has unescaped ';'s in it so we need to split here to handle
                            // that case.
                            if (metadataValue.Contains(';'))
                            {
                                var splits = ExpressionShredder.SplitSemiColonSeparatedList(metadataValue);

                                foreach (string itemSpec in splits)
                                {
                                    // return a result through the enumerator
                                    transformedItems.Add(new KeyValuePair<string, S>(itemSpec, item.Value));
                                }
                            }
                            else
                            {
                                // return a result through the enumerator
                                transformedItems.Add(new KeyValuePair<string, S>(metadataValue, item.Value));
                            }
                        }
                        else if (metadataValue != String.Empty && includeNullEntries)
                        {
                            transformedItems.Add(new KeyValuePair<string, S>(metadataValue, item.Value));
                        }
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only the items from itemsOfType that have distinct Item1 in the Tuple
            /// Using a case sensitive comparison.
            /// </summary>
            internal static void DistinctWithCase(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                DistinctWithComparer(elementLocation, functionName, itemsOfType, arguments, StringComparer.Ordinal, transformedItems);
            }

            /// <summary>
            /// Intrinsic function that adds only the items from itemsOfType that have distinct Item1 in the Tuple
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void Distinct(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                DistinctWithComparer(elementLocation, functionName, itemsOfType, arguments, StringComparer.OrdinalIgnoreCase, transformedItems);
            }

            /// <summary>
            /// Intrinsic function that adds only the items from itemsOfType that have distinct Item1 in the Tuple
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void DistinctWithComparer(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, StringComparer comparer, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                // This dictionary will ensure that we only return one result per unique itemspec
                HashSet<string> seenItems = new HashSet<string>(itemsOfType.Count, comparer);

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (item.Key != null && seenItems.Add(item.Key))
                    {
                        transformedItems.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function reverses the item list.
            /// </summary>
            internal static void Reverse(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                for (int i = itemsOfType.Count - 1; i >= 0; i--)
                {
                    transformedItems.Add(itemsOfType[i]);
                }
            }

            /// <summary>
            /// Intrinsic function that transforms expressions like the %(foo) in @(Compile->'%(foo)').
            /// </summary>
            internal static void ExpandQuotedExpressionFunction(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string quotedExpressionFunction = arguments[0];
                OneOrMultipleMetadataMatches matches = GetQuotedExpressionMatches(quotedExpressionFunction, elementLocation);

                // This is just a sanity check in case a code change causes something in the call stack to take this reference.
                SpanBasedStringBuilder includeBuilder = s_includeBuilder ?? new SpanBasedStringBuilder();
                s_includeBuilder = null;

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    string include = null;

                    // If we've been handed a null entry by an upstream transform
                    // then we don't want to try to tranform it with an itemspec modification.
                    // Simply allow the null to be passed along (if, we are including nulls as specified by includeNullEntries
                    if (item.Key != null)
                    {
                        int curIndex = 0;

                        switch (matches.Type)
                        {
                            case MetadataMatchType.None:
                                // If we didn't match anything, just use the original string.
                                include = quotedExpressionFunction;
                                break;

                            // If we matched on a full string, we don't have to concatenate anything.
                            case MetadataMatchType.ExactSingle:
                                include = GetMetadataValueFromMatch(matches.Single, item.Key, item.Value, elementLocation, ref curIndex);
                                break;

                            // If we matched on a partial string, just replace the single group.
                            case MetadataMatchType.InexactSingle:
                                includeBuilder.Append(quotedExpressionFunction, 0, matches.Single.Index);
                                includeBuilder.Append(
                                    GetMetadataValueFromMatch(matches.Single, item.Key, item.Value, elementLocation, ref curIndex));
                                includeBuilder.Append(quotedExpressionFunction, curIndex, quotedExpressionFunction.Length - curIndex);
                                include = includeBuilder.ToString();
                                includeBuilder.Clear();
                                break;

                            // Otherwise, iteratively replace each match group.
                            case MetadataMatchType.Multiple:
                                foreach (MetadataMatch match in matches.Multiple)
                                {
                                    includeBuilder.Append(quotedExpressionFunction, curIndex, match.Index - curIndex);
                                    includeBuilder.Append(
                                        GetMetadataValueFromMatch(match, item.Key, item.Value, elementLocation, ref curIndex));
                                }

                                includeBuilder.Append(quotedExpressionFunction, curIndex, quotedExpressionFunction.Length - curIndex);
                                include = includeBuilder.ToString();
                                includeBuilder.Clear();
                                break;
                            default:
                                break;
                        }
                    }

                    // Include may be empty. Historically we have created items with empty include
                    // and ultimately set them on tasks, but we don't do that anymore as it's broken.
                    // Instead we optionally add a null, so that input and output lists are the same length; this allows
                    // the caller to possibly do correlation.

                    // We pass in the existing item so we can copy over its metadata
                    if (!string.IsNullOrEmpty(include))
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(include, item.Value));
                    }
                    else if (includeNullEntries)
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                    }
                }

                s_includeBuilder = includeBuilder;
            }

            /// <summary>
            /// Extracts a value from the input string based on a regular expression.
            /// In the vast majority of cases, we'll only have 1-2 matches, and within those we can avoid allocating
            /// the vast majority of Regex objects and return a cached result.
            /// </summary>
            private static OneOrMultipleMetadataMatches GetQuotedExpressionMatches(string quotedExpressionFunction, IElementLocation elementLocation)
            {
                // Start with fast paths to avoid any allocations.
                if (TryGetCachedMetadataMatch(quotedExpressionFunction, out string cachedName)
                    || s_itemSpecModifiers.TryGetValue(quotedExpressionFunction, out cachedName))
                {
                    return new OneOrMultipleMetadataMatches(cachedName);
                }

                // GroupCollection + Groups are the most expensive source of allocations here, so we want to return
                // before ever accessing the property. Simply accessing it will trigger the full collection
                // allocation, so we avoid it unless absolutely necessary.
                // Unfortunately even .NET Core does not have a struct-based Group enumerator at this point.
                Match match = RegularExpressions.ItemMetadataRegex.Match(quotedExpressionFunction);

                if (!match.Success)
                {
                    // No matches - the caller will use the original string.
                    return new OneOrMultipleMetadataMatches();
                }

                // From here will either return:
                // 1. A single match, which may be offset within the input string..
                // 2. A list of multiple matches.
                List<MetadataMatch> multipleMatches = null;
                while (match.Success)
                {
                    // If true, this is likely an interpolated string, e.g. NETCOREAPP%(Identity)_OR_GREATER
                    bool isItemSpecModifier = s_itemSpecModifiers.TryGetValue(match.Value, out string name);
                    if (!isItemSpecModifier)
                    {
                        // Here is the worst case path which we've hopefully avoided at the point.
                        GroupCollection groupCollection = match.Groups;
                        name = groupCollection[RegularExpressions.NameGroup].Value;
                        ProjectErrorUtilities.VerifyThrowInvalidProject(groupCollection[RegularExpressions.ItemSpecificationGroup].Length == 0, elementLocation, "QualifiedMetadataInTransformNotAllowed", match.Value, name);
                    }

                    Match nextMatch = match.NextMatch();

                    // If we only have a single match, return before allocating the list.
                    bool isSingleMatch = multipleMatches == null && !nextMatch.Success;
                    if (isSingleMatch)
                    {
                        OneOrMultipleMetadataMatches singleMatch = new(quotedExpressionFunction, match, name);

                        // Only cache full string matches - skip known modifiers since they are permenantly cached.
                        if (singleMatch.Type == MetadataMatchType.ExactSingle && !isItemSpecModifier)
                        {
                            s_lastParsedQuotedExpression = name;
                        }

                        return singleMatch;
                    }

                    // We have multiple matches, so run the full loop.
                    // e.g. %(Filename)%(Extension)
                    // This is a very hot path, so we avoid allocating this until after we know there are multiple matches.
                    multipleMatches ??= [];
                    multipleMatches.Add(new MetadataMatch(match, name));
                    match = nextMatch;
                }

                return new OneOrMultipleMetadataMatches(multipleMatches);
            }

            /// <summary>
            /// Given a string such as %(ReferenceAssembly), check if the inner substring matches the cached value.
            /// If so, return the cached substring without allocating.
            /// </summary>
            /// <remarks>
            /// <see cref="ExpandQuotedExpressionFunction"/> often receives the same expression for multiple calls.
            /// To save on regex overhead, we cache the last substring extracted from a regex match.
            /// This is thread-safe as long as all checks work on a consistent local reference.
            /// </remarks>
            private static bool TryGetCachedMetadataMatch(string stringToCheck, out string cachedMatch)
            {
                // Pull a local reference first in case the cached value is swapped.
                cachedMatch = s_lastParsedQuotedExpression;
                if (string.IsNullOrEmpty(cachedMatch))
                {
                    return false;
                }

                // Quickly cancel out of definite misses.
                int length = stringToCheck.Length;
                if (length == cachedMatch.Length + QuotedExpressionSurroundCharCount
                    && stringToCheck[0] == '%' && stringToCheck[1] == '(' && stringToCheck[length - 1] == ')')
                {
                    // If the inner slice is a hit, don't allocate a string.
                    ReadOnlySpan<char> span = stringToCheck.AsSpan(2, length - QuotedExpressionSurroundCharCount);
                    if (span.SequenceEqual(cachedMatch.AsSpan()))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Intrinsic function that transforms expressions by invoking methods of System.String on the itemspec
            /// of the item in the pipeline.
            /// </summary>
            internal static void ExecuteStringFunction(
                Expander<P, I> expander,
                IElementLocation elementLocation,
                bool includeNullEntries,
                string functionName,
                List<KeyValuePair<string, S>> itemsOfType,
                string[] arguments,
                List<KeyValuePair<string, S>> transformedItems)
            {
                // Transform: expression is like @(Compile->'%(foo)'), so create completely new items,
                // using the Include from the source items
                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    Function function = new Function(
                        typeof(string),
                        item.Key,
                        item.Key,
                        functionName,
                        arguments,
                        BindingFlags.Public | BindingFlags.InvokeMethod,
                        string.Empty,
                        expander.PropertiesUseTracker,
                        expander._fileSystem,
                        expander._loggingContext);

                    object result = function.Execute(item.Key, expander._properties, ExpanderOptions.ExpandAll, elementLocation);

                    string include = PropertyExpander.ConvertToString(result);

                    // We pass in the existing item so we can copy over its metadata
                    if (include.Length > 0)
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(include, item.Value));
                    }
                    else if (includeNullEntries)
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(null, item.Value));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds the items from itemsOfType with their metadata cleared, i.e. only the itemspec is retained.
            /// </summary>
            internal static void ClearMetadata(IElementLocation elementLocation, bool includeNullEntries, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments == null || arguments.Length == 0, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (includeNullEntries || item.Key != null)
                    {
                        transformedItems.Add(new KeyValuePair<string, S>(item.Key, null));
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only those items that have a not-blank value for the metadata specified
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void HasMetadata(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 1, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                    }

                    // GetMetadataValueEscaped returns empty string for missing metadata,
                    // but IItem specifies it should return null
                    if (!string.IsNullOrEmpty(metadataValue))
                    {
                        // return a result through the enumerator
                        transformedItems.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds only those items have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void WithMetadataValue(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];
                string metadataValueToFind = arguments[1];

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                    }

                    if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a result through the enumerator
                        transformedItems.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds those items don't have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void WithoutMetadataValue(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];
                string metadataValueToFind = arguments[1];

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    string metadataValue = null;

                    try
                    {
                        metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                    }
                    catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                    {
                        // Blank metadata name
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                    }

                    if (!String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                    {
                        // return a result through the enumerator
                        transformedItems.Add(item);
                    }
                }
            }

            /// <summary>
            /// Intrinsic function that adds a boolean to indicate if any of the items have the given metadata value
            /// Using a case insensitive comparison.
            /// </summary>
            internal static void AnyHaveMetadataValue(IElementLocation elementLocation, string functionName, List<KeyValuePair<string, S>> itemsOfType, string[] arguments, List<KeyValuePair<string, S>> transformedItems)
            {
                ProjectErrorUtilities.VerifyThrowInvalidProject(arguments?.Length == 2, elementLocation, "InvalidItemFunctionSyntax", functionName, arguments == null ? 0 : arguments.Length);

                string metadataName = arguments[0];
                string metadataValueToFind = arguments[1];
                bool metadataFound = false;

                foreach (KeyValuePair<string, S> item in itemsOfType)
                {
                    if (item.Value != null)
                    {
                        string metadataValue = null;

                        try
                        {
                            metadataValue = item.Value.GetMetadataValueEscaped(metadataName);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
                        {
                            // Blank metadata name
                            ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", metadataName, ex.Message);
                        }

                        if (metadataValue != null && String.Equals(metadataValue, metadataValueToFind, StringComparison.OrdinalIgnoreCase))
                        {
                            metadataFound = true;

                            // return a result through the enumerator
                            transformedItems.Add(new KeyValuePair<string, S>("true", item.Value));

                            // break out as soon as we found a match
                            return;
                        }
                    }
                }

                if (!metadataFound)
                {
                    // We did not locate an item with the required metadata
                    transformedItems.Add(new KeyValuePair<string, S>("false", null));
                }
            }

            /// <summary>
            /// Expands the metadata in the match provided into a string result.
            /// The match is expected to be the content of a transform.
            /// For example, representing "%(Filename.obj)" in the original expression "@(Compile->'%(Filename.obj)')".
            /// </summary>
            private static string GetMetadataValueFromMatch(
                MetadataMatch match,
                string itemSpec,
                IItem sourceOfMetadata,
                IElementLocation elementLocation,
                ref int curIndex)
            {
                string value = null;
                try
                {
                    if (ItemSpecModifiers.IsDerivableItemSpecModifier(match.Name))
                    {
                        // If we're not a ProjectItem or ProjectItemInstance, then ProjectDirectory will be null.
                        // In that case,
                        // 1. in multiprocess mode we're safe to get the current directory as we'll be running on TaskItems which
                        // only exist within a target where we can trust the current directory
                        // 2. in single process mode we get the project directory set for the thread
                        string directoryToUse = sourceOfMetadata.ProjectDirectory ?? FileUtilities.CurrentThreadWorkingDirectory ?? Directory.GetCurrentDirectory();
                        string definingProjectEscaped = sourceOfMetadata.GetMetadataValueEscaped(ItemSpecModifiers.DefiningProjectFullPath);

                        value = ItemSpecModifiers.GetItemSpecModifier(directoryToUse, itemSpec, definingProjectEscaped, match.Name);
                    }
                    else
                    {
                        value = sourceOfMetadata.GetMetadataValueEscaped(match.Name);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "CannotEvaluateItemMetadata", match.Name, ex.Message);
                }

                curIndex = match.Index + match.Length;
                return value;
            }

            /// <summary>
            /// The type of match we found.
            /// We use this to determine how to build the final output string.
            /// </summary>
            private enum MetadataMatchType
            {

                /// <summary>
                /// No matches found. The result will be empty.
                /// </summary>
                None,

                /// <summary>
                /// An exact full string match, e.g. '%(FullPath)'.
                /// </summary>
                ExactSingle,

                /// <summary>
                /// A single match with surrounding characters, e.g. 'somedir/%(FileName)'.
                /// </summary>
                InexactSingle,

                /// <summary>
                /// Multiple matches found, e.g. '%(FullPath)%(Extension)'.
                /// </summary>
                Multiple,
            }

            /// <summary>
            /// A discriminated union between one exact, one partial, or multiple matches.
            /// </summary>
            private readonly struct OneOrMultipleMetadataMatches
            {
                public OneOrMultipleMetadataMatches()
                {
                    Type = MetadataMatchType.None;
                }

                public OneOrMultipleMetadataMatches(string name)
                {
                    Type = MetadataMatchType.ExactSingle;
                    Single = new MetadataMatch(name);
                }

                public OneOrMultipleMetadataMatches(string quotedExpressionFunction, Match match, string name)
                {
                    // We know we have a full string match when our extracted name is the same length as the input
                    // string minus the surrounding characters.
                    Type = quotedExpressionFunction.Length == name.Length + QuotedExpressionSurroundCharCount
                            ? MetadataMatchType.ExactSingle
                            : MetadataMatchType.InexactSingle;
                    Single = new MetadataMatch(match, name);
                }

                public OneOrMultipleMetadataMatches(List<MetadataMatch> allMatches)
                {
                    Type = MetadataMatchType.Multiple;
                    Multiple = allMatches;
                }

                internal MetadataMatch Single { get; }

                internal List<MetadataMatch> Multiple { get; }

                internal MetadataMatchType Type { get; }
            }

            /// <summary>
            /// Represents a single match. Whether it was cached or from a Regex should be transparent
            /// since we simulate the length calculation.
            /// </summary>
            private readonly struct MetadataMatch
            {
                public MetadataMatch(string name)
                {
                    Name = name;
                    Index = 0;
                    Length = name.Length + QuotedExpressionSurroundCharCount;
                }

                public MetadataMatch(Match match, string name)
                {
                    Name = name;
                    Index = match.Index;
                    Length = match.Length;
                }

                /// <summary>
                /// The inner value of the match.
                /// </summary>
                internal string Name { get; }

                /// <summary>
                /// The index of the match in the original string.
                /// If we have an exact string match, this will be 0.
                /// </summary>
                internal int Index { get; }

                /// <summary>
                /// The length of the match in the original string.
                /// If we have an exact string match, this computed to match the original input.
                /// </summary>
                internal int Length { get; }
            }
        }
    }
}
