// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

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
    internal static class ExpressionShredder
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
            => IndexOfPropertyMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether <paramref name="expression"/> contains an item-vector marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if an item-vector marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsItemVectorMarker(string expression)
            => IndexOfItemVectorMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether <paramref name="expression"/> contains a metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a metadata marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsMetadataMarker(string expression)
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
            => IndexOfMarker(expression, '%', startIndex, count);

        private static int IndexOfMarker(string expression, char marker)
        {
            // IndexOf(char) is significantly faster than an ordinal two-character search,
            // especially when the marker is absent, so check the opening parenthesis separately.
            int markerIndex = expression.IndexOf(marker);
            if (markerIndex < 0 || markerIndex == expression.Length - 1)
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

                markerIndex = expression.IndexOf(marker, nextIndex);
            }
            while (markerIndex >= 0 && markerIndex < expression.Length - 1);

            return -1;
        }

        private static int IndexOfMarker(string expression, char marker, int startIndex, int count)
        {
            // IndexOf(char, int, int) is significantly faster than an ordinal two-character search,
            // especially when the marker is absent, so check the opening parenthesis separately.
            int markerIndex = expression.IndexOf(marker, startIndex, count);
            if (markerIndex < 0)
            {
                return -1;
            }

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

                markerIndex = expression.IndexOf(marker, nextIndex, endIndex - nextIndex);
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
        {
            return new SemiColonTokenizer(expression);
        }

        /// <summary>
        /// Given a list of expressions that may contain item list expressions,
        /// returns a pair of tables of all item names found, as K=Name, V=String.Empty;
        /// and all metadata not in transforms, as K=Metadata key, V=MetadataReference,
        /// where metadata key is like "itemname.metadataname" or "metadataname".
        /// PERF: Tables are null if there are no entries, because this is quite a common case.
        /// </summary>
        internal static ItemsAndMetadataPair GetReferencedItemNamesAndMetadata(IReadOnlyList<string> expressions)
        {
            ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

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
            ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

            GetReferencedItemNamesAndMetadata(expression, 0, expression.Length, ref pair, ShredderOptions.MetadataOutsideTransforms);

            bool result = (pair.Metadata?.Count > 0);

            return result;
        }

        /// <inheritdoc cref="TryGetNextItemVector(string, int, out ItemVector)"/>
        public static bool TryGetNextItemVector(string expression, out ItemVector itemVector)
            => TryGetNextItemVector(expression, startIndex: 0, out itemVector);

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
        public static bool TryGetNextItemVector(string expression, int startIndex, out ItemVector itemVector)
        {
            while ((startIndex = IndexOfItemVectorMarker(expression, startIndex)) >= 0)
            {
                if (TryParseItemVector(expression, startIndex, out itemVector))
                {
                    return true;
                }

                startIndex += 2;
            }

            itemVector = default;
            return false;
        }

        private static bool TryParseItemVector(string expression, int startIndex, out ItemVector itemVector)
        {
            int end = expression.Length;
            int index = startIndex + 2;

            SinkWhitespace(expression, ref index);

            if (!TryParseValidName(expression, ref index, end, stopBeforeTransformArrow: true, out string itemType))
            {
                itemVector = default;
                return false;
            }

            SinkWhitespace(expression, ref index);
            using RefArrayBuilder<ItemTransform> transforms = default;

            // If there's an '->' eat it and the subsequent quoted expression or transform function
            while (Sink(expression, ref index, end, '-', '>'))
            {
                SinkWhitespace(expression, ref index);

                if (TryParseQuotedTransform(expression, ref index, end, out ItemTransform transform))
                {
                    transforms.Add(transform);

                    SinkWhitespace(expression, ref index);
                    continue;
                }

                if (TryParseFunctionTransform(expression, ref index, end, out transform))
                {
                    transforms.Add(transform);

                    SinkWhitespace(expression, ref index);
                    continue;
                }

                itemVector = default;
                return false;
            }

            SinkWhitespace(expression, ref index);

            string separator = null;
            int separatorStart = -1;

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

                separatorStart = index - startIndex;
                separator = expression.Substring(index, closingQuote - index);

                index = closingQuote + 1;
            }

            SinkWhitespace(expression, ref index);

            if (!Sink(expression, ref index, ')'))
            {
                itemVector = default;
                return false;
            }

            int length = index - startIndex;

            // Create an ItemVector that encompasses the entire expression delimited by @( and the )
            // with the item name and any separator contained within it
            // and each transform expression contained within it (i.e. each ->XYZ)
            itemVector = new ItemVector(
                text: Strings.WeakIntern(expression.AsSpan(startIndex, length)),
                index: startIndex,
                itemType,
                separator,
                separatorStart,
                transforms.ToImmutable());

            return true;
        }

        /// <summary>
        /// Given a subexpression, finds referenced item names and inserts them into the table
        /// as K=Name, V=String.Empty.
        /// </summary>
        /// <remarks>
        /// We can ignore any semicolons in the expression, since we're not itemizing it.
        /// </remarks>
        internal static void GetReferencedItemNamesAndMetadata(string expression, int start, int end, ref ItemsAndMetadataPair pair, ShredderOptions whatToShredFor)
        {
            int i = start;

            while (i < end)
            {
                int markerIndex = expression.IndexOfAny(s_itemVectorOrMetadataMarkerPrefixes, i, end - i);
                if (markerIndex < 0)
                {
                    break;
                }

                char markerPrefix = expression[markerIndex];
                i = markerIndex + 1;

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

                    SinkWhitespace(expression, ref i, end);

                    int startOfName = i;

                    if (!TryScanValidName(expression, ref i, end, stopBeforeTransformArrow: true))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // Grab the name boundaries, but continue to verify it's a well-formed expression
                    // before we store it.
                    int nameLength = i - startOfName;

                    SinkWhitespace(expression, ref i, end);

                    bool transformOrFunctionFound = true;

                    // If there's an '->' eat it and the subsequent quoted expression or transform function
                    while (Sink(expression, ref i, end, '-', '>') && transformOrFunctionFound)
                    {
                        SinkWhitespace(expression, ref i, end);

                        if (TryScanQuotedTransform(expression, ref i, end))
                        {
                            SinkWhitespace(expression, ref i, end);
                            continue;
                        }

                        if (TryScanFunctionTransform(expression, ref i, end))
                        {
                            SinkWhitespace(expression, ref i, end);
                            continue;
                        }

                        i = restartPoint;
                        transformOrFunctionFound = false;
                    }

                    if (!transformOrFunctionFound)
                    {
                        continue;
                    }

                    SinkWhitespace(expression, ref i, end);

                    // If there's a ',', eat it and the subsequent quoted expression
                    if (Sink(expression, ref i, ','))
                    {
                        SinkWhitespace(expression, ref i, end);

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

                    SinkWhitespace(expression, ref i, end);

                    if (!Sink(expression, ref i, ')'))
                    {
                        i = restartPoint;
                        continue;
                    }

                    // If we've got this far, we know the item expression was
                    // well formed, so make sure the name's in the table
                    if ((whatToShredFor & ShredderOptions.ItemTypes) != 0)
                    {
                        pair.Items ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                        pair.Items.Add(expression.Substring(startOfName, nameLength));
                    }

                    continue;
                }

                // Start of a possible metadata expression

                if (!TryParseMetadataExpression(expression, ref i, end, out string itemName, out string metadataName))
                {
                    i = restartPoint;
                    continue;
                }

                if ((whatToShredFor & ShredderOptions.MetadataOutsideTransforms) != 0)
                {
                    string qualifiedMetadataName = itemName != null ? $"{itemName}.{metadataName}" : metadataName;
                    pair.Metadata ??= new Dictionary<string, MetadataReference>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Metadata[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
                }
            }
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
        /// <param name="expression">The expression being scanned.</param>
        /// <param name="i">Current scan position (just after <c>%(</c>). Advanced on success.</param>
        /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
        /// <param name="itemType">The item type if qualified; otherwise <see langword="null"/>.</param>
        /// <param name="metadataName">The metadata name.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid metadata expression was parsed.
        /// </returns>
        internal static bool TryParseMetadataExpression(string expression, ref int i, int end, out string itemType, out string metadataName)
        {
            itemType = null;
            metadataName = null;

            SinkWhitespace(expression, ref i, end);

            if (!TryParseValidName(expression, ref i, end, out string firstName))
            {
                return false;
            }

            SinkWhitespace(expression, ref i, end);

            if (Sink(expression, ref i, end, '.'))
            {
                // Qualified: %(ItemType.Name)
                itemType = firstName;

                SinkWhitespace(expression, ref i, end);

                if (!TryParseValidName(expression, ref i, end, out metadataName))
                {
                    return false;
                }

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
        ///  Parses a quoted item transform beginning at the specified index.
        /// </summary>
        /// <param name="expression">The expression containing the transform.</param>
        /// <param name="i">The current index, updated to one past the closing quote when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <param name="transform">The parsed transform when successful.</param>
        /// <returns>
        ///  <see langword="true"/> if a quoted transform was parsed; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryParseQuotedTransform(string expression, ref int i, int end, out ItemTransform transform)
        {
            int startTransform = i;

            if (TryScanQuotedTransform(expression, ref i, end))
            {
                int startQuoted = startTransform + 1;

                transform = new ItemTransform(
                    text: expression.Substring(startQuoted, i - startQuoted - 1),
                    index: startQuoted);
                return true;
            }

            transform = default;
            return false;
        }

        /// <summary>
        ///  Advances past a quoted item transform without constructing an <see cref="ItemTransform"/>.
        /// </summary>
        /// <param name="expression">The expression containing the transform.</param>
        /// <param name="i">The current index, updated to one past the closing quote when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a quoted transform was found; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryScanQuotedTransform(string expression, ref int i, int end)
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

            return end > i;
        }

        /// <summary>
        ///  Scans an argument list beginning at the specified index.
        /// </summary>
        /// <param name="expression">The expression containing the argument list.</param>
        /// <param name="i">The current index, updated to one past the closing parenthesis when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a complete argument list was found; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryScanArgumentList(string expression, ref int i, int end)
        {
            Debug.Assert((uint)end <= (uint)expression.Length, "The scan end must be within the expression.");

            if (!Sink(expression, ref i, end, '('))
            {
                return false;
            }

            int nestLevel = 1;
            unsafe
            {
                fixed (char* pchar = expression)
                {
                    // Scan for our closing ')'
                    while (i < end && nestLevel > 0)
                    {
                        char character = pchar[i];

                        switch (character)
                        {
                            case '\'' or '`' or '"':
                                int closingQuote = expression.IndexOf(character, i + 1, end - i - 1);
                                if (closingQuote < 0)
                                {
                                    return false;
                                }

                                i = closingQuote;
                                break;

                            case '(':
                                nestLevel++;
                                break;

                            case ')':
                                nestLevel--;
                                break;
                        }

                        i++;
                    }
                }
            }

            return nestLevel == 0;
        }

        /// <summary>
        ///  Parses a function item transform beginning at the specified index.
        /// </summary>
        /// <param name="expression">The expression containing the transform.</param>
        /// <param name="i">The current index, updated to one past the closing parenthesis when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <param name="transform">The parsed transform when successful.</param>
        /// <returns>
        ///  <see langword="true"/> if a function transform was parsed; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryParseFunctionTransform(string expression, ref int i, int end, out ItemTransform transform)
        {
            int startTransform = i;

            if (TryScanFunctionTransform(
                    expression,
                    ref i,
                    end,
                    out int endFunctionName,
                    out int startArguments,
                    out int endArguments))
            {
                string functionName = expression.Substring(startTransform, endFunctionName - startTransform);
                string functionArguments = null;
                if (endArguments > startArguments)
                {
                    functionArguments = Strings.WeakIntern(expression.AsSpan(startArguments, endArguments - startArguments));
                }

                transform = new ItemTransform(
                    text: expression.Substring(startTransform, i - startTransform),
                    index: startTransform,
                    functionName,
                    functionArguments);
                return true;
            }

            transform = default;
            return false;
        }

        /// <summary>
        ///  Advances past a function item transform without constructing an <see cref="ItemTransform"/>.
        /// </summary>
        /// <param name="expression">The expression containing the transform.</param>
        /// <param name="i">The current index, updated to one past the closing parenthesis when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a function transform was found; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryScanFunctionTransform(string expression, ref int i, int end)
            => TryScanFunctionTransform(expression, ref i, end, out _, out _, out _);

        private static bool TryScanFunctionTransform(
            string expression,
            ref int i,
            int end,
            out int endFunctionName,
            out int startArguments,
            out int endArguments)
        {
            endFunctionName = 0;
            startArguments = 0;
            endArguments = 0;

            if (!TryScanValidName(expression, ref i, end))
            {
                return false;
            }

            endFunctionName = i;

            // Eat any whitespace between the function name and its arguments.
            SinkWhitespace(expression, ref i, end);
            startArguments = i + 1;

            if (!TryScanArgumentList(expression, ref i, end))
            {
                return false;
            }

            endArguments = i - 1;
            return true;
        }

        private static bool TryParseValidName(string expression, ref int i, int end, out string name)
            => TryParseValidName(expression, ref i, end, stopBeforeTransformArrow: false, out name);

        /// <summary>
        ///  Parses and weak-interns a valid name beginning at the specified index.
        /// </summary>
        /// <param name="expression">The expression containing the name.</param>
        /// <param name="i">The current index, updated to one past the name when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <param name="stopBeforeTransformArrow">
        ///  <see langword="true"/> to treat a trailing <c>-></c> as an item transform arrow rather than part of
        ///  the name.
        /// </param>
        /// <param name="name">The weak-interned name when successful.</param>
        /// <returns>
        ///  <see langword="true"/> if a valid name was parsed; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryParseValidName(
            string expression,
            ref int i,
            int end,
            bool stopBeforeTransformArrow,
            out string name)
        {
            int start = i;

            if (TryScanValidName(expression, ref i, end, stopBeforeTransformArrow))
            {
                name = Strings.WeakIntern(expression.AsSpan(start, i - start));
                return true;
            }

            name = null;
            return false;
        }

        private static bool TryScanValidName(string expression, ref int i, int end)
            => TryScanValidName(expression, ref i, end, stopBeforeTransformArrow: false);

        /// <summary>
        ///  Scans a valid name beginning at the specified index.
        /// </summary>
        /// <remarks>
        ///  The accepted grammar is <c>[A-Za-z_][A-Za-z_0-9\-]*</c> (via
        ///  <see cref="XmlUtilities.IsValidInitialElementNameCharacter"/> and
        ///  <see cref="XmlUtilities.IsValidSubsequentElementNameCharacter"/>), which defines a valid item
        ///  type or metadata name. This MUST be kept in sync with
        ///  <see cref="ProjectWriter.itemTypeOrMetadataNameSpecification"/>: if the grammar used to parse
        ///  item/metadata expressions diverges from the one used to write them back out, expressions could
        ///  round-trip incorrectly.
        /// </remarks>
        /// <param name="expression">The expression containing the name.</param>
        /// <param name="i">The current index, updated to one past the name when successful.</param>
        /// <param name="end">The exclusive upper bound of the scan.</param>
        /// <param name="stopBeforeTransformArrow">
        ///  <see langword="true"/> to treat a trailing <c>-></c> as an item transform arrow rather than part of
        ///  the name.
        /// </param>
        /// <returns>
        ///  <see langword="true"/> if a valid name was found; otherwise, <see langword="false"/>.
        /// </returns>
        private static bool TryScanValidName(string expression, ref int i, int end, bool stopBeforeTransformArrow)
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

            // '-' is a valid name character, but the final '-' in '->' starts the item transform arrow.
            if (stopBeforeTransformArrow &&
                end > i &&
                expression[i - 1] == '-' &&
                expression[i] == '>')
            {
                i--;
            }

            return true;
        }

        /// <summary>
        ///  Returns <see langword="true"/> if the character at the specified index is the specified char.
        ///  Leaves index one past the character.
        /// </summary>
        private static bool Sink(string expression, ref int i, char c)
            => Sink(expression, ref i, expression.Length, c);

        /// <summary>
        ///  Returns <see langword="true"/> if the character at the specified index (which must be before
        ///  <paramref name="end"/>) is the specified char. Leaves index one past the character.
        /// </summary>
        private static bool Sink(string expression, ref int i, int end, char c)
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
        private static bool Sink(string expression, ref int i, int end, char c1, char c2)
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
        private static void SinkWhitespace(string expression, ref int i)
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
        private static void SinkWhitespace(string expression, ref int i, int end)
        {
            while (i < end && char.IsWhiteSpace(expression[i]))
            {
                i++;
            }
        }
    }
}
