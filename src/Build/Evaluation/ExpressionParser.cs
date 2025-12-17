// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
    internal static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression)
        => GetReferencedItemExpressions(expression, 0, expression.Length);

    private static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression, int start, int end)
        => new(expression, start, end);

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
