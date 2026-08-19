// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
#if !NET
using System.Text;
#endif
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;
#if NET
using Microsoft.Build.Utilities;
#endif
using Microsoft.NET.StringTools;

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
    private static partial class ItemExpander
    {
        /// <summary>
        ///  Executes the list of transform functions.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///  Each captured transform function will be mapped to either a static method on
        ///  <see cref="Transforms"/> or a known item spec modifier which operates on the item path.
        ///  </para>
        ///  <para>
        ///  For each function, the full list of items will be iteratively transformed using the
        ///  output of the previous. E.g. given functions f, g, h, the order of operations will
        ///  look like: <c>results = h(g(f(items)))</c>.
        ///  </para>
        ///  <para>
        ///  If no function name is found, the transform is evaluated as a quoted expression.
        ///  </para>
        /// </remarks>
        /// <returns>
        ///  <see langword="true"/> if the transform completed successfully; <see langword="false"/> if
        ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> was set and the result is non-empty.
        /// </returns>
        private static bool TryTransform(
            Expander<P, I> expander,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            ImmutableArray<ItemTransform> transforms,
            ICollection<I> itemsOfType,
            out List<TransformEntry> result)
        {
            // Each transform runs on the full set of transformed items from the previous result.
            // We can reuse our buffers by just swapping the references after each transform.
            List<TransformEntry> input = CreateEntries(itemsOfType);
            List<TransformEntry> output = new(itemsOfType.Count);

            // Create a TransformFunction for each transform in the chain by extracting the relevant information
            // from the regex parsing results
            for (int i = 0; i < transforms.Length; i++)
            {
                ItemTransform transform = transforms[i];
                StringSegment function = transform.Text;
                StringSegment functionName = transform.FunctionName;
                StringSegment argumentsExpression = transform.FunctionArguments;

                StringSegment[] arguments = null;
                TransformKind kind;
                ItemSpecModifierKind modifierKind = default;

                // Quoted transforms have no function name. Select their kind directly to avoid synthesizing
                // a function name and one-element argument array, then performing name-based dispatch.
                if (transform.IsQuotedExpression)
                {
                    kind = TransformKind.ExpandQuotedExpressionFunction;
                }
                else
                {
                    if (argumentsExpression.HasValue)
                    {
                        arguments = ExtractFunctionArguments(elementLocation, function, argumentsExpression);
                    }

                    if (ItemSpecModifiers.TryGetDerivableModifierKind(functionName, out modifierKind))
                    {
                        kind = TransformKind.ItemSpecModifierFunction;
                    }
                    else if (!TryGetIntrinsicTransform(functionName, out kind))
                    {
                        kind = TransformKind.ExecuteStringFunction;
                    }
                }

                switch (kind)
                {
                    case TransformKind.ItemSpecModifierFunction:
                        Transforms.ItemSpecModifierFunction(input, output, arguments, includeNullEntries, functionName, modifierKind, elementLocation);
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
                        // The unnamed form stores the quoted expression in transform.Text. An explicitly named
                        // invocation uses parsed arguments so its existing syntax validation is preserved.
                        if (transform.IsQuotedExpression)
                        {
                            Transforms.ExpandQuotedExpressionFunction(input, output, function, includeNullEntries, elementLocation);
                        }
                        else
                        {
                            Transforms.ExpandQuotedExpressionFunction(input, output, arguments, includeNullEntries, functionName, elementLocation);
                        }

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
                        ProjectErrorUtilities.ThrowInvalidProject(elementLocation, "UnknownItemFunction", functionName.ValueOrEmpty);
                        break;
                }

                // If we have another transform, swap the source and transform lists.
                if (i < transforms.Length - 1)
                {
                    (output, input) = (input, output);
                    output.Clear();
                }
            }

            // Check for break on non-empty only after ALL transforms are complete
            if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
            {
                foreach (TransformEntry entry in output)
                {
                    if (!string.IsNullOrEmpty(entry.Value))
                    {
                        result = null;
                        return false;
                    }
                }
            }

            result = output;
            return true;
        }

        /// <summary>
        ///  Resolves a built-in item transform without hashing or realizing its parsed name.
        /// </summary>
        /// <param name="name">The transform name segment.</param>
        /// <param name="kind">The resolved transform kind.</param>
        /// <returns>
        ///  <see langword="true"/> when <paramref name="name"/> identifies an intrinsic transform;
        ///  otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryGetIntrinsicTransform(StringSegment name, out TransformKind kind)
        {
            kind = name.Length switch
            {
                5 when name.Equals("Count", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.Count,
                6 when name.Equals("Exists", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.Exists,
                7 when name.Equals("Combine", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.Combine,
                7 when name.Equals("Reverse", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.Reverse,
                8 when name.Equals("Metadata", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.Metadata,
                8 when name.Equals("Distinct", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.Distinct,
                11 when name.Equals("HasMetadata", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.HasMetadata,
                13 when name.Equals("DirectoryName", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.DirectoryName,
                13 when name.Equals("ClearMetadata", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.ClearMetadata,
                16 when name.Equals("DistinctWithCase", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.DistinctWithCase,
                17 when name.Equals("WithMetadataValue", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.WithMetadataValue,
                20 when name.Equals("WithoutMetadataValue", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.WithoutMetadataValue,
                20 when name.Equals("AnyHaveMetadataValue", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.AnyHaveMetadataValue,
                21 when name.Equals("ExecuteStringFunction", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.ExecuteStringFunction,
                29 when name.Equals("GetPathsOfAllDirectoriesAbove", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.GetPathsOfAllDirectoriesAbove,
                30 when name.Equals("ExpandQuotedExpressionFunction", StringComparison.OrdinalIgnoreCase) =>
                    TransformKind.ExpandQuotedExpressionFunction,
                _ => default,
            };

            return kind != default;
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
            Expander<P, I> expander,
            string expression,
            IItemProvider<I> items,
            IItemFactory<I, T> itemFactory,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            IElementLocation elementLocation)
            where T : class, IItem
            => ExpandSingleItemVectorExpressionIntoItems(
                expander,
                expression.AsSegment(),
                items,
                itemFactory,
                options,
                includeNullEntries,
                out isTransformExpression,
                elementLocation);

        /// <inheritdoc cref="ExpandSingleItemVectorExpressionIntoItems{T}(Expander{P, I}, string, IItemProvider{I}, IItemFactory{I, T}, ExpanderOptions, bool, out bool, IElementLocation)"/>
        internal static IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(
            Expander<P, I> expander,
            StringSegment expression,
            IItemProvider<I> items,
            IItemFactory<I, T> itemFactory,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            IElementLocation elementLocation)
            where T : class, IItem
        {
            isTransformExpression = false;

            return TryExpandSingleItemVectorExpression(expression, options, elementLocation, out ItemVector itemVector)
                ? ExpandItemVectorIntoItems(
                    itemVector,
                    expander,
                    items,
                    itemFactory,
                    options,
                    includeNullEntries,
                    out isTransformExpression,
                    elementLocation)
                : null;
        }

        internal static bool TryExpandSingleItemVectorExpression(
            string expression,
            ExpanderOptions options,
            IElementLocation elementLocation,
            out ItemVector itemVector)
            => TryExpandSingleItemVectorExpression(expression.AsSegment(), options, elementLocation, out itemVector);

        /// <inheritdoc cref="TryExpandSingleItemVectorExpression(string, ExpanderOptions, IElementLocation, out ItemVector)"/>
        internal static bool TryExpandSingleItemVectorExpression(
            StringSegment expression,
            ExpanderOptions options,
            IElementLocation elementLocation,
            out ItemVector itemVector)
        {
            if (((options & ExpanderOptions.ExpandItems) == 0) || expression.Length == 0)
            {
                itemVector = default;
                return false;
            }

            if (!ExpressionShredder.TryGetNextItemVectorExpression(expression, out itemVector))
            {
                return false;
            }

            // We have a single valid @(itemlist) reference in the given expression.
            // If the passed-in expression contains exactly one item list reference,
            // with nothing else concatenated to the beginning or end, then proceed
            // with itemizing it, otherwise error.
            ProjectErrorUtilities.VerifyThrowInvalidProject(
                itemVector.Index == 0 && itemVector.Length == expression.Length,
                elementLocation,
                "EmbeddedItemVectorCannotBeItemized",
                expression.ValueOrEmpty);

            return true;
        }

        internal static IList<T> ExpandItemVectorIntoItems<T>(
            ItemVector itemVector, Expander<P, I> expander, IItemProvider<I> items, IItemFactory<I, T> itemFactory,
            ExpanderOptions options, bool includeNullEntries, out bool isTransformExpression, IElementLocation elementLocation)
            where T : class, IItem
        {
            Assumed.NotNull(items, "Cannot expand items without providing items");
            isTransformExpression = false;
            bool brokeEarlyNonEmpty;

            // If the incoming factory doesn't have an item type that it can use to
            // create items, it's our indication that the caller wants its items to have the type of the
            // expression being expanded. For example, items from expanding "@(Compile") should
            // have the item type "Compile".
            if (itemFactory.ItemType == null)
            {
                itemFactory.ItemType = itemVector.ItemType.ValueOrEmpty;
            }

            IList<T> result;
            if (itemVector.HasSeparator)
            {
                // Reference contains a separator, for example @(Compile, ';').
                // We need to flatten the list into
                // a scalar and then create a single item. Basically we need this
                // to be able to convert item lists with user specified separators into properties.
                string expandedItemVector;
                using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();
                brokeEarlyNonEmpty = ExpandItemVectorIntoStringBuilder(expander, itemVector, items, elementLocation, builder, options);

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

            List<TransformEntry> entries;
            brokeEarlyNonEmpty = ExpandItemVector(expander, itemVector, items, elementLocation /* including null items */, options, true, out isTransformExpression, out entries);

            if (brokeEarlyNonEmpty)
            {
                return null;
            }

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
        ///  Expands an item vector into a list of escaped values.
        ///  If the item vector specifies a separator, the values are joined into a single entry.
        /// </summary>
        /// <param name="expander">The expander whose state will be used to expand any transforms.</param>
        /// <param name="itemVector">
        ///  The parsed item vector to expand.
        /// </param>
        /// <param name="evaluatedItems">The <see cref="IItemProvider{T}"/> that provides the items to expand.</param>
        /// <param name="elementLocation">The location of the XML element containing <paramref name="itemVector"/>.</param>
        /// <param name="options">The expansion options.</param>
        /// <param name="includeNullEntries">Whether to include values that evaluate to <see langword="null"/>.</param>
        /// <param name="isTransformExpression">
        ///  <see langword="true"/> if the item vector contains a transform, even when its item list is empty.
        /// </param>
        /// <param name="entries">
        ///  The expanded entries, or <see langword="null"/> when the expression produces no entries.
        ///  <see cref="TransformEntry.Value"/> contains the escaped value, and <see cref="TransformEntry.Item"/>
        ///  identifies the item from which the value was derived, when available.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if <see cref="ExpanderOptions.BreakOnNotEmpty"/> caused expansion to stop after
        ///  determining that the result would be non-empty; otherwise, <see langword="false"/>.
        /// </returns>
        internal static bool ExpandItemVector(
            Expander<P, I> expander,
            ItemVector itemVector,
            IItemProvider<I> evaluatedItems,
            IElementLocation elementLocation,
            ExpanderOptions options,
            bool includeNullEntries,
            out bool isTransformExpression,
            out List<TransformEntry> entries)
        {
            Assumed.NotNull(evaluatedItems, "Cannot expand items without providing items");

            // An empty item type indicates that the expression could not be parsed correctly.
            ProjectErrorUtilities.VerifyThrowInvalidProject(!itemVector.ItemType.IsNullOrEmpty, elementLocation, "InvalidFunctionPropertyExpression");

            ICollection<I> items = evaluatedItems.GetItems(itemVector.ItemType);
            ImmutableArray<ItemTransform> transforms = itemVector.Transforms;
            StringSegment separator = itemVector.Separator;

            isTransformExpression = !transforms.IsEmpty;
            entries = null;

            if (!isTransformExpression)
            {
                // An empty item vector produces no entries.
                if (items.Count == 0)
                {
                    return false; // did not break early
                }

                bool breakOnNotEmpty = (options & ExpanderOptions.BreakOnNotEmpty) != 0;

                // An explicit separator, such as @(Compile, ','), collapses the item vector into one scalar entry.
                if (separator.HasValue)
                {
                    if (!TryJoinItems(items, separator, breakOnNotEmpty, out string result))
                    {
                        return true; // broke early
                    }

                    entries = new(capacity: 1) { new(result, null) };
                    return false; // did not break early
                }

                // Without a transform, preserve each item's escaped include and its original item.
                foreach (I item in items)
                {
                    string evaluatedIncludeEscaped = item.EvaluatedIncludeEscaped;
                    if (breakOnNotEmpty && evaluatedIncludeEscaped.Length > 0)
                    {
                        return true; // broke early
                    }

                    entries ??= new List<TransformEntry>(items.Count);
                    entries.Add(new TransformEntry(evaluatedIncludeEscaped, item));
                }

                return false; // did not break early
            }

            // Most transforms cannot produce a value from an empty item list.
            if (items.Count == 0 && !ShouldEvaluateEmptyList(transforms))
            {
                return false; // did not break early
            }

            if (!TryTransform(expander, elementLocation, options, includeNullEntries, transforms, items, out entries))
            {
                return true; // broke early
            }

            if (separator.HasValue)
            {
                // An explicit separator collapses the transformed values into one scalar entry.
                string joinedItems = JoinEntries(separator, entries);

                entries.Clear();
                entries.Add(new TransformEntry(joinedItems, null));
            }

            return false; // did not break early

            static bool ShouldEvaluateEmptyList(ImmutableArray<ItemTransform> transforms)
            {
                // Count returns zero and AnyHaveMetadataValue returns false for an empty list, so those transforms must still run.
                foreach (ItemTransform transform in transforms)
                {
                    StringSegment functionName = transform.FunctionName;
                    if (functionName.Equals("Count", StringComparison.OrdinalIgnoreCase) ||
                        functionName.Equals("AnyHaveMetadataValue", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            static bool TryJoinItems(ICollection<I> items, StringSegment separator, bool breakOnNotEmpty, out string result)
            {
                using IEnumerator<I> enumerator = items.GetEnumerator();
                if (!enumerator.MoveNext())
                {
                    result = string.Empty;
                    return true; // joined successfully
                }

                string firstItem = enumerator.Current.EvaluatedIncludeEscaped;
                if (breakOnNotEmpty && firstItem.Length > 0)
                {
                    result = null;
                    return false; // broke early
                }

                if (items.Count == 1)
                {
                    result = firstItem;
                    return true; // joined successfully
                }

                // Use stack- and pool-backed storage on .NET and MSBuild's cached StringBuilder on .NET Framework.
#if NET
                using ValueStringBuilder builder = new(stackalloc char[256]);
#else
                StringBuilder builder = StringBuilderCache.Acquire();
#endif
                builder.Append(firstItem);

                while (enumerator.MoveNext())
                {
                    string evaluatedIncludeEscaped = enumerator.Current.EvaluatedIncludeEscaped;
                    if (breakOnNotEmpty && evaluatedIncludeEscaped.Length > 0)
                    {
#if !NET
                        StringBuilderCache.Release(builder);
#endif
                        result = null;
                        return false; // broke early
                    }

#if NET
                    builder.Append(separator.AsSpan());
#else
                    builder.AppendSegment(separator);
#endif
                    builder.Append(evaluatedIncludeEscaped);
                }

#if NET
                result = builder.ToString();
#else
                result = StringBuilderCache.GetStringAndRelease(builder);
#endif
                return true; // joined successfully
            }

            static string JoinEntries(StringSegment separator, List<TransformEntry> entries)
            {
                if (entries is [{ Value: var value }])
                {
                    return value ?? string.Empty;
                }

                // Use stack- and pool-backed storage on .NET and MSBuild's cached StringBuilder on .NET Framework.
#if NET
                using ValueStringBuilder builder = new(stackalloc char[256]);
#else
                StringBuilder builder = StringBuilderCache.Acquire();
#endif
                bool first = true;

                foreach (TransformEntry entry in entries)
                {
                    if (!first)
                    {
#if NET
                        builder.Append(separator.AsSpan());
#else
                        builder.AppendSegment(separator);
#endif
                    }

                    first = false;
                    builder.Append(entry.Value);
                }

#if NET
                return builder.ToString();
#else
                return StringBuilderCache.GetStringAndRelease(builder);
#endif
            }
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

            if (!ExpressionShredder.TryGetNextItemVectorExpression(expression, out ItemVector currentItem))
            {
                return expression;
            }

            return ExpandItemVectorsIntoString(
                expander,
                expression,
                items,
                options,
                elementLocation,
                currentItem);
        }

        /// <summary>
        ///  Expands item vectors when the first vector has already been parsed by an upstream scanner.
        /// </summary>
        internal static string ExpandItemVectorsIntoString(
            Expander<P, I> expander,
            string expression,
            IItemProvider<I> items,
            ExpanderOptions options,
            IElementLocation elementLocation,
            ItemVector currentItem)
        {
            Assumed.NotNull(items, "Cannot expand items without providing items");
            using SpanBasedStringBuilder builder = Strings.GetSpanBasedStringBuilder();

            // As we walk through the matches, we need to copy out the original parts of the string which
            // are not covered by the match.  This preserves original behavior which did not trim whitespace
            // from between separators.
            int lastStringIndex = 0;
            do
            {
                if (currentItem.Index > lastStringIndex)
                {
                    if ((options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return null;
                    }

                    builder.Append(expression, lastStringIndex, currentItem.Index - lastStringIndex);
                }

                bool brokeEarlyNonEmpty = ExpandItemVectorIntoStringBuilder(expander, currentItem, items, elementLocation, builder, options);

                if (brokeEarlyNonEmpty)
                {
                    return null;
                }

                lastStringIndex = currentItem.Index + currentItem.Length;
            }
            while (ExpressionShredder.TryGetNextItemVectorExpression(expression, lastStringIndex, out currentItem));

            builder.Append(expression, lastStringIndex, expression.Length - lastStringIndex);

            return builder.ToString();
        }

        /// <summary>
        ///  Expands an item vector and appends its value to <paramref name="builder"/>.
        /// </summary>
        /// <returns>
        ///  <see langword="true"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/> caused expansion to
        ///  stop after finding a non-empty value; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool ExpandItemVectorIntoStringBuilder(
            Expander<P, I> expander,
            ItemVector itemVector,
            IItemProvider<I> evaluatedItems,
            IElementLocation elementLocation,
            SpanBasedStringBuilder builder,
            ExpanderOptions options)
        {
            if (itemVector.Transforms.IsEmpty &&
                !(IsTruncationEnabled(options) && itemVector.HasSeparator))
            {
                return ExpandBareItemVectorIntoStringBuilder(
                    itemVector,
                    evaluatedItems,
                    builder,
                    options);
            }

            List<TransformEntry> entries;
            bool throwaway;
            var brokeEarlyNonEmpty = ExpandItemVector(expander, itemVector, evaluatedItems, elementLocation /* including null items */, options, true, out throwaway, out entries);

            if (brokeEarlyNonEmpty)
            {
                return true;
            }

            if (entries == null)
            {
                // No items to expand.
                return false;
            }

            int startLength = builder.Length;
            bool truncate = IsTruncationEnabled(options);

            // If the vector has a separator, expansion has already joined the items with it.
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (truncate)
                {
                    if (i >= ItemLimitPerExpansion)
                    {
                        builder.Append("...");
                        return false;
                    }
                    int currentLength = builder.Length - startLength;
                    if (!string.IsNullOrEmpty(entry.Value) && currentLength + entry.Value.Length > CharacterLimitPerExpansion)
                    {
                        int truncateIndex = CharacterLimitPerExpansion - currentLength - 3;
                        if (truncateIndex > 0)
                        {
                            builder.Append(entry.Value, 0, truncateIndex);
                        }
                        builder.Append("...");
                        return false;
                    }
                }
                builder.Append(entry.Value);
                if (i < entries.Count - 1)
                {
                    builder.Append(";");
                }
            }

            return false;
        }

        /// <summary>
        ///  Streams a bare item vector directly into the destination without allocating transform
        ///  entries that carry item references no string-producing caller consumes.
        /// </summary>
        private static bool ExpandBareItemVectorIntoStringBuilder(
            ItemVector itemVector,
            IItemProvider<I> evaluatedItems,
            SpanBasedStringBuilder builder,
            ExpanderOptions options)
        {
            ICollection<I> items = evaluatedItems.GetItems(itemVector.ItemType);
            if (items.Count == 0)
            {
                return false;
            }

            bool breakOnNotEmpty = (options & ExpanderOptions.BreakOnNotEmpty) != 0;
            bool truncate = IsTruncationEnabled(options);
            bool hasExplicitSeparator = itemVector.HasSeparator;
            StringSegment separator = hasExplicitSeparator ? itemVector.Separator : ";";
            int startLength = builder.Length;
            int itemIndex = 0;

            if (hasExplicitSeparator)
            {
                return TryAppendJoinedItems(
                    items,
                    separator,
                    breakOnNotEmpty,
                    builder);
            }

            foreach (I item in items)
            {
                string value = item.EvaluatedIncludeEscaped;
                if (breakOnNotEmpty && value.Length > 0)
                {
                    return true;
                }

                if (truncate && !hasExplicitSeparator && itemIndex >= ItemLimitPerExpansion)
                {
                    builder.Append(separator.AsMemory());
                    builder.Append("...");
                    return false;
                }

                if (itemIndex > 0)
                {
                    builder.Append(separator.AsMemory());
                }

                if (truncate)
                {
                    int currentLength = builder.Length - startLength;
                    if (currentLength + value.Length > CharacterLimitPerExpansion)
                    {
                        int truncateLength = CharacterLimitPerExpansion - currentLength - 3;
                        if (truncateLength > 0)
                        {
                            builder.Append(value, 0, Math.Min(value.Length, truncateLength));
                        }

                        builder.Append("...");
                        return false;
                    }
                }

                builder.Append(value);
                itemIndex++;
            }

            return false;
        }

        /// <summary>
        ///  Joins bare item values into one scalar before appending it to the destination.
        /// </summary>
        /// <param name="items">The items to join.</param>
        /// <param name="separator">The explicit separator.</param>
        /// <param name="breakOnNotEmpty">Whether expansion should stop after finding a non-empty value.</param>
        /// <param name="destination">The destination builder.</param>
        /// <returns>
        ///  <see langword="true"/> when expansion stopped because a non-empty value was found;
        ///  otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryAppendJoinedItems(
            ICollection<I> items,
            StringSegment separator,
            bool breakOnNotEmpty,
            SpanBasedStringBuilder destination)
        {
            using IEnumerator<I> enumerator = items.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                return false;
            }

            string firstValue = enumerator.Current.EvaluatedIncludeEscaped;
            if (breakOnNotEmpty && firstValue.Length > 0)
            {
                return true;
            }

            if (!enumerator.MoveNext())
            {
                destination.Append(firstValue);
                return false;
            }

#if NET
            using ValueStringBuilder joined = new(stackalloc char[256]);
#else
            StringBuilder joined = StringBuilderCache.Acquire();
#endif
            joined.Append(firstValue);

            do
            {
                string value = enumerator.Current.EvaluatedIncludeEscaped;
                if (breakOnNotEmpty && value.Length > 0)
                {
#if !NET
                    StringBuilderCache.Release(joined);
#endif
                    return true;
                }

#if NET
                joined.Append(separator.AsSpan());
#else
                joined.AppendSegment(separator);
#endif
                joined.Append(value);
            }
            while (enumerator.MoveNext());

#if NET
            destination.Append(joined.ToString());
#else
            destination.Append(StringBuilderCache.GetStringAndRelease(joined));
#endif
            return false;
        }
    }
}
