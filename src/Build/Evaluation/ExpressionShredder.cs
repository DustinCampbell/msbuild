// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// What the shredder should be looking for.
    /// </summary>
    [Flags]
    internal enum ShredderOptions
    {
        /// <summary>
        /// Don't use
        /// </summary>
        Invalid = 0x0,

        /// <summary>
        /// Shred item types
        /// </summary>
        ItemTypes = 0x1,

        /// <summary>
        /// Shred metadata not contained inside of a transform.
        /// </summary>
        MetadataOutsideTransforms = 0x2,

        /// <summary>
        /// Shred both items and metadata not contained in a transform.
        /// </summary>
        All = ItemTypes | MetadataOutsideTransforms
    }

    /// <summary>
    /// A class which interprets and splits MSBuild expressions
    /// </summary>
    internal static partial class ExpressionShredder
    {
        /// <summary>
        ///  The marker that can begin a property expression.
        /// </summary>
        public const string PropertyMarker = "$(";

        /// <summary>
        ///  The marker that can begin an item-vector expression.
        /// </summary>
        public const string ItemVectorMarker = "@(";

        /// <summary>
        ///  The marker that can begin a metadata expression.
        /// </summary>
        public const string MetadataMarker = "%(";

        private static readonly char[] s_itemVectorOrMetadataMarkerPrefixes = ['@', '%'];

        /// <summary>
        ///  Determines whether <paramref name="expression"/> contains a property marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a property marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsPropertyMarker(string expression)
            => ContainsPropertyMarker(expression.AsSegment());

        /// <inheritdoc cref="ContainsPropertyMarker(string)"/>
        /// <param name="expression">The expression segment to scan.</param>
        public static bool ContainsPropertyMarker(StringSegment expression)
            => IndexOfPropertyMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether <paramref name="expression"/> contains an item-vector marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if an item-vector marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsItemVectorMarker(string expression)
            => ContainsItemVectorMarker(expression.AsSegment());

        /// <inheritdoc cref="ContainsItemVectorMarker(string)"/>
        /// <param name="expression">The expression segment to scan.</param>
        public static bool ContainsItemVectorMarker(StringSegment expression)
            => IndexOfItemVectorMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether <paramref name="expression"/> contains a metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a metadata marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsMetadataMarker(string expression)
            => ContainsMetadataMarker(expression.AsSegment());

        /// <inheritdoc cref="ContainsMetadataMarker(string)"/>
        /// <param name="expression">The expression segment to scan.</param>
        public static bool ContainsMetadataMarker(StringSegment expression)
            => IndexOfMetadataMarker(expression) >= 0;

        /// <summary>
        ///  Finds the first property marker.
        /// </summary>
        /// <remarks>
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyMarker(string expression)
            => IndexOfPropertyMarker(expression.AsSegment());

        /// <inheritdoc cref="IndexOfPropertyMarker(string)"/>
        /// <param name="expression">The expression segment to scan.</param>
        public static int IndexOfPropertyMarker(StringSegment expression)
            => IndexOfMarker(expression, '$');

        /// <summary>
        ///  Finds the first property marker at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <inheritdoc cref="IndexOfPropertyMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyMarker(string expression, int startIndex)
            => IndexOfPropertyMarker(expression.AsSegment(), startIndex);

        /// <inheritdoc cref="IndexOfPropertyMarker(string, int)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        public static int IndexOfPropertyMarker(StringSegment expression, int startIndex)
            => IndexOfMarker(expression, '$', startIndex, expression.Length - startIndex);

        /// <summary>
        ///  Finds the first property marker in the range beginning at <paramref name="startIndex"/>
        ///  and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <remarks>
        ///  Both characters of the marker must be contained within the specified range.
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyMarker(string expression, int startIndex, int count)
            => IndexOfPropertyMarker(expression.AsSegment(), startIndex, count);

        /// <inheritdoc cref="IndexOfPropertyMarker(string, int, int)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        public static int IndexOfPropertyMarker(StringSegment expression, int startIndex, int count)
            => IndexOfMarker(expression, '$', startIndex, count);

        /// <summary>
        ///  Finds the first item-vector marker.
        /// </summary>
        /// <remarks>
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfItemVectorMarker(string expression)
            => IndexOfItemVectorMarker(expression.AsSegment());

        /// <inheritdoc cref="IndexOfItemVectorMarker(string)"/>
        /// <param name="expression">The expression segment to scan.</param>
        public static int IndexOfItemVectorMarker(StringSegment expression)
            => IndexOfMarker(expression, '@');

        /// <summary>
        ///  Finds the first item-vector marker at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <remarks>
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfItemVectorMarker(string expression, int startIndex)
            => IndexOfItemVectorMarker(expression.AsSegment(), startIndex);

        /// <inheritdoc cref="IndexOfItemVectorMarker(string, int)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        public static int IndexOfItemVectorMarker(StringSegment expression, int startIndex)
            => IndexOfMarker(expression, '@', startIndex, expression.Length - startIndex);

        /// <summary>
        ///  Finds the first item-vector marker in the range beginning at <paramref name="startIndex"/>
        ///  and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <remarks>
        ///  Both characters of the marker must be contained within the specified range.
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfItemVectorMarker(string expression, int startIndex, int count)
            => IndexOfItemVectorMarker(expression.AsSegment(), startIndex, count);

        /// <inheritdoc cref="IndexOfItemVectorMarker(string, int, int)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        public static int IndexOfItemVectorMarker(StringSegment expression, int startIndex, int count)
            => IndexOfMarker(expression, '@', startIndex, count);

        /// <summary>
        ///  Finds the first metadata marker.
        /// </summary>
        /// <remarks>
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfMetadataMarker(string expression)
            => IndexOfMetadataMarker(expression.AsSegment());

        /// <inheritdoc cref="IndexOfMetadataMarker(string)"/>
        /// <param name="expression">The expression segment to scan.</param>
        public static int IndexOfMetadataMarker(StringSegment expression)
            => IndexOfMarker(expression, '%');

        /// <summary>
        ///  Finds the first metadata marker at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <remarks>
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfMetadataMarker(string expression, int startIndex)
            => IndexOfMetadataMarker(expression.AsSegment(), startIndex);

        /// <inheritdoc cref="IndexOfMetadataMarker(string, int)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        public static int IndexOfMetadataMarker(StringSegment expression, int startIndex)
            => IndexOfMarker(expression, '%', startIndex, expression.Length - startIndex);

        /// <summary>
        ///  Finds the first metadata marker in the range beginning at <paramref name="startIndex"/>
        ///  and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <remarks>
        ///  Both characters of the marker must be contained within the specified range.
        ///  This method does not validate the expression or locate its closing parenthesis.
        /// </remarks>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfMetadataMarker(string expression, int startIndex, int count)
            => IndexOfMetadataMarker(expression.AsSegment(), startIndex, count);

        /// <inheritdoc cref="IndexOfMetadataMarker(string, int, int)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        public static int IndexOfMetadataMarker(StringSegment expression, int startIndex, int count)
            => IndexOfMarker(expression, '%', startIndex, count);

        private static int IndexOfMarker(StringSegment expression, char marker)
        {
            // IndexOf(char) is significantly faster than an ordinal two-character search,
            // especially when the marker is absent, so check the opening parenthesis separately.
            string buffer = expression.Buffer;
            int offset = expression.Offset;
            int markerIndex = buffer.IndexOf(marker, offset, expression.Length);
            if (markerIndex < 0 || markerIndex == offset + expression.Length - 1)
            {
                return -1;
            }

            markerIndex -= offset;
            do
            {
                int nextIndex = markerIndex + 1;
                if (expression[nextIndex] == '(')
                {
                    return markerIndex;
                }

                markerIndex = buffer.IndexOf(
                    marker,
                    offset + nextIndex,
                    expression.Length - nextIndex);
                markerIndex = markerIndex < 0 ? -1 : markerIndex - offset;
            }
            while (markerIndex >= 0 && markerIndex < expression.Length - 1);

            return -1;
        }

        private static int IndexOfMarker(StringSegment expression, char marker, int startIndex, int count)
        {
            // IndexOf(char, int, int) is significantly faster than an ordinal two-character search,
            // especially when the marker is absent, so check the opening parenthesis separately.
            string buffer = expression.Buffer;
            int offset = expression.Offset;
            int markerIndex = buffer.IndexOf(marker, offset + startIndex, count);
            if (markerIndex < 0)
            {
                return -1;
            }

            markerIndex -= offset;
            int endIndex = startIndex + count;
            if (markerIndex == endIndex - 1)
            {
                return -1;
            }

            do
            {
                int nextIndex = markerIndex + 1;
                if (expression[nextIndex] == '(')
                {
                    return markerIndex;
                }

                markerIndex = buffer.IndexOf(
                    marker,
                    offset + nextIndex,
                    endIndex - nextIndex);
                markerIndex = markerIndex < 0 ? -1 : markerIndex - offset;
            }
            while (markerIndex >= 0 && markerIndex < endIndex - 1);

            return -1;
        }

        /// <summary>
        /// Splits an expression into fragments at semi-colons, except where the
        /// semi-colons are in a macro or separator expression.
        /// Fragments are trimmed and empty fragments discarded.
        /// </summary>
        /// <remarks>
        /// See <see cref="SemiColonTokenizer"/> for rules.
        /// </remarks>
        /// <param name="expression">List expression to split</param>
        /// <returns>Array of non-empty strings from split list.</returns>
        internal static SemiColonTokenizer SplitSemiColonSeparatedList(string expression)
            => SplitSemiColonSeparatedList(expression.AsSegment());

        /// <inheritdoc cref="SplitSemiColonSeparatedList(string)"/>
        /// <param name="expression">List expression segment to split.</param>
        internal static SemiColonTokenizer SplitSemiColonSeparatedList(StringSegment expression)
            => new(expression);

        /// <summary>
        /// Given a list of expressions that may contain item list expressions,
        /// returns a pair of tables of all item names found, as K=Name, V=String.Empty;
        /// and all metadata not in transforms, as K=Metadata key, V=MetadataReference,
        /// where metadata key is like "itemname.metadataname" or "metadataname".
        /// PERF: Tables are null if there are no entries, because this is quite a common case.
        /// </summary>
        internal static ItemsAndMetadataPair GetReferencedItemNamesAndMetadata(IReadOnlyList<string> expressions)
        {
            ItemsAndMetadataPair pair = new();

            // PERF: Use for to avoid boxing expressions enumerator
            for (int i = 0; i < expressions.Count; i++)
            {
                string expression = expressions[i];
                GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref pair, ShredderOptions.All);
            }

            return pair;
        }

        /// <summary>
        /// Returns true if there is a metadata expression (outside of a transform) in the expression.
        /// </summary>
        internal static bool ContainsMetadataExpressionOutsideTransform(string expression)
        {
            ItemsAndMetadataPair pair = new();

            GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref pair, ShredderOptions.MetadataOutsideTransforms);

            bool result = pair.Metadata.Count > 0;

            return result;
        }

        /// <inheritdoc cref="TryGetNextItemVectorExpression(string, int, out ItemVector)"/>
        public static bool TryGetNextItemVectorExpression(string expression, out ItemVector itemVector)
            => TryGetNextItemVectorExpression(expression.AsSegment(), startIndex: 0, out itemVector);

        /// <inheritdoc cref="TryGetNextItemVectorExpression(string, out ItemVector)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="itemVector">The parsed item-vector expression.</param>
        public static bool TryGetNextItemVectorExpression(StringSegment expression, out ItemVector itemVector)
            => TryGetNextItemVectorExpression(expression, startIndex: 0, out itemVector);

        /// <summary>
        ///  Finds and parses the next valid item-vector expression at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="itemVector">The parsed item-vector expression.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid item-vector expression is found; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        public static bool TryGetNextItemVectorExpression(string expression, int startIndex, out ItemVector itemVector)
            => TryGetNextItemVectorExpression(expression.AsSegment(), startIndex, out itemVector);

        /// <inheritdoc cref="TryGetNextItemVectorExpression(string, int, out ItemVector)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="itemVector">The parsed item-vector expression.</param>
        public static bool TryGetNextItemVectorExpression(StringSegment expression, int startIndex, out ItemVector itemVector)
        {
            while ((startIndex = IndexOfItemVectorMarker(expression, startIndex)) >= 0)
            {
                if (TryParseItemVectorExpression(expression, startIndex, out itemVector))
                {
                    return true;
                }

                startIndex += 2;
            }

            itemVector = default;
            return false;
        }

        private static bool TryParseItemVectorExpression(StringSegment expression, int startIndex, out ItemVector itemVector)
        {
            int end = expression.Length;
            int index = startIndex + 2;

            SinkWhitespace(expression, ref index);

            int startOfName = index;

            if (!SinkValidName(expression, ref index, end))
            {
                itemVector = default;
                return false;
            }

            // '-' is a legitimate char in an item name, but we should match '->' as an arrow
            // in '@(foo->'x')' rather than as the last char of the item name.
            // The old regex accomplished this by being "greedy"
            if (end > index && expression[index - 1] == '-' && expression[index] == '>')
            {
                index--;
            }

            // Component ranges are relative to the complete item-vector text retained below.
            Range itemTypeRange = (startOfName - startIndex)..(index - startIndex);

            SinkWhitespace(expression, ref index);
            TransformsBuilder transforms = default;

            // If there's an '->' eat it and the subsequent quoted expression or transform function
            while (Sink(expression, ref index, end, '-', '>'))
            {
                SinkWhitespace(expression, ref index);
                int startTransform = index;

                if (SinkSingleQuotedExpression(expression, ref index, end))
                {
                    int startQuoted = startTransform + 1;
                    int endQuoted = index - 1;
                    transforms.Add(ItemTransform.CreateQuotedExpressionTransform(
                        expression[startQuoted..endQuoted]));
                    SinkWhitespace(expression, ref index);
                    continue;
                }

                startTransform = index;
                if (TryParseFunctionTransform(expression, startTransform, ref index, end, out ItemTransform transform))
                {
                    transforms.Add(transform);

                    SinkWhitespace(expression, ref index);
                    continue;
                }

                itemVector = default;
                return false;
            }

            SinkWhitespace(expression, ref index);

            Range? separatorRange = null;
            // If there's a ',', eat it and the subsequent quoted expression
            if (Sink(expression, ref index, ','))
            {
                SinkWhitespace(expression, ref index);

                if (!Sink(expression, ref index, '\''))
                {
                    itemVector = default;
                    return false;
                }

                int closingQuote = expression.IndexOf('\'', index);
                if (closingQuote == -1)
                {
                    itemVector = default;
                    return false;
                }

                separatorRange = (index - startIndex)..(closingQuote - startIndex);

                index = closingQuote + 1;
            }

            SinkWhitespace(expression, ref index);

            if (!Sink(expression, ref index, ')'))
            {
                itemVector = default;
                return false;
            }

            // Preserve one segment for the complete item vector and relative ranges for its parsed components.
            itemVector = new ItemVector(
                text: expression[startIndex..index],
                index: startIndex,
                itemTypeRange,
                separatorRange,
                transforms.ToImmutable());

            return true;
        }

        /// <summary>
        ///  Parses an item-vector expression known to begin at <paramref name="startIndex"/>.
        /// </summary>
        /// <param name="expression">The expression segment.</param>
        /// <param name="startIndex">The index of the <c>@(</c> marker.</param>
        /// <param name="itemVector">The parsed item-vector expression.</param>
        /// <returns>
        ///  <see langword="true"/> when a valid item vector begins at the specified index; otherwise,
        ///  <see langword="false"/>.
        /// </returns>
        internal static bool TryGetItemVectorExpressionAt(
            StringSegment expression,
            int startIndex,
            out ItemVector itemVector)
            => TryParseItemVectorExpression(expression, startIndex, out itemVector);

        /// <summary>
        /// Given a subexpression, finds referenced item names and inserts them into the table
        /// as K=Name, V=String.Empty.
        /// </summary>
        /// <remarks>
        /// We can ignore any semicolons in the expression, since we're not itemizing it.
        /// </remarks>
        internal static void GetReferencedItemNamesAndMetadata(string expression, int start, int end, ref ItemsAndMetadataPair pair, ShredderOptions whatToShredFor)
        {
            int markerIndex = expression.IndexOfAny(
                s_itemVectorOrMetadataMarkerPrefixes,
                start,
                end - start);

            if (markerIndex >= 0)
            {
                GetReferencedItemNamesAndMetadata(
                    expression.AsSegment(),
                    start,
                    end,
                    ref pair,
                    whatToShredFor,
                    markerIndex);
            }
        }

        /// <inheritdoc cref="GetReferencedItemNamesAndMetadata(string, int, int, ref ItemsAndMetadataPair, ShredderOptions)"/>
        /// <param name="expression">The expression segment to scan.</param>
        /// <param name="start">The index at which to begin scanning.</param>
        /// <param name="end">The exclusive end index of the scan.</param>
        /// <param name="pair">The accumulated item and metadata references.</param>
        /// <param name="whatToShredFor">The reference kinds to collect.</param>
        internal static void GetReferencedItemNamesAndMetadata(StringSegment expression, int start, int end, ref ItemsAndMetadataPair pair, ShredderOptions whatToShredFor)
        {
            int markerIndex = IndexOfItemOrMetadataMarker(expression, start, end);
            if (markerIndex >= 0)
            {
                GetReferencedItemNamesAndMetadata(
                    expression,
                    start,
                    end,
                    ref pair,
                    whatToShredFor,
                    markerIndex);
            }
        }

        private static void GetReferencedItemNamesAndMetadata(
            StringSegment expression,
            int start,
            int end,
            ref ItemsAndMetadataPair pair,
            ShredderOptions whatToShredFor,
            int markerIndex)
        {
            int i = start;

            while (i < end)
            {
                if (markerIndex < i)
                {
                    markerIndex = IndexOfItemOrMetadataMarker(expression, i, end);
                }

                if (markerIndex < 0)
                {
                    break;
                }

                char markerPrefix = expression[markerIndex];
                i = markerIndex + 1;
                markerIndex = -1;

                if (i >= end)
                {
                    break;
                }

                if (expression[i] != '(')
                {
                    continue;
                }

                i++;
                int restartPoint = i;

                if (markerPrefix == '@')
                {
                    // Start of a possible item list expression

                    SinkWhitespace(expression, ref i);

                    int startOfName = i;

                    if (!SinkValidName(expression, ref i, end))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // '-' is a legitimate char in an item name, but we should match '->' as an arrow
                    // in '@(foo->'x')' rather than as the last char of the item name.
                    // The old regex accomplished this by being "greedy"
                    if (end > i && expression[i - 1] == '-' && expression[i] == '>')
                    {
                        i--;
                    }

                    // Grab the name boundaries, but continue to verify it's a well-formed expression
                    // before we store it.
                    int nameLength = i - startOfName;

                    SinkWhitespace(expression, ref i);

                    bool transformOrFunctionFound = true;

                    // If there's an '->' eat it and the subsequent quoted expression or transform function
                    while (Sink(expression, ref i, end, '-', '>') && transformOrFunctionFound)
                    {
                        SinkWhitespace(expression, ref i);
                        int startTransform = i;

                        bool isQuotedTransform = SinkSingleQuotedExpression(expression, ref i, end);
                        if (isQuotedTransform)
                        {
                            SinkWhitespace(expression, ref i);
                            continue;
                        }

                        if (TryParseFunctionTransform(expression, startTransform, ref i, end, out _))
                        {
                            SinkWhitespace(expression, ref i);
                            continue;
                        }

                        i = restartPoint;
                        transformOrFunctionFound = false;
                    }

                    if (!transformOrFunctionFound)
                    {
                        continue;
                    }

                    SinkWhitespace(expression, ref i);

                    // If there's a ',', eat it and the subsequent quoted expression
                    if (Sink(expression, ref i, ','))
                    {
                        SinkWhitespace(expression, ref i);

                        if (!Sink(expression, ref i, '\''))
                        {
                            i = restartPoint;
                            continue;
                        }

                        int closingQuote = expression.IndexOf('\'', i);
                        if (closingQuote == -1)
                        {
                            i = restartPoint;
                            continue;
                        }

                        // Look for metadata in the separator expression
                        // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
                        GetReferencedItemNamesAndMetadata(expression, i, closingQuote, ref pair, ShredderOptions.MetadataOutsideTransforms);

                        i = closingQuote + 1;
                    }

                    SinkWhitespace(expression, ref i);

                    if (!Sink(expression, ref i, ')'))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // If we've got this far, we know the item expression was
                    // well formed, so make sure the name's in the table
                    if ((whatToShredFor & ShredderOptions.ItemTypes) != 0)
                    {
                        pair.AddItemType(expression[startOfName..(startOfName + nameLength)]);
                    }

                    continue;
                }

                // Start of a possible metadata expression

                if (!TryParseMetadataExpression(expression, ref i, end, out StringSegment itemName, out StringSegment metadataName))
                {
                    i = restartPoint;
                    continue;
                }

                if ((whatToShredFor & ShredderOptions.MetadataOutsideTransforms) != 0)
                {
                    pair.AddMetadata(new MetadataReference(itemName, metadataName));
                }
            }
        }

        private static int IndexOfItemOrMetadataMarker(
            StringSegment expression,
            int start,
            int end)
        {
            int markerIndex = expression.Buffer.IndexOfAny(
                s_itemVectorOrMetadataMarkerPrefixes,
                expression.Offset + start,
                end - start);

            return markerIndex < 0 ? -1 : markerIndex - expression.Offset;
        }

        /// <summary>
        ///  Attempts to parse a metadata expression of the form <c>%(Name)</c> or <c>%(ItemType.Name)</c>,
        ///  starting just after the <c>%(</c> has been consumed (i.e., <paramref name="i"/> points at
        ///  the first character after the opening parenthesis).
        /// </summary>
        /// <remarks>
        ///  On success, <paramref name="i"/> is left one past the closing <c>)</c>.
        ///  On failure, <paramref name="i"/> is at an indeterminate position and the caller
        ///  should restore it from a saved restart point.
        /// </remarks>
        /// <param name="expression">The expression segment being scanned.</param>
        /// <param name="i">Current scan position (just after <c>%(</c>). Advanced on success.</param>
        /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
        /// <param name="itemType">The item type if qualified; otherwise <see langword="null"/>.</param>
        /// <param name="metadataName">The metadata name.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid metadata expression was parsed.
        /// </returns>
        internal static bool TryParseMetadataExpression(
            StringSegment expression,
            ref int i,
            int end,
            out StringSegment itemType,
            out StringSegment metadataName)
        {
            itemType = default;
            metadataName = default;

            SinkWhitespace(expression, ref i, end);

            int startOfText = i;

            if (!SinkValidName(expression, ref i, end))
            {
                return false;
            }

            StringSegment firstName = expression[startOfText..i];

            SinkWhitespace(expression, ref i, end);

            if (Sink(expression, ref i, end, '.'))
            {
                // Qualified: %(ItemType.Name)
                itemType = firstName;

                SinkWhitespace(expression, ref i, end);

                startOfText = i;

                if (!SinkValidName(expression, ref i, end))
                {
                    return false;
                }

                metadataName = expression[startOfText..i];

                SinkWhitespace(expression, ref i, end);
            }
            else
            {
                // Unqualified: %(Name)
                metadataName = firstName;
            }

            return Sink(expression, ref i, end, ')');
        }

        /// <summary>
        /// Returns true if a single quoted subexpression begins at the specified index
        /// and ends before the specified end index.
        /// Leaves index one past the end of the second quote.
        /// </summary>
        private static bool SinkSingleQuotedExpression(StringSegment expression, ref int i, int end)
        {
            if (!Sink(expression, ref i, '\''))
            {
                return false;
            }

            while (i < end && expression[i] != '\'')
            {
                i++;
            }

            i++;

            if (end <= i)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Scan for the closing bracket that matches the one we've already skipped;
        /// essentially, pushes and pops on a stack of parentheses to do this.
        /// Takes the expression and the index to start at.
        /// Returns the index of the matching parenthesis, or -1 if it was not found.
        /// </summary>
        private static bool SinkArgumentsInParentheses(StringSegment expression, ref int i, int end)
        {
            int nestLevel = 0;
            int length = expression.Length;
            int restartPoint;

            if (expression[i] == '(')
            {
                nestLevel++;
                i++;
            }
            else
            {
                return false;
            }

            // Scan for our closing ')'.
            while (i < length && i < end && nestLevel > 0)
            {
                char character = expression[i];

                if (character == '\'' || character == '`' || character == '"')
                {
                    restartPoint = i;
                    if (!SinkUntilClosingQuote(character, expression, ref i, end))
                    {
                        i = restartPoint;
                        return false;
                    }
                }
                else if (character == '(')
                {
                    nestLevel++;
                }
                else if (character == ')')
                {
                    nestLevel--;
                }

                i++;
            }

            return nestLevel == 0;
        }

        /// <summary>
        /// Skip all characters until we find the matching quote character
        /// </summary>
        private static bool SinkUntilClosingQuote(char quoteChar, StringSegment expression, ref int i, int end)
        {
            // We have already checked the first quote.
            i++;

            int closingQuote = expression.IndexOf(quoteChar, i, end - i);
            if (closingQuote >= 0)
            {
                i = closingQuote;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if a item function subexpression begins at the specified index
        /// and ends before the specified end index.
        /// Leaves index one past the end of the closing paren.
        /// </summary>
        private static bool TryParseFunctionTransform(StringSegment expression, int startTransform, ref int i, int end, out ItemTransform transform)
        {
            if (SinkValidName(expression, ref i, end))
            {
                int endFunctionName = i;

                // Eat any whitespace between the function name and its arguments
                SinkWhitespace(expression, ref i);
                int startFunctionArguments = i + 1;

                if (SinkArgumentsInParentheses(expression, ref i, end))
                {
                    int endFunctionArguments = i - 1;

                    Range? functionArgumentsRange = null;
                    if (endFunctionArguments > startFunctionArguments)
                    {
                        functionArgumentsRange =
                            (startFunctionArguments - startTransform)..(endFunctionArguments - startTransform);
                    }

                    transform = ItemTransform.CreateFunctionTransform(
                        expression[startTransform..i],
                        endFunctionName - startTransform,
                        functionArgumentsRange);
                    return true;
                }
            }

            transform = default;
            return false;
        }

        /// <summary>
        /// Returns true if a valid name begins at the specified index.
        /// Leaves index one past the end of the name.
        /// </summary>
        /// <remarks>
        /// The accepted grammar is <c>[A-Za-z_][A-Za-z_0-9\-]*</c> (via
        /// <see cref="XmlUtilities.IsValidInitialElementNameCharacter"/> and
        /// <see cref="XmlUtilities.IsValidSubsequentElementNameCharacter"/>), which defines a valid item
        /// type or metadata name. This MUST be kept in sync with
        /// <see cref="ProjectWriter.itemTypeOrMetadataNameSpecification"/>: if the grammar used to parse
        /// item/metadata expressions diverges from the one used to write them back out, expressions could
        /// round-trip incorrectly.
        /// </remarks>
        private static bool SinkValidName(StringSegment expression, ref int i, int end)
        {
            if (end <= i || !XmlUtilities.IsValidInitialElementNameCharacter(expression[i]))
            {
                return false;
            }

            i++;

            while (end > i && XmlUtilities.IsValidSubsequentElementNameCharacter(expression[i]))
            {
                i++;
            }

            return true;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the character at the specified index is the specified char.
        ///  Leaves index one past the character.
        /// </summary>
        private static bool Sink(StringSegment expression, ref int i, char c)
            => Sink(expression, ref i, expression.Length, c);

        /// <summary>
        ///  Returns <see langword="true"/> if the character at the specified index (which must be before
        ///  <paramref name="end"/>) is the specified char. Leaves index one past the character.
        /// </summary>
        private static bool Sink(StringSegment expression, ref int i, int end, char c)
        {
            if (i < end && expression[i] == c)
            {
                i++;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the next two characters at the specified index are the specified sequence.
        ///  Leaves index one past the second character.
        /// </summary>
        private static bool Sink(StringSegment expression, ref int i, int end, char c1, char c2)
        {
            if (i < end - 1 && expression[i] == c1 && expression[i + 1] == c2)
            {
                i += 2;
                return true;
            }

            return false;
        }

        /// <summary>
        ///  Moves past all whitespace starting at the specified index.
        ///  Returns the next index, possibly the string length.
        /// </summary>
        /// <param name="expression">The expression to process.</param>
        /// <param name="i">The start location for skipping whitespace, contains the next non-whitespace character on exit.</param>
        /// <remarks>
        ///  <see cref="char.IsWhiteSpace(char)"/> is not identical in behavior to regex's <c>\s</c> character class,
        ///  but it's extremely close, and it's what we use in conditional expressions.
        /// </remarks>
        private static void SinkWhitespace(StringSegment expression, ref int i)
            => SinkWhitespace(expression, ref i, expression.Length);

        /// <summary>
        ///  Moves past all whitespace starting at the specified index, without scanning at or beyond
        ///  <paramref name="end"/>. Returns the next index, possibly <paramref name="end"/>.
        /// </summary>
        /// <param name="expression">The expression to process.</param>
        /// <param name="i">
        ///  The start location for skipping whitespace, contains the next non-whitespace character (or <paramref name="end"/>) on exit.
        /// </param>
        /// <param name="end">Exclusive end index of the scan range.</param>
        /// <remarks>
        ///  <see cref="char.IsWhiteSpace(char)"/> is not identical in behavior to regex's <c>\s</c> character class,
        ///  but it's extremely close, and it's what we use in conditional expressions.
        /// </remarks>
        private static void SinkWhitespace(StringSegment expression, ref int i, int end)
        {
            while (i < end && char.IsWhiteSpace(expression[i]))
            {
                i++;
            }
        }
    }
}
