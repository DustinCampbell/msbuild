// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

        private const char PropertyMarkerPrefix = '$';
        private const char ItemVectorMarkerPrefix = '@';
        private const char MetadataMarkerPrefix = '%';

#if NET
        // Modern .NET can expose collection expressions as static-data-backed spans and search them with
        // MemoryExtensions.IndexOfAny. .NET Framework's string.IndexOfAny overloads require char arrays.
        private static ReadOnlySpan<char> ItemVectorOrMetadataMarkerPrefixes => [ItemVectorMarkerPrefix, MetadataMarkerPrefix];
        private static ReadOnlySpan<char> PropertyOrItemMarkerPrefixes => [PropertyMarkerPrefix, ItemVectorMarkerPrefix];
        private static ReadOnlySpan<char> PropertyOrMetadataMarkerPrefixes => [PropertyMarkerPrefix, MetadataMarkerPrefix];
        private static ReadOnlySpan<char> AllMarkerPrefixes => [PropertyMarkerPrefix, ItemVectorMarkerPrefix, MetadataMarkerPrefix];
#else
        private static readonly char[] ItemVectorOrMetadataMarkerPrefixes = [ItemVectorMarkerPrefix, MetadataMarkerPrefix];
        private static readonly char[] PropertyOrItemMarkerPrefixes = [PropertyMarkerPrefix, ItemVectorMarkerPrefix];
        private static readonly char[] PropertyOrMetadataMarkerPrefixes = [PropertyMarkerPrefix, MetadataMarkerPrefix];
        private static readonly char[] AllMarkerPrefixes = [PropertyMarkerPrefix, ItemVectorMarkerPrefix, MetadataMarkerPrefix];
