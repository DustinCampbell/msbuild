// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Provides methods for locating, parsing, and splitting MSBuild expressions.
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

    /// <summary>
    ///  The possible first characters of item-vector and metadata markers.
    /// </summary>
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
    /// <param name="expression">The expression to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
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
    /// <param name="expression">The expression to scan.</param>
    /// <param name="startIndex">The index at which to begin scanning.</param>
    /// <param name="count">The number of characters to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  Both characters of the marker must be contained within the specified range.
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfPropertyMarker(string expression, int startIndex, int count)
        => IndexOfMarker(expression, '$', startIndex, count);

    /// <summary>
    ///  Finds the first item-vector marker.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfItemVectorMarker(string expression)
        => IndexOfMarker(expression, '@');

    /// <summary>
    ///  Finds the first item-vector marker at or after <paramref name="startIndex"/>.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="startIndex">The index at which to begin scanning.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfItemVectorMarker(string expression, int startIndex)
        => IndexOfMarker(expression, '@', startIndex, expression.Length - startIndex);

    /// <summary>
    ///  Finds the first item-vector marker in the range beginning at <paramref name="startIndex"/>
    ///  and spanning <paramref name="count"/> characters.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="startIndex">The index at which to begin scanning.</param>
    /// <param name="count">The number of characters to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  Both characters of the marker must be contained within the specified range.
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfItemVectorMarker(string expression, int startIndex, int count)
        => IndexOfMarker(expression, '@', startIndex, count);

    /// <summary>
    ///  Finds the first metadata marker.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfMetadataMarker(string expression)
        => IndexOfMarker(expression, '%');

    /// <summary>
    ///  Finds the first metadata marker at or after <paramref name="startIndex"/>.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="startIndex">The index at which to begin scanning.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfMetadataMarker(string expression, int startIndex)
        => IndexOfMarker(expression, '%', startIndex, expression.Length - startIndex);

    /// <summary>
    ///  Finds the first metadata marker in the range beginning at <paramref name="startIndex"/>
    ///  and spanning <paramref name="count"/> characters.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="startIndex">The index at which to begin scanning.</param>
    /// <param name="count">The number of characters to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
    /// <remarks>
    ///  Both characters of the marker must be contained within the specified range.
    ///  This method does not validate the expression or locate its closing parenthesis.
    /// </remarks>
    public static int IndexOfMetadataMarker(string expression, int startIndex, int count)
        => IndexOfMarker(expression, '%', startIndex, count);

    /// <summary>
    ///  Finds the first occurrence of a two-character marker beginning with <paramref name="marker"/>.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="marker">The first character of the marker.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
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

    /// <summary>
    ///  Finds the first occurrence of a two-character marker beginning with <paramref name="marker"/> in a
    ///  bounded range.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="marker">The first character of the marker.</param>
    /// <param name="startIndex">The index at which to begin scanning.</param>
    /// <param name="count">The number of characters to scan.</param>
    /// <returns>
    ///  The zero-based index of the marker, or <c>-1</c> if it is not found.
    /// </returns>
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
    ///  Splits an expression into fragments at semicolons, except where the semicolons are in a macro or
    ///  separator expression. Fragments are trimmed and empty fragments are discarded.
    /// </summary>
    /// <param name="expression">The list expression to split.</param>
    /// <returns>
    ///  A tokenizer that enumerates the non-empty fragments.
    /// </returns>
    /// <remarks>
    ///  See <see cref="SemiColonTokenizer"/> for the splitting rules.
    /// </remarks>
    public static SemiColonTokenizer SplitSemiColonSeparatedList(string expression)
        => new(expression);

    /// <summary>
    ///  Finds all item names and metadata references outside transforms in a list of expressions.
    /// </summary>
    /// <param name="expressions">The expressions to scan.</param>
    /// <returns>
    ///  The collected item names and metadata references.
    /// </returns>
    /// <remarks>
    ///  Item names are stored in a set. Metadata keys are either qualified
    ///  (<c>ItemType.MetadataName</c>) or unqualified (<c>MetadataName</c>). The collections remain
    ///  <see langword="null"/> when no corresponding references are found.
    /// </remarks>
    public static ItemsAndMetadataPair GetReferencedItemNamesAndMetadata(IReadOnlyList<string> expressions)
    {
        ItemsAndMetadataPair pair = default;

        // PERF: Use for to avoid boxing expressions enumerator
        for (int i = 0; i < expressions.Count; i++)
        {
            string expression = expressions[i];
            GetReferencedItemNamesAndMetadata(expression, ref pair);
        }

        return pair;
    }

    /// <summary>
    ///  Determines whether an expression contains a metadata reference outside an item transform.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <returns>
    ///  <see langword="true"/> if a metadata reference is found outside a transform; otherwise,
    ///  <see langword="false"/>.
    /// </returns>
    public static bool ContainsMetadataExpressionOutsideTransform(string expression)
    {
        ItemsAndMetadataPair pair = default;
        Scanner scanner = new(expression);
        scanner.CollectReferencedItemNamesAndMetadata(
            ref pair,
            collectItemTypes: false,
            collectMetadataOutsideTransforms: true);

        return pair.Metadata?.Count > 0;
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
        Scanner scanner = new(expression, startIndex, expression.Length);
        return scanner.TryGetNextItemVector(out itemVector);
    }

    /// <summary>
    ///  Finds referenced item names and metadata in an expression and adds them to the supplied collections.
    /// </summary>
    /// <param name="expression">The expression to scan.</param>
    /// <param name="pair">The collections to which referenced item names and metadata are added.</param>
    /// <remarks>
    ///  Semicolons can be ignored because the expression is not being itemized.
    /// </remarks>
    public static void GetReferencedItemNamesAndMetadata(string expression, ref ItemsAndMetadataPair pair)
    {
        Scanner scanner = new(expression);
        scanner.CollectReferencedItemNamesAndMetadata(
            ref pair,
            collectItemTypes: true,
            collectMetadataOutsideTransforms: true);
    }

    /// <summary>
    ///  Attempts to parse a metadata expression of the form <c>%(Name)</c> or <c>%(ItemType.Name)</c>,
    ///  starting just after the <c>%(</c> has been consumed (i.e., <paramref name="i"/> points at
    ///  the first character after the opening parenthesis).
    /// </summary>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position (just after <c>%(</c>). Advanced on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <param name="itemType">The item type if qualified; otherwise <see langword="null"/>.</param>
    /// <param name="metadataName">The metadata name when parsing succeeds.</param>
    /// <returns>
    ///  <see langword="true"/> if a valid metadata expression is parsed; otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///  On success, <paramref name="i"/> is left one past the closing <c>)</c>.
    ///  On failure, <paramref name="i"/> is at an indeterminate position and the caller
    ///  should restore it from a saved position.
    /// </remarks>
    public static bool TryParseMetadataExpression(
        string expression,
        ref int i,
        int end,
        out string? itemType,
        [NotNullWhen(true)] out string? metadataName)
    {
        Scanner scanner = new(expression, i, end);
        bool result = scanner.TryParseMetadataExpression(out itemType, out metadataName);
        i = scanner.Position;
        return result;
    }
}
