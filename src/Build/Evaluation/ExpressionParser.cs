// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal static class ExpressionParser
{
    internal static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression)
        => GetReferencedItemExpressions(expression, 0, expression.Length);

    private static ReferencedItemExpressionsEnumerator GetReferencedItemExpressions(string expression, int start, int end)
        => new(expression, start, end);

    internal ref struct ReferencedItemExpressionsEnumerator
    {
        private readonly string _expression;
        private readonly int _end;
        private int _position;

        public ReferencedItemExpressionsEnumerator(string expression, int start, int end)
        {
            _expression = expression;
            _end = end;

            _position = expression.IndexOf('@', start, end - start);
            if (_position < 0)
            {
                _position = int.MaxValue;
            }
        }

        public ItemExpressionCapture Current { get; private set; }

        public bool MoveNext()
        {
            for (; _position < _end; _position++)
            {
                if (!Sink(_expression, ref _position, _end, '@', '('))
                {
                    continue;
                }

                // Start of a possible item list expression

                // Store the index to backtrack to if this doesn't turn out to be a well
                // formed expression. (Subtract one for the increment when we loop around.)
                int restartPoint = _position - 1;

                // Store the expression's start point
                int startPoint = _position - 2;

                SinkWhitespace(_expression, ref _position);

                int startOfName = _position;

                if (!SinkValidName(_expression, ref _position, _end))
                {
                    _position = restartPoint;
                    continue;
                }

                // '-' is a legitimate char in an item name, but we should match '->' as an arrow
                // in '@(foo->'x')' rather than as the last char of the item name.
                // The old regex accomplished this by being "greedy"
                if (_end > _position && _expression[_position - 1] == '-' && _expression[_position] == '>')
                {
                    _position--;
                }

                // Grab the name, but continue to verify it's a well-formed expression
                // before we store it.
                var itemName = TextToken.FromBounds(_expression, startOfName, _position);

                SinkWhitespace(_expression, ref _position);

                bool transformOrFunctionFound = true;
                using RefArrayBuilder<ItemExpressionCapture> transformExpressions = new(initialCapacity: 4);

                // If there's an '->' eat it and the subsequent quoted expression or transform function
                while (Sink(_expression, ref _position, _end, '-', '>') && transformOrFunctionFound)
                {
                    SinkWhitespace(_expression, ref _position);
                    int startTransform = _position;

                    bool isQuotedTransform = SinkSingleQuotedExpression(_expression, ref _position, _end);
                    if (isQuotedTransform)
                    {
                        int startQuoted = startTransform + 1;
                        int endQuoted = _position - 1;

                        transformExpressions.Add(new(TextToken.FromBounds(_expression, startQuoted, endQuoted)));

                        SinkWhitespace(_expression, ref _position);
                        continue;
                    }

                    startTransform = _position;
                    ItemExpressionCapture? functionCapture = SinkItemFunctionExpression(_expression, startTransform, ref _position, _end);
                    if (functionCapture is ItemExpressionCapture functionCaptureValue)
                    {
                        transformExpressions.Add(functionCaptureValue);

                        SinkWhitespace(_expression, ref _position);
                        continue;
                    }

                    if (!isQuotedTransform && functionCapture == null)
                    {
                        _position = restartPoint;
                        transformOrFunctionFound = false;
                    }
                }

                if (!transformOrFunctionFound)
                {
                    continue;
                }

                SinkWhitespace(_expression, ref _position);

                TextToken separator = TextToken.Missing;

                // If there's a ',', eat it and the subsequent quoted expression
                if (Sink(_expression, ref _position, ','))
                {
                    SinkWhitespace(_expression, ref _position);

                    if (!Sink(_expression, ref _position, '\''))
                    {
                        _position = restartPoint;
                        continue;
                    }

                    int closingQuote = _expression.IndexOf('\'', _position);
                    if (closingQuote == -1)
                    {
                        _position = restartPoint;
                        continue;
                    }

                    // separatorStart = _position - startPoint;
                    separator = TextToken.FromBounds(_expression, _position, closingQuote);

                    _position = closingQuote + 1;
                }

                SinkWhitespace(_expression, ref _position);

                if (!Sink(_expression, ref _position, ')'))
                {
                    _position = restartPoint;
                    continue;
                }

                int endPoint = _position;
                _position--;

                // Create an expression capture that encompasses the entire expression between the @( and the )
                // with the item name and any separator contained within it
                // and each transform expression contained within it (i.e. each ->XYZ)
                ItemExpressionCapture expressionCapture = new(TextToken.FromBounds(_expression, startPoint, endPoint), itemName, separator, transformExpressions.ToImmutable());

                Current = expressionCapture;
                ++_position;

                return true;
            }

            Current = default;

            return false;
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

    internal readonly struct TextToken
    {
        public static readonly TextToken Missing = new(ReadOnlyMemory<char>.Empty, -1);

        public ReadOnlyMemory<char> Memory { get; }
        public int Start { get; }

        private TextToken(ReadOnlyMemory<char> text, int start)
        {
            Memory = text;
            Start = start;
        }

        public static TextToken FromBounds(string expression, int start, int end)
            => new(expression.AsMemory(start, end - start), start);
    }

    /// <summary>
    /// Represents one substring for a single successful capture.
    /// </summary>
    internal readonly struct ItemExpressionCapture
    {
        private readonly TextToken _text;
        private readonly TextToken _itemType;
        private readonly TextToken _separator;
        private readonly TextToken _functionName;
        private readonly TextToken _functionArguments;
        private readonly ItemExpressionCapture[] _captures;

        public ItemExpressionCapture(TextToken text)
            : this(text, itemType: TextToken.Missing, separator: TextToken.Missing,
                   captures: [], functionName: TextToken.Missing, functionArguments: TextToken.Missing)
        {
        }

        public ItemExpressionCapture(TextToken text, TextToken itemType, TextToken separator, ImmutableArray<ItemExpressionCapture> captures)
            : this(text, itemType, separator, captures, functionName: TextToken.Missing, functionArguments: TextToken.Missing)
        {
        }

        public ItemExpressionCapture(TextToken text, TextToken functionName, TextToken functionArguments)
            : this(text, itemType: TextToken.Missing, separator: TextToken.Missing,
                   captures: [], functionName, functionArguments)
        {
        }

        private ItemExpressionCapture(
            TextToken text,
            TextToken itemType,
            TextToken separator,
            ImmutableArray<ItemExpressionCapture> captures,
            TextToken functionName,
            TextToken functionArguments)
        {
            _text = text;
            _itemType = itemType;
            _separator = separator;
            _captures = ImmutableCollectionsMarshal.AsArray(captures)!;
            _functionName = functionName;
            _functionArguments = functionArguments;
        }

        /// <summary>
        /// Captures within this capture.
        /// </summary>
        public ImmutableArray<ItemExpressionCapture> Captures => ImmutableCollectionsMarshal.AsImmutableArray(_captures);

        /// <summary>
        /// The position in the original string where the first character of the captured
        /// substring was found.
        /// </summary>
        public int Index => _text.Start;

        /// <summary>
        /// The length of the captured substring.
        /// </summary>
        public int Length => _text.Memory.Length;

        /// <summary>
        /// Gets the captured substring from the input string.
        /// </summary>
        public ReadOnlySpan<char> Value => _text.Memory.Span;

        /// <summary>
        /// Gets the captured itemtype.
        /// </summary>
        public ReadOnlySpan<char> ItemType => _itemType.Memory.Span;

        /// <summary>
        /// Gets the captured itemtype.
        /// </summary>
        public ReadOnlySpan<char> Separator => _separator.Memory.Span;

        /// <summary>
        /// The starting character of the separator.
        /// </summary>
        public int SeparatorStart => _separator.Start;

        /// <summary>
        /// The function name, if any, within this expression.
        /// </summary>
        public ReadOnlySpan<char> FunctionName => _functionName.Memory.Span;

        /// <summary>
        /// The function arguments, if any, within this expression.
        /// </summary>
        public ReadOnlySpan<char> FunctionArguments => _functionArguments.Memory.Span;

        /// <summary>
        /// Gets the captured substring from the input string.
        /// </summary>
        public override string ToString()
            => _text.Memory.ToString();
    }
}