#endif
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
        ///  Determines whether an expression contains a property or item-vector marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsPropertyOrItemVectorMarker(string expression)
            => IndexOfPropertyOrItemVectorMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether an expression contains a property or metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsPropertyOrMetadataMarker(string expression)
            => IndexOfPropertyOrMetadataMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether an expression contains an item-vector or metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsItemVectorOrMetadataMarker(string expression)
            => IndexOfItemVectorOrMetadataMarker(expression) >= 0;

        /// <summary>
        ///  Determines whether an expression contains a property, item-vector, or metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  <see langword="true"/> if a marker is found; otherwise, <see langword="false"/>.
        /// </returns>
        public static bool ContainsAnyExpansionMarker(string expression)
            => IndexOfAnyExpansionMarker(expression) >= 0;

        /// <summary>
        ///  Finds the first property or item-vector marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyOrItemVectorMarker(string expression)
            => IndexOfAnyMarker(expression, PropertyOrItemMarkerPrefixes);

        /// <summary>
        ///  Finds the first property or item-vector marker at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <inheritdoc cref="IndexOfPropertyOrItemVectorMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyOrItemVectorMarker(string expression, int startIndex)
            => IndexOfAnyMarker(expression, PropertyOrItemMarkerPrefixes, startIndex, count: expression.Length - startIndex);

        /// <summary>
        ///  Finds the first property or item-vector marker in the range beginning at
        ///  <paramref name="startIndex"/> and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <inheritdoc cref="IndexOfPropertyOrItemVectorMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyOrItemVectorMarker(string expression, int startIndex, int count)
            => IndexOfAnyMarker(expression, PropertyOrItemMarkerPrefixes, startIndex, count);

        /// <summary>
        ///  Finds the first property or metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyOrMetadataMarker(string expression)
            => IndexOfAnyMarker(expression, PropertyOrMetadataMarkerPrefixes);

        /// <summary>
        ///  Finds the first property or metadata marker at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <inheritdoc cref="IndexOfPropertyOrMetadataMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyOrMetadataMarker(string expression, int startIndex)
            => IndexOfAnyMarker(expression, PropertyOrMetadataMarkerPrefixes, startIndex, expression.Length - startIndex);

        /// <summary>
        ///  Finds the first property or metadata marker in the range beginning at
        ///  <paramref name="startIndex"/> and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <inheritdoc cref="IndexOfPropertyOrMetadataMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfPropertyOrMetadataMarker(string expression, int startIndex, int count)
            => IndexOfAnyMarker(expression, PropertyOrMetadataMarkerPrefixes, startIndex, count);

        /// <summary>
        ///  Finds the first item-vector or metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfItemVectorOrMetadataMarker(string expression)
            => IndexOfAnyMarker(expression, ItemVectorOrMetadataMarkerPrefixes);

        /// <summary>
        ///  Finds the first item-vector or metadata marker at or after <paramref name="startIndex"/>.
        /// </summary>
        /// <inheritdoc cref="IndexOfItemVectorOrMetadataMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfItemVectorOrMetadataMarker(string expression, int startIndex)
            => IndexOfAnyMarker(expression, ItemVectorOrMetadataMarkerPrefixes, startIndex, count: expression.Length - startIndex);

        /// <summary>
        ///  Finds the first item-vector or metadata marker in the range beginning at
        ///  <paramref name="startIndex"/> and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <inheritdoc cref="IndexOfItemVectorOrMetadataMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfItemVectorOrMetadataMarker(string expression, int startIndex, int count)
            => IndexOfAnyMarker(expression, ItemVectorOrMetadataMarkerPrefixes, startIndex, count);

        /// <summary>
        ///  Finds the first property, item-vector, or metadata marker.
        /// </summary>
        /// <param name="expression">The expression to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfAnyExpansionMarker(string expression)
            => IndexOfAnyMarker(expression, AllMarkerPrefixes);

        /// <summary>
        ///  Finds the first property, item-vector, or metadata marker at or after
        ///  <paramref name="startIndex"/>.
        /// </summary>
        /// <inheritdoc cref="IndexOfAnyExpansionMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfAnyExpansionMarker(string expression, int startIndex)
            => IndexOfAnyMarker(expression, AllMarkerPrefixes, startIndex, expression.Length - startIndex);

        /// <summary>
        ///  Finds the first property, item-vector, or metadata marker in the range beginning at
        ///  <paramref name="startIndex"/> and spanning <paramref name="count"/> characters.
        /// </summary>
        /// <inheritdoc cref="IndexOfAnyExpansionMarker(string)"/>
        /// <param name="expression">The expression to scan.</param>
        /// <param name="startIndex">The index at which to begin scanning.</param>
        /// <param name="count">The number of characters to scan.</param>
        /// <returns>
        ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
        /// </returns>
        public static int IndexOfAnyExpansionMarker(string expression, int startIndex, int count)
            => IndexOfAnyMarker(expression, AllMarkerPrefixes, startIndex, count);

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
            => IndexOfMarker(expression, PropertyMarkerPrefix);

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
            => IndexOfMarker(expression, PropertyMarkerPrefix, startIndex, expression.Length - startIndex);

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
            => IndexOfMarker(expression, PropertyMarkerPrefix, startIndex, count);

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
            => IndexOfMarker(expression, ItemVectorMarkerPrefix);

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
            => IndexOfMarker(expression, ItemVectorMarkerPrefix, startIndex, expression.Length - startIndex);

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
            => IndexOfMarker(expression, ItemVectorMarkerPrefix, startIndex, count);

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
            => IndexOfMarker(expression, MetadataMarkerPrefix);

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
            => IndexOfMarker(expression, MetadataMarkerPrefix, startIndex, expression.Length - startIndex);

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
            => IndexOfMarker(expression, MetadataMarkerPrefix, startIndex, count);

        private static int IndexOfMarker(string expression, char marker)
        {
            if (expression.Length < 2)
            {
                return -1;
            }

            // IndexOf(char) is significantly faster than an ordinal two-character search,
            // especially when the marker is absent, so check the opening parenthesis separately.
            // 
            // PERF NOTE: On modern .NET, string.IndexOf(char) does not argument checking and
            // delegates directly to SpanHelpers.IndexOfChar(ref _firstChar, value, Length).
            // So, it's not necessary to first convert the string to a span.
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
            if (count < 2)
            {
                return -1;
            }

            if (expression[startIndex] == marker && expression[startIndex + 1] == '(')
            {
                return startIndex;
            }

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

#if NET
        private static int IndexOfAnyMarker(string expression, ReadOnlySpan<char> markers)
        {
            if (expression.Length > 1 &&
                expression[1] == '(' &&
                markers.IndexOf(expression[0]) >= 0)
            {
                return 0;
            }

            ReadOnlySpan<char> remaining = expression;
            int offset = 0;

            while (remaining.Length > 1)
            {
                int markerIndex = remaining.IndexOfAny(markers);
                if (markerIndex < 0 || markerIndex == remaining.Length - 1)
                {
                    break;
                }

                if (remaining[markerIndex + 1] == '(')
                {
                    return offset + markerIndex;
                }

                int consumed = markerIndex + 1;
                remaining = remaining[consumed..];
                offset += consumed;
            }

            return -1;
        }
#else
        private static int IndexOfAnyMarker(string expression, char[] markers)
        {
            if (expression.Length > 1 && expression[1] == '(')
            {
                char first = expression[0];
                for (int i = 0; i < markers.Length; i++)
                {
                    if (markers[i] == first)
                    {
                        return 0;
                    }
                }
            }

            int startIndex = 0;
            while (startIndex < expression.Length - 1)
            {
                int markerIndex = expression.IndexOfAny(markers, startIndex);
                if (markerIndex < 0 || markerIndex == expression.Length - 1)
                {
                    break;
                }

                if (expression[markerIndex + 1] == '(')
                {
                    return markerIndex;
                }

                startIndex = markerIndex + 1;
            }

            return -1;
        }
#endif

#if NET
        private static int IndexOfAnyMarker(string expression, ReadOnlySpan<char> markers, int startIndex, int count)
        {
            ReadOnlySpan<char> remaining = expression.AsSpan(startIndex, count);
            int offset = startIndex;

            while (remaining.Length > 1)
            {
                int markerIndex = remaining.IndexOfAny(markers);
                if (markerIndex < 0 || markerIndex == remaining.Length - 1)
                {
                    break;
                }

                if (remaining[markerIndex + 1] == '(')
                {
                    return offset + markerIndex;
                }

                int consumed = markerIndex + 1;
                remaining = remaining[consumed..];
                offset += consumed;
            }

            return -1;
        }
#else
        private static int IndexOfAnyMarker(string expression, char[] markers, int startIndex, int count)
        {
            int markerIndex = expression.IndexOfAny(markers, startIndex, count);
            int endIndex = startIndex + count;

            while (markerIndex >= 0 && markerIndex < endIndex - 1)
            {
                if (expression[markerIndex + 1] == '(')
                {
                    return markerIndex;
                }

                int nextIndex = markerIndex + 1;
                markerIndex = expression.IndexOfAny(markers, nextIndex, endIndex - nextIndex);
            }

            return -1;
        }
#endif

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

        /// <inheritdoc cref="TryGetNextItemVectorExpression(string, int, out ItemExpressionCapture)"/>
        public static bool TryGetNextItemVectorExpression(string expression, out ItemExpressionCapture itemVector)
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
        public static bool TryGetNextItemVectorExpression(string expression, int startIndex, out ItemExpressionCapture itemVector)
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

        private static bool TryParseItemVectorExpression(string expression, int startIndex, out ItemExpressionCapture itemVector)
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

            // Grab the name, but continue to verify it's a well-formed expression
            // before we store it.
            string itemName = Strings.WeakIntern(expression.AsSpan(startOfName, index - startOfName));

            SinkWhitespace(expression, ref index);
            List<ItemExpressionCapture> transformExpressions = null;

            // If there's an '->' eat it and the subsequent quoted expression or transform function
            while (Sink(expression, ref index, end, '-', '>'))
            {
                SinkWhitespace(expression, ref index);
                int startTransform = index;

                if (SinkSingleQuotedExpression(expression, ref index, end))
                {
                    int startQuoted = startTransform + 1;
                    int endQuoted = index - 1;
                    if (transformExpressions == null)
                    {
                        // PERF: Almost all expressions have only one capture, so optimize for that case
                        transformExpressions = new List<ItemExpressionCapture>(1);
                    }

                    transformExpressions.Add(new ItemExpressionCapture(startQuoted, endQuoted - startQuoted, expression.Substring(startQuoted, endQuoted - startQuoted)));
                    SinkWhitespace(expression, ref index);
                    continue;
                }

                startTransform = index;
                if (TryParseFunctionTransform(expression, startTransform, ref index, end, out ItemExpressionCapture transform))
                {
                    // PERF: Almost all expressions have only one capture, so optimize for that case
                    transformExpressions ??= new List<ItemExpressionCapture>(1);
                    transformExpressions.Add(transform);

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

            // Create an expression capture that encompasses the entire expression between the @( and the )
            // with the item name and any separator contained within it
            // and each transform expression contained within it (i.e. each ->XYZ)
            itemVector = new ItemExpressionCapture(
                index: startIndex,
                length,
                subExpression: Strings.WeakIntern(expression.AsSpan(startIndex, length)),
                itemType: itemName,
                separator,
                separatorStart,
                captures: transformExpressions);

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
                int markerIndex = IndexOfItemVectorOrMetadataMarker(expression, i, end - i);
                if (markerIndex < 0)
                {
                    break;
                }

                char markerPrefix = expression[markerIndex];
                i = markerIndex + 2;
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

            int startOfText = i;

            if (!SinkValidName(expression, ref i, end))
            {
                return false;
            }

            string firstName = Strings.WeakIntern(expression.AsSpan(startOfText, i - startOfText));

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

                metadataName = Strings.WeakIntern(expression.AsSpan(startOfText, i - startOfText));

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
        private static bool SinkSingleQuotedExpression(string expression, ref int i, int end)
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
        private static bool SinkArgumentsInParentheses(string expression, ref int i, int end)
        {
            int nestLevel = 0;
            int length = expression.Length;
            int restartPoint;

            unsafe
            {
                fixed (char* pchar = expression)
                {
                    if (pchar[i] == '(')
                    {
                        nestLevel++;
                        i++;
                    }
                    else
                    {
                        return false;
                    }

                    // Scan for our closing ')'
                    while (i < length && i < end && nestLevel > 0)
                    {
                        char character = pchar[i];

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
                }
            }

            if (nestLevel == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Skip all characters until we find the matching quote character
        /// </summary>
        private static bool SinkUntilClosingQuote(char quoteChar, string expression, ref int i, int end)
        {
            unsafe
            {
                fixed (char* pchar = expression)
                {
                    // We have already checked the first quote
                    i++;

                    // Scan for our closing quoteChar
                    while (i < expression.Length && i < end)
                    {
                        if (pchar[i] == quoteChar)
                        {
                            return true;
                        }

                        i++;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if a item function subexpression begins at the specified index
        /// and ends before the specified end index.
        /// Leaves index one past the end of the closing paren.
        /// </summary>
        private static bool TryParseFunctionTransform(string expression, int startTransform, ref int i, int end, out ItemExpressionCapture transform)
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

                    string functionName = expression.Substring(startTransform, endFunctionName - startTransform);
                    string functionArguments = null;
                    if (endFunctionArguments > startFunctionArguments)
                    {
                        functionArguments = Strings.WeakIntern(expression.AsSpan(startFunctionArguments, endFunctionArguments - startFunctionArguments));
                    }

                    transform = new ItemExpressionCapture(startTransform, i - startTransform, expression.Substring(startTransform, i - startTransform), null, null, -1, null, functionName, functionArguments);
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
        private static bool SinkValidName(string expression, ref int i, int end)
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

        /// <summary>
        /// Represents one substring for a single successful capture.
        /// </summary>
        internal struct ItemExpressionCapture
        {
            /// <summary>
            /// Create an Expression Capture instance
            /// Represents a sub expression, shredded from a larger expression
            /// </summary>
            public ItemExpressionCapture(int index, int length, string subExpression)
                : this(index, length, subExpression, null, null, -1, null, null, null)
            {
            }

            public ItemExpressionCapture(int index, int length, string subExpression, string itemType, string separator, int separatorStart, List<ItemExpressionCapture> captures)
                : this(index, length, subExpression, itemType, separator, separatorStart, captures, null, null)
            {
            }

            /// <summary>
            /// Create an Expression Capture instance
            /// Represents a sub expression, shredded from a larger expression
            /// </summary>
            public ItemExpressionCapture(int index, int length, string subExpression, string itemType, string separator, int separatorStart, List<ItemExpressionCapture> captures, string functionName, string functionArguments)
            {
                Index = index;
                Length = length;
                Value = subExpression;
                ItemType = itemType;
                Separator = separator;
                SeparatorStart = separatorStart;
                Captures = captures;
                FunctionName = functionName;
                FunctionArguments = functionArguments;
            }

            /// <summary>
            /// Captures within this capture
            /// </summary>
            public List<ItemExpressionCapture> Captures { get; }

            /// <summary>
            /// The position in the original string where the first character of the captured
            /// substring was found.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// The length of the captured substring.
            /// </summary>
            public int Length { get; }

            /// <summary>
            /// Gets the captured substring from the input string.
            /// </summary>
            public string Value { get; }

            /// <summary>
            /// Gets the captured itemtype.
            /// </summary>
            public string ItemType { get; }

            /// <summary>
            /// Gets the captured itemtype.
            /// </summary>
            public string Separator { get; }

            /// <summary>
            /// The starting character of the separator.
            /// </summary>
            public int SeparatorStart { get; }

            /// <summary>
            /// The function name, if any, within this expression
            /// </summary>
            public string FunctionName { get; }

            /// <summary>
            /// The function arguments, if any, within this expression
            /// </summary>
            public string FunctionArguments { get; }

            /// <summary>
            /// Gets the captured substring from the input string.
            /// </summary>
            public override string ToString()
            {
                return Value;
            }
        }
    }
}
