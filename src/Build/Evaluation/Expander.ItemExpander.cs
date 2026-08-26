// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
#if !NET
using System.Text;
#endif
using Microsoft.Build.Evaluation.Expander;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
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
        ///  Each captured transform function will be mapped to either a static method on
        ///  <see cref="Transforms"/> or a known item spec modifier which operates on the item path.
        ///  </para>
        ///  <para>
        ///  For each function, the full list of items will be iteratively transformed using the
        ///  output of the previous. E.g. given functions f, g, h, the order of operations will
        ///  look like: <c>results = h(g(f(items)))</c>.
        ///  </para>
        ///  <para>
        ///  If no function name is found, we default to
        ///  the quoted-expression transform.
        ///  </para>
        /// </remarks>
        /// <returns>
        ///  <see langword="true"/> if the transform completed successfully; <see langword="false"/> if
        ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> was set and the result is non-empty.
        /// </returns>
        private static bool TryTransform(
            ExpansionContext context,
            bool includeNullEntries,
            List<ExpressionShredder.ItemExpressionCapture> captures,
            ICollection<I> itemsOfType,
            out List<TransformEntry> result)
        {
            ErrorReporter errors = context.Errors;

            // Each transform runs on the full set of transformed items from the previous result.
            // We can reuse our buffers by just swapping the references after each transform.
            List<TransformEntry> input = CreateEntries(itemsOfType);
            List<TransformEntry> output = new(itemsOfType.Count);

            // Create a TransformFunction for each transform in the chain by extracting the relevant information
            // from the regex parsing results
            for (int i = 0; i < captures.Count; i++)
            {
                ExpressionShredder.ItemExpressionCapture capture = captures[i];
                string function = capture.Value;
                string functionName = capture.FunctionName;
                string argumentsExpression = capture.FunctionArguments;

                string[] arguments = null;
                TransformKind kind;

                // Quoted transforms have no function name. Select their kind directly to avoid synthesizing
                // a function name and one-element argument array, then performing name-based dispatch.
                if (functionName is null)
                {
                    kind = TransformKind.ExpandQuotedExpressionFunction;
                }
                else
                {
                    ArgumentList parsedArguments = PropertyFunctionParser.ParseArguments(
                        argumentsExpression,
                        function,
                        context.Location);
                    arguments = new string[parsedArguments.Count];
                    for (int argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
                    {
                        arguments[argumentIndex] = parsedArguments[argumentIndex].Value;
                    }

                    if (ItemSpecModifiers.IsDerivableItemSpecModifier(functionName))
                    {
                        kind = TransformKind.ItemSpecModifierFunction;
                    }
                    else if (!s_intrinsicTransforms.TryGetValue(functionName, out kind))
                    {
                        kind = TransformKind.ExecuteStringFunction;
                    }
                }

                switch (kind)
                {
                    case TransformKind.ItemSpecModifierFunction:
                        Transforms.ItemSpecModifierFunction(
                            input,
                            output,
                            arguments,
                            includeNullEntries,
                            functionName,
                            errors);
                        break;
                    case TransformKind.Count:
                        Transforms.Count(input, output);
                        break;
                    case TransformKind.Exists:
                        Transforms.Exists(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.Combine:
                        Transforms.Combine(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.GetPathsOfAllDirectoriesAbove:
                        Transforms.GetPathsOfAllDirectoriesAbove(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.DirectoryName:
                        Transforms.DirectoryName(input, output, arguments, includeNullEntries, functionName, errors);
                        break;
                    case TransformKind.Metadata:
                        Transforms.Metadata(input, output, arguments, includeNullEntries, functionName, errors);
                        break;
                    case TransformKind.DistinctWithCase:
                        Transforms.DistinctWithCase(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.Distinct:
                        Transforms.Distinct(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.Reverse:
                        Transforms.Reverse(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.ExpandQuotedExpressionFunction:
                        // The unnamed form stores the quoted expression in capture.Value. An explicitly named
                        // invocation uses parsed arguments so its existing syntax validation is preserved.
                        if (functionName is null)
                        {
                            Transforms.ExpandQuotedExpressionFunction(
                                input,
                                output,
                                function,
                                includeNullEntries,
                                errors);
                        }
                        else
                        {
                            Transforms.ExpandQuotedExpressionFunction(
                                input,
                                output,
                                arguments,
                                includeNullEntries,
                                functionName,
                                errors);
                        }

                        break;
                    case TransformKind.ExecuteStringFunction:
                        Transforms.ExecuteStringFunction(
                            context,
                            input,
                            output,
                            arguments,
                            includeNullEntries,
                            functionName);
                        break;
                    case TransformKind.ClearMetadata:
                        Transforms.ClearMetadata(input, output, arguments, includeNullEntries, functionName, errors);
                        break;
                    case TransformKind.HasMetadata:
                        Transforms.HasMetadata(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.WithMetadataValue:
                        Transforms.WithMetadataValue(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.WithoutMetadataValue:
                        Transforms.WithoutMetadataValue(input, output, arguments, functionName, errors);
                        break;
                    case TransformKind.AnyHaveMetadataValue:
                        Transforms.AnyHaveMetadataValue(input, output, arguments, functionName, errors);
                        break;
                    default:
                        errors.UnknownItemFunction.Throw(functionName);
                        break;
                }

                // If we have another transform, swap the source and transform lists.
                if (i < captures.Count - 1)
                {
                    (output, input) = (input, output);
                    output.Clear();
                }
            }

            // Check for break on non-empty only after ALL transforms are complete
            if ((context.Options & ExpanderOptions.BreakOnNotEmpty) != 0)
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
            string expression,
            IItemFactory<I, T> itemFactory,
            bool includeNullEntries,
            out bool isTransformExpression,
            ExpansionContext context)
            where T : class, IItem
        {
            isTransformExpression = false;

            return TryExpandSingleItemVectorExpression(
                expression,
                context.Options,
                context.Errors,
                out ExpressionShredder.ItemExpressionCapture itemVector)
                ? ExpandExpressionCaptureIntoItems(
                    itemVector,
                    context.Items,
                    itemFactory,
                    includeNullEntries,
                    out isTransformExpression,
                    context)
                : null;
        }

        internal static bool TryExpandSingleItemVectorExpression(
            string expression,
            ExpanderOptions options,
            ErrorReporter errors,
            out ExpressionShredder.ItemExpressionCapture itemVector)
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
            errors.ItemVectorCannotBeItemized.ThrowIfFalse(
                itemVector.Index == 0 && itemVector.Length == expression.Length,
                expression);

            return true;
        }

        internal static IList<T> ExpandExpressionCaptureIntoItems<T>(
            ExpressionShredder.ItemExpressionCapture expressionCapture,
            IItemProvider<I> items,
            IItemFactory<I, T> itemFactory,
            bool includeNullEntries,
            out bool isTransformExpression,
            ExpansionContext context)
            where T : class, IItem
        {
            Assumed.NotNull(items, "Cannot expand items without providing items");
            isTransformExpression = false;
            bool brokeEarlyNonEmpty;

            string file = context.Location.File;

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
                brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(
                    expressionCapture,
                    items,
                    builder,
                    context);

                if (brokeEarlyNonEmpty)
                {
                    return null;
                }

                expandedItemVector = builder.ToString();

                result = Array.Empty<T>();

                if (expandedItemVector.Length > 0)
                {
                    T newItem = itemFactory.CreateItem(expandedItemVector, file);

                    result = [newItem];
                }

                return result;
            }

            List<TransformEntry> entries;
            brokeEarlyNonEmpty = ExpandItemVector(
                expressionCapture,
                items,
                includeNullEntries: true,
                out isTransformExpression,
                out entries,
                context);

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
                    result.Add(itemFactory.CreateItem(itemSpec, file));
                }
                else if (itemSpec != null && originalItem != null)
                {
                    if (itemSpec.Equals(originalItem.EvaluatedIncludeEscaped))
                    {
                        result.Add(itemFactory.CreateItem(originalItem, file));
                    }
                    else
                    {
                        result.Add(itemFactory.CreateItem(itemSpec, originalItem, file));
                    }
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
        /// <param name="itemVector">
        ///  The parsed item vector to expand.
        /// </param>
        /// <param name="evaluatedItems">The <see cref="IItemProvider{I}"/> that provides the items to expand.</param>
        /// <param name="includeNullEntries">Whether to include values that evaluate to <see langword="null"/>.</param>
        /// <param name="isTransformExpression">
        ///  <see langword="true"/> if the item vector contains a transform, even when its item list is empty.
        /// </param>
        /// <param name="entries">
        ///  The expanded entries, or <see langword="null"/> when the expression produces no entries.
        ///  <see cref="TransformEntry.Value"/> contains the escaped value, and <see cref="TransformEntry.Item"/>
        ///  identifies the item from which the value was derived, when available.
        /// </param>
        /// <param name="context">The context for the expansion operation.</param>
        /// <returns>
        ///  <see langword="true"/> if <see cref="ExpanderOptions.BreakOnNotEmpty"/> caused expansion to stop after
        ///  determining that the result would be non-empty; otherwise, <see langword="false"/>.
        /// </returns>
        internal static bool ExpandItemVector(
            ExpressionShredder.ItemExpressionCapture itemVector,
            IItemProvider<I> evaluatedItems,
            bool includeNullEntries,
            out bool isTransformExpression,
            out List<TransformEntry> entries,
            ExpansionContext context)
        {
            Assumed.NotNull(evaluatedItems, "Cannot expand items without providing items");

            // An empty item type indicates that the expression could not be parsed correctly.
            context.Errors.InvalidPropertyFunction.ThrowIfFalse(
                !itemVector.ItemType.IsNullOrEmpty(),
                itemVector.Value);

            ICollection<I> items = evaluatedItems.GetItems(itemVector.ItemType);
            List<ExpressionShredder.ItemExpressionCapture> captures = itemVector.Captures;
            string separator = itemVector.Separator;

            isTransformExpression = captures is not null;
            entries = null;

            if (!isTransformExpression)
            {
                // An empty item vector produces no entries.
                if (items.Count == 0)
                {
                    return false; // did not break early
                }

                bool breakOnNotEmpty = (context.Options & ExpanderOptions.BreakOnNotEmpty) != 0;

                // An explicit separator, such as @(Compile, ','), collapses the item vector into one scalar entry.
                if (separator is not null)
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
            if (items.Count == 0 && !ShouldEvaluateEmptyList(captures))
            {
                return false; // did not break early
            }

            // A transform item vector without any captures indicates that it could not be parsed correctly.
            context.Errors.InvalidPropertyFunction.ThrowIfFalse(captures.Count > 0, itemVector.Value);

            if (!TryTransform(context, includeNullEntries, captures, items, out entries))
            {
                return true; // broke early
            }

            if (separator is not null)
            {
                // An explicit separator collapses the transformed values into one scalar entry.
                string joinedItems = JoinEntries(separator, entries);

                entries.Clear();
                entries.Add(new TransformEntry(joinedItems, null));
            }

            return false; // did not break early

            static bool ShouldEvaluateEmptyList(List<ExpressionShredder.ItemExpressionCapture> captures)
            {
                // Count returns zero and AnyHaveMetadataValue returns false for an empty list, so those transforms must still run.
                foreach (ExpressionShredder.ItemExpressionCapture capture in captures)
                {
                    string functionName = capture.FunctionName;
                    if (string.Equals(functionName, "Count", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(functionName, "AnyHaveMetadataValue", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            static bool TryJoinItems(ICollection<I> items, string separator, bool breakOnNotEmpty, out string result)
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

                    builder.Append(separator);
                    builder.Append(evaluatedIncludeEscaped);
                }

#if NET
                result = builder.ToString();
#else
                result = StringBuilderCache.GetStringAndRelease(builder);
#endif
                return true; // joined successfully
            }

            static string JoinEntries(string separator, List<TransformEntry> entries)
            {
                if (entries.Count == 0)
                {
                    return string.Empty;
                }

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
                        builder.Append(separator);
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
        ///  Expands all item vectors embedded in an expression into a single string.
        /// </summary>
        /// <param name="expression">The expression containing item vectors.</param>
        /// <param name="context">The context for the expansion operation.</param>
        /// <returns>
        ///  The expanded string, or <see langword="null"/> when
        ///  <see cref="ExpanderOptions.BreakOnNotEmpty"/> stops expansion.
        /// </returns>
        internal static string ExpandItemVectorsIntoString(string expression, ExpansionContext context)
        {
            if ((context.Options & ExpanderOptions.ExpandItems) == 0 || expression.Length == 0)
            {
                return expression;
            }

            Assumed.NotNull(context.Items, "Cannot expand items without providing items");

            if (!ExpressionShredder.TryGetNextItemVectorExpression(expression, out ExpressionShredder.ItemExpressionCapture currentItem))
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
                if (currentItem.Index > lastStringIndex)
                {
                    if ((context.Options & ExpanderOptions.BreakOnNotEmpty) != 0)
                    {
                        return null;
                    }

                    builder.Append(expression, lastStringIndex, currentItem.Index - lastStringIndex);
                }

                bool brokeEarlyNonEmpty = ExpandExpressionCaptureIntoStringBuilder(
                    currentItem,
                    context.Items,
                    builder,
                    context);

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
        ///  Expands an item-vector capture and appends its values to a string builder.
        /// </summary>
        /// <param name="capture">The item-vector capture to expand.</param>
        /// <param name="evaluatedItems">The items available to the expansion.</param>
        /// <param name="builder">The builder that receives the expanded values.</param>
        /// <param name="context">The context for the expansion operation.</param>
        /// <returns>
        ///  <see langword="true"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/> stops expansion; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        private static bool ExpandExpressionCaptureIntoStringBuilder(
            ExpressionShredder.ItemExpressionCapture capture,
            IItemProvider<I> evaluatedItems,
            SpanBasedStringBuilder builder,
            ExpansionContext context)
        {
            List<TransformEntry> entries;
            var brokeEarlyNonEmpty = ExpandItemVector(
                capture,
                evaluatedItems,
                includeNullEntries: true,
                out _,
                out entries,
                context);

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
            bool truncate = IsTruncationEnabled(context.Options);

            // if the capture.Separator is not null, then ExpandExpressionCapture would have joined the items using that separator itself
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
    }
}
