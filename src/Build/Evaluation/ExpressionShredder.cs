// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// A class which interprets and splits MSBuild expressions
/// </summary>
internal static class ExpressionShredder
{
    private static readonly char[] s_markers = ['@', '%'];

    /// <summary>
    ///  Splits an expression into fragments at semicolons, except where the semicolons are inside a
    ///  macro or separator expression. Fragments are trimmed and empty fragments are discarded.
    /// </summary>
    /// <param name="expression">The list expression to split.</param>
    /// <returns>
    ///  An <see cref="ExpressionSplitter"/> over the fragments. It is a struct that is its own
    ///  enumerator, so it can be consumed with <c>foreach</c> or a collection-expression spread
    ///  (<c>[.. ...]</c>) without any heap allocation. See <see cref="ExpressionSplitter"/> for
    ///  the full splitting rules.
    /// </returns>
    internal static ExpressionSplitter Split(string expression)
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
        ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

        // PERF: Use for to avoid boxing expressions enumerator
        for (int i = 0; i < expressions.Count; i++)
        {
            string expression = expressions[i];
            GetReferencedItemNamesAndMetadata(expression, ref pair);
        }

        return pair;
    }

    /// <summary>
    /// Returns true if there is a metadata expression (outside of a transform) in the expression.
    /// </summary>
    internal static bool ContainsMetadataExpressionOutsideTransform(string expression)
    {
        ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

        GetReferencedItemNamesAndMetadata(expression, start: 0, end: expression.Length, ref pair, includeItemTypes: false, includeMetadataOutsideTransforms: true);

        bool result = (pair.Metadata?.Count > 0);

        return result;
    }

    /// <summary>
    /// Given an expression, finds referenced item vector expressions (e.g. <c>@(Foo)</c>,
    /// <c>@(Foo->'%(Bar)')</c>).
    /// </summary>
    internal static ItemVectorEnumerator GetReferencedItemExpressions(string expression)
        => new(expression);

    /// <summary>
    ///  Attempts to scan a single <c>@(...)</c> item vector expression starting at <paramref name="i"/>.
    /// </summary>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position. Advanced past the expression on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <param name="capture">The scanned expression if one was found; otherwise <see langword="default"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if a well-formed item vector expression was scanned.
    /// </returns>
    /// <remarks>
    ///  On success, <paramref name="i"/> is advanced to one past the closing <c>)</c> and
    ///  <paramref name="capture"/> contains the scanned expression. On failure, <paramref name="i"/> is
    ///  left unchanged and the caller should advance past the opening <c>@(</c> before scanning again.
    /// </remarks>
    internal static bool TryScanItemExpressionCapture(string expression, ref int i, int end, out ItemVector capture)
    {
        capture = default;

        // Work on a local scan position that is only committed back to i on success, so a failed
        // scan leaves the caller's position unchanged.
        int index = i;

        if (!Sink(expression, ref index, end, '@', '('))
        {
            return false;
        }

        // Start of a possible item list expression. Store the expression's start point (the '@').
        int startPoint = index - 2;

        SinkWhitespace(expression, ref index, end);

        int startOfName = index;

        if (!SinkValidName(expression, ref index, end))
        {
            return false;
        }

        // Grab the name, but continue to verify it's a well-formed expression
        // before we store it.
        string itemName = Strings.WeakIntern(expression.AsSpan(startOfName, index - startOfName));

        SinkWhitespace(expression, ref index, end);
        List<ItemVector>? transformExpressions = null;

        // If there's an '->' eat it and the subsequent quoted expression or transform function
        while (Sink(expression, ref index, end, '-', '>'))
        {
            SinkWhitespace(expression, ref index, end);

            if (TryParseQuotedTransform(expression, ref index, end, out ItemVector quotedTransform))
            {
                // PERF: Almost all expressions have only one capture, so optimize for that case
                transformExpressions ??= new List<ItemVector>(1);
                transformExpressions.Add(quotedTransform);

                SinkWhitespace(expression, ref index, end);
                continue;
            }

            if (TryParseFunctionTransform(expression, ref index, end, out ItemVector functionCapture))
            {
                // PERF: Almost all expressions have only one capture, so optimize for that case
                transformExpressions ??= new List<ItemVector>(1);
                transformExpressions.Add(functionCapture);

                SinkWhitespace(expression, ref index, end);
                continue;
            }

            // Saw '->' but neither a quoted transform nor a transform function followed: malformed.
            return false;
        }

        SinkWhitespace(expression, ref index, end);

        string? separator = null;
        int separatorStart = -1;

        // If there's a ',', eat it and the subsequent quoted expression
        if (Sink(expression, ref index, end, ','))
        {
            SinkWhitespace(expression, ref index, end);

            if (!Sink(expression, ref index, end, '\''))
            {
                return false;
            }

            int closingQuote = expression.IndexOf('\'', index, end - index);
            if (closingQuote == -1)
            {
                return false;
            }

            separatorStart = index - startPoint;
            separator = expression.Substring(index, closingQuote - index);

            index = closingQuote + 1;
        }

        SinkWhitespace(expression, ref index, end);

        if (!Sink(expression, ref index, end, ')'))
        {
            return false;
        }

        int endPoint = index;

        // Create an expression capture that encompasses the entire expression between the @( and the )
        // with the item name and any separator contained within it
        // and each transform expression contained within it (i.e. each ->XYZ)
        capture = new ItemVector(
            text: Strings.WeakIntern(expression.AsSpan(startPoint, endPoint - startPoint)),
            index: startPoint,
            length: endPoint - startPoint,
            itemName,
            separator,
            separatorStart,
            transformExpressions);

        i = index;
        return true;
    }

    /// <summary>
    /// Given a subexpression, finds referenced item names and inserts them into the table
    /// as K=Name, V=String.Empty.
    /// </summary>
    /// <remarks>
    /// We can ignore any semicolons in the expression, since we're not itemizing it.
    /// </remarks>
    internal static void GetReferencedItemNamesAndMetadata(string expression, ref ItemsAndMetadataPair pair)
        => GetReferencedItemNamesAndMetadata(expression, start: 0, end: expression.Length, pair: ref pair, includeItemTypes: true, includeMetadataOutsideTransforms: true);

    /// <summary>
    /// Given a subexpression, finds referenced item names and inserts them into the table
    /// as K=Name, V=String.Empty.
    /// </summary>
    /// <remarks>
    /// We can ignore any semicolons in the expression, since we're not itemizing it.
    /// </remarks>
    private static void GetReferencedItemNamesAndMetadata(string expression, int start, int end, ref ItemsAndMetadataPair pair, bool includeItemTypes, bool includeMetadataOutsideTransforms)
    {
        int index = start;

        while (index < end)
        {
            // Find the next '@' or '%'; bail out if there's no room for a '(' after it.
            index = expression.IndexOfAny(s_markers, index, end - index);
            if (index < 0 || index + 1 >= end)
            {
                return;
            }

            // Only '@(' and '%(' are markers; skip a bare '@' or '%'.
            if (expression[index + 1] != '(')
            {
                index++;
                continue;
            }

            char marker = expression[index];

            // Skip past the marker's two opening characters. If the expression turns out to be
            // malformed, scanning resumes here.
            index += 2;
            int restartPoint = index;

            if (marker == '@')
            {
                // Start of a possible item list expression.
                SinkWhitespace(expression, ref index, end);

                int startOfName = index;

                if (!SinkValidName(expression, ref index, end))
                {
                    index = restartPoint;
                    continue;
                }

                // Grab the name boundaries, but continue to verify it's a well-formed expression
                // before we store it.
                int nameLength = index - startOfName;

                SinkWhitespace(expression, ref index, end);

                bool transformOrFunctionFound = true;

                // If there's an '->' eat it and the subsequent quoted expression or transform function
                while (Sink(expression, ref index, end, '-', '>') && transformOrFunctionFound)
                {
                    SinkWhitespace(expression, ref index, end);

                    if (SinkSingleQuotedExpression(expression, ref index, end))
                    {
                        SinkWhitespace(expression, ref index, end);
                        continue;
                    }

                    if (SinkFunctionTransform(expression, ref index, end))
                    {
                        SinkWhitespace(expression, ref index, end);
                        continue;
                    }

                    index = restartPoint;
                    transformOrFunctionFound = false;
                }

                if (!transformOrFunctionFound)
                {
                    continue;
                }

                SinkWhitespace(expression, ref index, end);

                // If there's a ',', eat it and the subsequent quoted expression
                if (Sink(expression, ref index, end, ','))
                {
                    SinkWhitespace(expression, ref index, end);

                    if (!Sink(expression, ref index, end, '\''))
                    {
                        index = restartPoint;
                        continue;
                    }

                    int closingQuote = expression.IndexOf('\'', index, end - index);
                    if (closingQuote == -1)
                    {
                        index = restartPoint;
                        continue;
                    }

                    // Look for metadata in the separator expression
                    // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
                    GetReferencedItemNamesAndMetadata(expression, start: index, end: closingQuote, ref pair, includeItemTypes: false, includeMetadataOutsideTransforms: true);

                    index = closingQuote + 1;
                }

                SinkWhitespace(expression, ref index, end);

                if (!Sink(expression, ref index, end, ')'))
                {
                    index = restartPoint;
                    continue;
                }

                // If we've got this far, we know the item expression was
                // well formed, so make sure the name's in the table
                if (includeItemTypes)
                {
                    pair.Items ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Items.Add(expression.Substring(startOfName, nameLength));
                }
            }
            else
            {
                // Start of a possible metadata expression.
                if (!TryParseMetadataExpression(expression, ref index, end, out string? itemName, out string? metadataName))
                {
                    index = restartPoint;
                    continue;
                }

                if (includeMetadataOutsideTransforms)
                {
                    string qualifiedMetadataName = itemName != null ? $"{itemName}.{metadataName}" : metadataName;
                    pair.Metadata ??= new Dictionary<string, MetadataReference>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Metadata[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
                }
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
    internal static bool TryParseMetadataExpression(string expression, ref int i, int end, out string? itemType, [NotNullWhen(true)] out string? metadataName)
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
    ///  Returns <see langword="true"/> if a single-quoted subexpression (e.g. <c>'foo'</c>) begins at
    ///  <paramref name="i"/>, advancing past the closing quote.
    /// </summary>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position. Advanced past the closing quote on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <returns>
    ///  <see langword="true"/> if a single-quoted subexpression was found.
    /// </returns>
    private static bool SinkSingleQuotedExpression(string expression, ref int i, int end)
    {
        if (!Sink(expression, ref i, end, '\''))
        {
            return false;
        }

        int startIndex = i;
        int endIndex = expression.IndexOf('\'', startIndex, end - startIndex);

        if (endIndex < 0)
        {
            return false;
        }

        i = endIndex + 1;
        return true;
    }

    /// <summary>
    ///  Attempts to parse a single-quoted transform (e.g. <c>'foo'</c>) beginning at <paramref name="i"/>,
    ///  capturing its quoted contents into <paramref name="result"/>.
    /// </summary>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position. Advanced past the closing quote on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <param name="result">The parsed transform if one was found; otherwise <see langword="default"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if a single-quoted transform was parsed.
    /// </returns>
    private static bool TryParseQuotedTransform(string expression, ref int i, int end, out ItemVector result)
    {
        if (!Sink(expression, ref i, end, '\''))
        {
            result = default;
            return false;
        }

        int startQuoted = i;
        int endQuoted = expression.IndexOf('\'', startQuoted, end - startQuoted);

        if (endQuoted < 0)
        {
            result = default;
            return false;
        }

        result = new ItemVector(
            text: expression.Substring(startQuoted, endQuoted - startQuoted),
            index: startQuoted,
            length: endQuoted - startQuoted);

        i = endQuoted + 1;
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
        Assumed.LessThanOrEqual(end, expression.Length);

        int start = i;

        // The opening '(' is required; bail out (without pinning) if it isn't there.
        if (i >= end || expression[i] != '(')
        {
            return false;
        }

        int nestLevel = 1;
        i++;

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
                            // Skip to the matching closing quote (the opening one is already consumed).
                            i++;

                            while (i < end && pchar[i] != character)
                            {
                                i++;
                            }

                            if (i >= end)
                            {
                                i = start;
                                return false;
                            }

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

        if (nestLevel != 0)
        {
            i = start;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if a item function subexpression begins at the specified index
    /// and ends before the specified end index.
    /// Leaves index one past the end of the closing paren.
    /// </summary>
    private static bool SinkFunctionTransform(string expression, ref int i, int end)
    {
        if (SinkValidName(expression, ref i, end))
        {
            // Eat any whitespace between the function name and its arguments
            SinkWhitespace(expression, ref i, end);

            if (SinkArgumentsInParentheses(expression, ref i, end))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Attempts to parse an item function transform (e.g. <c>Distinct()</c>) beginning at
    ///  <paramref name="i"/>, capturing it into <paramref name="result"/>.
    /// </summary>
    /// <param name="expression">The expression being scanned.</param>
    /// <param name="i">Current scan position. Advanced past the closing <c>)</c> on success.</param>
    /// <param name="end">Exclusive end index of the scan range; no character at or beyond this index is read.</param>
    /// <param name="result">The parsed transform if one was found; otherwise <see langword="default"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if an item function transform was parsed.
    /// </returns>
    private static bool TryParseFunctionTransform(string expression, ref int i, int end, out ItemVector result)
    {
        int start = i;

        if (SinkValidName(expression, ref i, end))
        {
            int endFunctionName = i;

            // Eat any whitespace between the function name and its arguments
            SinkWhitespace(expression, ref i, end);
            int startFunctionArguments = i + 1;

            if (SinkArgumentsInParentheses(expression, ref i, end))
            {
                int endFunctionArguments = i - 1;

                string functionName = expression.Substring(start, endFunctionName - start);
                string? functionArguments = null;
                if (endFunctionArguments > startFunctionArguments)
                {
                    functionArguments = Strings.WeakIntern(expression.AsSpan(startFunctionArguments, endFunctionArguments - startFunctionArguments));
                }

                result = new ItemVector(
                    text: expression.Substring(start, i - start),
                    index: start,
                    length: i - start,
                    functionName: functionName,
                    functionArguments: functionArguments);

                return true;
            }
        }

        result = default;
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

        // '-' is a legitimate char in an item name, but we should match '->' as an arrow
        // in '@(foo->'x')' rather than as the last char of the item name.
        // The old regex accomplished this by being "greedy"
        if (end > i && expression[i - 1] == '-' && expression[i] == '>')
        {
            i--;
        }

        return true;
    }

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
