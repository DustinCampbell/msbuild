// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Text;
using static Microsoft.NET.StringTools.Strings;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
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
    internal static SemiColonTokenizer SplitSemiColonSeparatedList(StringSegment expression)
        => new(expression);

    internal static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(StringSegment expression)
        => new(expression);

    /// <summary>
    /// Returns true if there is a metadata expression (outside of a transform) in the expression.
    /// </summary>
    internal static bool ContainsMetadataExpressionOutsideTransform(StringSegment expression)
    {
        ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

        GetReferencedItemNamesAndMetadata(expression, ref pair, ShredderOptions.MetadataOutsideTransforms);

        return pair.Metadata?.Count > 0;
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
            GetReferencedItemNamesAndMetadata(expression, ref pair, ShredderOptions.All);
        }

        return pair;
    }

    /// <summary>
    /// Given a subexpression, finds referenced item names and inserts them into the table
    /// as K=Name, V=String.Empty.
    /// </summary>
    /// <remarks>
    /// We can ignore any semicolons in the expression, since we're not itemizing it.
    /// </remarks>
    internal static void GetReferencedItemNamesAndMetadata(StringSegment expression, ref ItemsAndMetadataPair pair, ShredderOptions whatToShredFor)
    {
        bool includeItemTypes = (whatToShredFor & ShredderOptions.ItemTypes) != 0;
        bool includeMetadata = (whatToShredFor & ShredderOptions.MetadataOutsideTransforms) != 0;

        StringSegment worker = expression;

        while (!worker.IsEmpty)
        {
            if (TryParseItemName(ref worker, start: expression.Length - worker.Length, ref pair, out var itemNameSegment))
            {
                if (includeItemTypes)
                {
                    string itemName = WeakIntern(itemNameSegment.AsSpan());

                    pair.Items ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Items.Add(itemName);
                }
            }
            else if (TryParseMetadataName(ref worker, out itemNameSegment, out var metadataNameSegment))
            {
                if (includeMetadata)
                {
                    string? itemName;
                    string metadataKey;
                    string metadataName = WeakIntern(metadataNameSegment.AsSpan());

                    if (itemNameSegment.IsEmpty)
                    {
                        itemName = null;
                        metadataKey = metadataName;
                    }
                    else
                    {
                        itemName = WeakIntern(itemNameSegment.AsSpan());
                        metadataKey = $"{itemName}.{metadataName}";
                    }

                    pair.Metadata ??= new Dictionary<string, MetadataReference>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Metadata[metadataKey] = new MetadataReference(itemName, metadataName);
                }
            }
            else
            {
                worker = worker[1..];
            }
        }
    }

    private static bool TryParseItemName(
        ref StringSegment text,
        int start,
        ref ItemsAndMetadataPair pair,
        out StringSegment itemName)
    {
        itemName = default;

        if (!text.StartsWith("@("))
        {
            return false;
        }

        var current = text[2..].TrimStart();

        if (!TryParseValidName(ref current, out var name))
        {
            return false;
        }

        current = current.TrimStart();

        bool transformOrFunctionFound = true;

        // If there's an '->' eat it and the subsequent quoted expression or transform function
        while (transformOrFunctionFound && current.StartsWith("->"))
        {
            current = current[2..].TrimStart();

            bool isQuotedTransform = TryParseSingleQuotedExpression(ref current, out var quotedExpression);
            if (isQuotedTransform)
            {
                current = current.TrimStart();
                continue;
            }

            int nestedStart = start + text.Length - current.Length;

            if (TryParseItemExpressionCapture(ref current, nestedStart, out var capture))
            {
                current = current.TrimStart();
                continue;
            }

            if (!isQuotedTransform)
            {
                transformOrFunctionFound = false;
            }
        }

        if (!transformOrFunctionFound)
        {
            return false;
        }

        current = current.TrimStart();

        // If there's a ',', eat it and the subsequent quoted expression
        if (current is [',', ..])
        {
            current = current[1..].TrimStart();

            if (current is not ['\'', ..])
            {
                return false;
            }

            current = current[1..];
            int closingQuote = current.IndexOf('\'');
            if (closingQuote == -1)
            {
                return false;
            }

            // Look for metadata in the separator expression
            // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
            GetReferencedItemNamesAndMetadata(current[..closingQuote], ref pair, ShredderOptions.MetadataOutsideTransforms);

            current = current.Slice(startIndex: closingQuote + 1);
        }

        current = current.TrimStart();

        if (current is not [')', ..])
        {
            return false;
        }

        itemName = name;
        text = current[1..];
        return true;
    }

    private static bool TryParseMetadataName(
        ref StringSegment text,
        out StringSegment itemName,
        out StringSegment metadataName)
    {
        itemName = default;
        metadataName = default;

        if (!text.StartsWith("%("))
        {
            return false;
        }

        var current = text[2..].TrimStart();

        if (!TryParseValidName(ref current, out var firstPart))
        {
            return false;
        }

        current = current.TrimStart();

        // We don't know if it's an item or metadata name yet
        if (current is ['.', ..])
        {
            current = current[1..].TrimStart();

            if (!TryParseValidName(ref current, out var secondPart))
            {
                return false;
            }

            current = current.TrimStart();
            itemName = firstPart;
            metadataName = secondPart;
        }
        else
        {
            metadataName = firstPart;
        }

        if (current is not [')', ..])
        {
            itemName = default;
            metadataName = default;
            return false;
        }

        text = current[1..];
        return true;
    }

    /// <summary>
    /// Returns true if a valid name begins at the specified index.
    /// </summary>
    private static bool TryParseValidName(ref StringSegment text, out StringSegment name)
    {
        var span = text.AsSpan();

        if (span.IsEmpty || !XmlUtilities.IsValidInitialElementNameCharacter(span[0]))
        {
            name = default;
            return false;
        }

        int i = 1;

        while (i < span.Length && XmlUtilities.IsValidSubsequentElementNameCharacter(span[i]))
        {
            i++;
        }

        // '-' is a legitimate char in an item name, but we should match '->' as an arrow
        // in '@(foo->'x')' rather than as the last char of the item name.
        if (i < span.Length && span.Slice(i - 1, 2) is "->")
        {
            i -= 1;
        }

        name = text[..i];
        text = text[i..];
        return true;
    }

    /// <summary>
    /// Returns true if a single quoted subexpression begins at the specified index
    /// and ends before the specified end index.
    /// Leaves index one past the end of the second quote.
    /// </summary>
    private static bool TryParseSingleQuotedExpression(ref StringSegment text, out StringSegment quotedExpression)
    {
        var span = text.AsSpan();

        if (span is not ['\'', ..])
        {
            quotedExpression = default;
            return false;
        }

        int i = 1;

        while (i < span.Length && span[i] != '\'')
        {
            i++;
        }

        i++;

        if (i > text.Length)
        {
            quotedExpression = default;
            return false;
        }

        quotedExpression = text[..i];
        text = text[i..];
        return true;
    }

    /// <summary>
    /// Returns true if a item function subexpression begins at the specified index
    /// and ends before the specified end index.
    /// Leaves index one past the end of the closing paren.
    /// </summary>
    private static bool TryParseItemExpressionCapture(ref StringSegment text, int start, out ItemExpressionCapture capture)
    {
        int end = start + text.Length;
        var current = text;

        if (TryParseValidName(ref current, out var name))
        {
            current = current.TrimStart();

            int beforeArguments = end - current.Length;

            if (TryParseFunctionArguments(ref current, out var arguments))
            {
                int afterArguments = end - current.Length;

                int argumentsStart = beforeArguments + 1;
                int argumentsEnd = afterArguments - 1;

                if (argumentsEnd >= argumentsStart)
                {
                    arguments = arguments[1..^1];
                }

                ExpressionSegment functionName = new(name, start);
                ExpressionSegment functionArguments = !arguments.IsEmpty
                    ? new(arguments, argumentsStart)
                    : ExpressionSegment.Missing;

                int length = text.Length - current.Length;

                capture = new(new(text[..length], start), functionName, functionArguments);

                text = current;
                return true;
            }

            capture = default;
            return false;
        }

        capture = default;
        return false;
    }

    /// <summary>
    /// Scan for the closing bracket that matches the one we've already skipped;
    /// essentially, pushes and pops on a stack of parentheses to do this.
    /// Takes the expression and the index to start at.
    /// Returns the index of the matching parenthesis, or -1 if it was not found.
    /// </summary>
    private static bool TryParseFunctionArguments(ref StringSegment text, out StringSegment arguments)
    {
        var span = text.AsSpan();
        int index = 0;
        int nestLevel = 0;

        if (index < span.Length && span[index] == '(')
        {
            nestLevel++;
            index++;
        }
        else
        {
            arguments = default;
            return false;
        }

        // Scan for our closing ')'
        while (index < span.Length && nestLevel > 0)
        {
            switch (span[index])
            {
                case '\'' or '`' or '"':
                    {
                        char quoteChar = span[index];
                        index++;

                        // Scan for our closing quoteChar
                        while (index < span.Length)
                        {
                            if (span[index] == quoteChar)
                            {
                                break;
                            }

                            index++;
                        }
                    }

                    break;

                case '(':
                    nestLevel++;
                    break;

                case ')':
                    nestLevel--;
                    break;
            }

            index++;
        }

        if (nestLevel == 0)
        {
            arguments = text[..index];
            text = text[index..];
            return true;
        }
        else
        {
            arguments = default;
            return false;
        }
    }
}
