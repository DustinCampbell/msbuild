// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

#if NETFRAMEWORK
using Microsoft.Build.Utilities;
#endif

using static Microsoft.NET.StringTools.Strings;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
    internal static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression)
        => GetReferencedItemExpressions(expression, 0, expression.Length);

    private static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression, int start, int end)
        => new(expression, start, end);

    /// <summary>
    /// Returns true if there is a metadata expression (outside of a transform) in the expression.
    /// </summary>
    internal static bool ContainsMetadataExpressionOutsideTransform(string expression)
    {
        ItemsAndMetadataPair pair = new ItemsAndMetadataPair(null, null);

        GetReferencedItemNamesAndMetadata(expression.AsMemory(), ref pair, ShredderOptions.MetadataOutsideTransforms);

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
            GetReferencedItemNamesAndMetadata(expression.AsMemory(), ref pair, ShredderOptions.All);
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
    internal static void GetReferencedItemNamesAndMetadata(ReadOnlyMemory<char> expression, ref ItemsAndMetadataPair pair, ShredderOptions whatToShredFor)
    {
        bool includeItemTypes = (whatToShredFor & ShredderOptions.ItemTypes) != 0;
        bool includeMetadata = (whatToShredFor & ShredderOptions.MetadataOutsideTransforms) != 0;

        ReadOnlyMemory<char> memory = expression;

        while (!memory.IsEmpty)
        {
            if (TryParseItemName(ref memory, start: expression.Length - memory.Length, ref pair, out var itemNameMemory))
            {
                if (includeItemTypes)
                {
                    string itemName = WeakIntern(itemNameMemory.Span);

                    pair.Items ??= new HashSet<string>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Items.Add(itemName);
                }
            }
            else if (TryParseMetadataName(ref memory, out itemNameMemory, out var metadataNameMemory))
            {
                if (includeMetadata)
                {
                    string? itemName;
                    string metadataKey;
                    string metadataName = WeakIntern(metadataNameMemory.Span);

                    if (itemNameMemory.IsEmpty)
                    {
                        itemName = null;
                        metadataKey = metadataName;
                    }
                    else
                    {
                        itemName = WeakIntern(itemNameMemory.Span);
                        metadataKey = $"{itemName}.{metadataName}";
                    }

                    pair.Metadata ??= new Dictionary<string, MetadataReference>(MSBuildNameIgnoreCaseComparer.Default);
                    pair.Metadata[metadataKey] = new MetadataReference(itemName, metadataName);
                }
            }
            else
            {
                memory = memory[1..];
            }
        }
    }

    private static bool TryParseItemName(
        ref ReadOnlyMemory<char> memory,
        int start,
        ref ItemsAndMetadataPair pair,
        out ReadOnlyMemory<char> itemNameMemory)
    {
        itemNameMemory = default;

        if (!memory.Span.StartsWith("@("))
        {
            return false;
        }

        var current = memory[2..].TrimStart();

        if (!TryParseValidName(ref current, out var name))
        {
            return false;
        }

        current = current.TrimStart();

        bool transformOrFunctionFound = true;

        // If there's an '->' eat it and the subsequent quoted expression or transform function
        while (transformOrFunctionFound && current.Span.StartsWith("->"))
        {
            current = current[2..].TrimStart();

            bool isQuotedTransform = TryParseSingleQuotedExpression(ref current, out var quotedExpression);
            if (isQuotedTransform)
            {
                current = current.TrimStart();
                continue;
            }

            int nestedStart = start + memory.Length - current.Length;

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
        if (current.Span is [',', ..])
        {
            current = current[1..].TrimStart();

            if (current.Span is not ['\'', ..])
            {
                return false;
            }

            current = current[1..];
            int closingQuote = current.Span.IndexOf('\'');
            if (closingQuote == -1)
            {
                return false;
            }

            // Look for metadata in the separator expression
            // e.g., @(foo, '%(bar)') contains batchable metadata 'bar'
            GetReferencedItemNamesAndMetadata(current[..closingQuote], ref pair, ShredderOptions.MetadataOutsideTransforms);

            current = current.Slice(start: closingQuote + 1);
        }

        current = current.TrimStart();

        if (current.Span is not [')', ..])
        {
            return false;
        }

        itemNameMemory = name;
        memory = current[1..];
        return true;
    }

    private static bool TryParseMetadataName(
        ref ReadOnlyMemory<char> memory,
        out ReadOnlyMemory<char> itemNameMemory,
        out ReadOnlyMemory<char> metadataNameMemory)
    {
        itemNameMemory = default;
        metadataNameMemory = default;

        if (!memory.Span.StartsWith("%("))
        {
            return false;
        }

        var current = memory[2..].TrimStart();

        if (!TryParseValidName(ref current, out var firstPart))
        {
            return false;
        }

        current = current.TrimStart();

        // We don't know if it's an item or metadata name yet
        if (current.Span is ['.', ..])
        {
            current = current[1..].TrimStart();

            if (!TryParseValidName(ref current, out var secondPart))
            {
                return false;
            }

            current = current.TrimStart();
            itemNameMemory = firstPart;
            metadataNameMemory = secondPart;
        }
        else
        {
            metadataNameMemory = firstPart;
        }

        if (current.Span is not [')', ..])
        {
            itemNameMemory = default;
            metadataNameMemory = default;
            return false;
        }

        memory = current[1..];
        return true;
    }

    /// <summary>
    /// Returns true if a valid name begins at the specified index.
    /// </summary>
    private static bool TryParseValidName(ref ReadOnlyMemory<char> memory, out ReadOnlyMemory<char> name)
    {
        var span = memory.Span;

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

        name = memory[..i];
        memory = memory[i..];
        return true;
    }

    /// <summary>
    /// Returns true if a single quoted subexpression begins at the specified index
    /// and ends before the specified end index.
    /// Leaves index one past the end of the second quote.
    /// </summary>
    private static bool TryParseSingleQuotedExpression(ref ReadOnlyMemory<char> memory, out ReadOnlyMemory<char> quotedExpression)
    {
        var span = memory.Span;

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

        quotedExpression = memory[..i];
        memory = memory[i..];
        return true;
    }

    /// <summary>
    /// Returns true if a item function subexpression begins at the specified index
    /// and ends before the specified end index.
    /// Leaves index one past the end of the closing paren.
    /// </summary>
    private static bool TryParseItemExpressionCapture(ref ReadOnlyMemory<char> memory, int start, out ItemExpressionCapture capture)
    {
        var current = memory;

        if (TryParseValidName(ref current, out var name))
        {
            int index = name.Length;

            // Eat any whitespace between the function name and its arguments
            int whitespaceLength = FirstNonWhitespaceIndex(current.Span);

            index += whitespaceLength;
            current = current[whitespaceLength..];

            if (TryParseFunctionArguments(ref current, out var arguments))
            {
                int argumentsStart = index + 1;
                index += arguments.Length;
                int argumentsEnd = index - 1;

                if (argumentsEnd > argumentsStart)
                {
                    arguments = arguments[1..^1];
                }

                TextToken functionName = new(name, start);
                TextToken functionArguments = !arguments.IsEmpty
                    ? new(arguments, argumentsStart)
                    : TextToken.Missing;

                capture = new(new(memory[..index], start), functionName, functionArguments);

                memory = current;
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
    private static bool TryParseFunctionArguments(ref ReadOnlyMemory<char> memory, out ReadOnlyMemory<char> arguments)
    {
        var span = memory.Span;
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
            arguments = memory[..index];
            memory = memory[index..];
            return true;
        }
        else
        {
            arguments = default;
            return false;
        }
    }

    /// <summary>
    /// Returns true if the next two characters at the specified index
    /// are the specified sequence.
    /// Leaves index one past the second character.
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
    /// Returns true if the character at the specified index
    /// is the specified char.
    /// Leaves index one past the character.
    /// </summary>
    private static bool Sink(string expression, ref int i, char c)
    {
        if (i < expression.Length && expression[i] == c)
        {
            i++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Moves past all whitespace starting at the specified index.
    /// Returns the next index, possibly the string length.
    /// </summary>
    /// <remarks>
    /// Char.IsWhitespace() is not identical in behavior to regex's \s character class,
    /// but it's extremely close, and it's what we use in conditional expressions.
    /// </remarks>
    /// <param name="span">The expression to process.</param>
    private static int FirstNonWhitespaceIndex(ReadOnlySpan<char> span)
    {
        int i = 0;

        while (i < span.Length && char.IsWhiteSpace(span[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// Moves past all whitespace starting at the specified index.
    /// Returns the next index, possibly the string length.
    /// </summary>
    /// <remarks>
    /// Char.IsWhitespace() is not identical in behavior to regex's \s character class,
    /// but it's extremely close, and it's what we use in conditional expressions.
    /// </remarks>
    /// <param name="expression">The expression to process.</param>
    /// <param name="i">The start location for skipping whitespace, contains the next non-whitespace character on exit.</param>
    private static void SinkWhitespace(string expression, ref int i)
    {
        while (i < expression.Length && char.IsWhiteSpace(expression[i]))
        {
            i++;
        }
    }

    /// <summary>
    /// Returns true if a valid name begins at the specified index.
    /// Leaves index one past the end of the name.
    /// </summary>
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

        return end > i;
    }

    /// <summary>
    /// Returns true if a item function subexpression begins at the specified index
    /// and ends before the specified end index.
    /// Leaves index one past the end of the closing paren.
    /// </summary>
    private static ItemExpressionCapture? SinkItemFunctionExpression(string expression, int startTransform, ref int i, int end)
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

                TextToken functionName = TextToken.FromBounds(expression, startTransform, endFunctionName);
                TextToken functionArguments = TextToken.Missing;

                if (endFunctionArguments > startFunctionArguments)
                {
                    functionArguments = TextToken.FromBounds(expression, startFunctionArguments, endFunctionArguments);
                }

                ItemExpressionCapture capture = new(TextToken.FromBounds(expression, startTransform, i), functionName, functionArguments);

                return capture;
            }

            return null;
        }
        else
        {
            return null;
        }
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

                    if (character is '\'' or '`' or '"')
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

        return nestLevel == 0;
    }

    /// <summary>
    /// Skip all characters until we find the matching quote character.
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
}
