// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.NET.StringTools;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Splits an expression into fragments at semicolons, except where the semicolons are inside a macro
///  or separator expression. Fragments are trimmed and empty fragments are discarded.
/// </summary>
/// <remarks>
///  These complex cases prevent a simple split on ';':
///  <list type="number">
///   <item>Macro expression: <c>@(foo-&gt;'xxx;xxx')</c></item>
///   <item>Separator expression: <c>@(foo, 'xxx;xxx')</c></item>
///   <item>Combination: <c>@(foo-&gt;'xxx;xxx', 'xxx;xxx')</c></item>
///  </list>
///  Semicolons inside macro or separator expressions like these must not be split on.
///  <para>
///   This <see langword="struct"/> acts as its own enumerator, so it can be consumed with <c>foreach</c>
///   or a collection-expression spread (<c>[.. ...]</c>) without any heap allocation. It intentionally
///   does not implement <see cref="System.Collections.Generic.IEnumerable{T}"/>, so it cannot be boxed
///   through LINQ or <c>List{T}.AddRange</c>.
///  </para>
/// </remarks>
internal ref struct ExpressionSplitter(string expression)
{
    /// <summary>
    ///  Returns a fresh enumerator positioned before the first fragment. Enables <c>foreach</c> and
    ///  collection-expression spreads over the value.
    /// </summary>
    public readonly Enumerator GetEnumerator()
        => new(expression);

    public ref struct Enumerator(string expression)
    {
        private readonly string _expression = expression;

        private string? _current;
        private int _index;

        public readonly string Current => _current!;

        public bool MoveNext()
        {
            int segmentStart = _index;
            bool insideItemList = false;
            bool insideQuotedPart = false;

            // Walk along the string, tracking whether we are inside an item list expression. When we
            // hit a semicolon outside an item list (or reach the end), the span since the previous
            // split point is the next segment.
            for (; _index < _expression.Length; _index++)
            {
                switch (_expression[_index])
                {
                    case ';':
                        if (!insideItemList)
                        {
                            string? segment = GetSubstring(segmentStart, _index - segmentStart);
                            if (segment is not null)
                            {
                                _current = segment;
                                return true;
                            }

                            // Empty segment; move past this semicolon and keep scanning.
                            segmentStart = _index + 1;
                        }

                        break;
                    case '@':
                        // An '@' immediately followed by a '(' starts an item list.
                        if (_index + 1 < _expression.Length && _expression[_index + 1] == '(')
                        {
                            insideItemList = true;
                        }

                        break;
                    case ')':
                        // A ')' outside a quoted part ends the item list.
                        if (insideItemList && !insideQuotedPart)
                        {
                            insideItemList = false;
                        }

                        break;
                    case '\'':
                        // A quote toggles the quoted part of an item list (e.g. a transform or separator).
                        if (insideItemList)
                        {
                            insideQuotedPart = !insideQuotedPart;
                        }

                        break;
                }
            }

            // Reached the end of the string: whatever remains is the final segment.
            _current = GetSubstring(segmentStart, _expression.Length - segmentStart);
            return _current is not null;
        }

        /// <summary>
        ///  Returns a whitespace-trimmed and possibly interned substring of the expression, or
        ///  <see langword="null"/> if the trimmed substring is empty.
        /// </summary>
        /// <param name="startIndex">Start index of the substring.</param>
        /// <param name="length">Length of the substring before trimming.</param>
        private readonly string? GetSubstring(int startIndex, int length)
        {
            int endIndex = startIndex + length;

            while (startIndex < endIndex && char.IsWhiteSpace(_expression[startIndex]))
            {
                startIndex++;
            }

            while (startIndex < endIndex && char.IsWhiteSpace(_expression[endIndex - 1]))
            {
                endIndex--;
            }

            return startIndex < endIndex
                ? Strings.WeakIntern(_expression.AsSpan(startIndex, endIndex - startIndex))
                : null;
        }
    }
}
