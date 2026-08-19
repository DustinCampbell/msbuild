// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Text;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Splits an expression into fragments at semicolons, except where the
    /// semicolons are in a macro or separator expression.
    /// Fragments are trimmed and empty fragments discarded.
    /// </summary>
    /// <remarks>
    /// These complex cases prevent us from doing a simple split on ';':
    ///  (1) Macro expression: @(foo->'xxx;xxx')
    ///  (2) Separator expression: @(foo, 'xxx;xxx')
    ///  (3) Combination: @(foo->'xxx;xxx', 'xxx;xxx')
    ///  We must not split on semicolons in macro or separator expressions like these.
    /// </remarks>
    internal readonly struct SemiColonTokenizer : IEnumerable<StringSegment>
    {
        private readonly StringSegment _expression;

        /// <summary>
        ///  Initializes a tokenizer over <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression to tokenize.</param>
        public SemiColonTokenizer(string expression)
            : this(expression.AsSegment())
        {
        }

        /// <summary>
        ///  Initializes a tokenizer over <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The expression segment to tokenize.</param>
        public SemiColonTokenizer(StringSegment expression)
        {
            _expression = expression;
        }

        public Enumerator GetEnumerator() => new Enumerator(_expression);

        IEnumerator<StringSegment> IEnumerable<StringSegment>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal struct Enumerator : IEnumerator<StringSegment>
        {
            private readonly StringSegment _expression;
            private StringSegment _current;
            private int _index;

            public Enumerator(StringSegment expression)
            {
                _expression = expression;
                _index = 0;
                _current = default;
            }

            public StringSegment Current
            {
                get { return _current; }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                int segmentStart = _index;
                bool insideItemList = false;
                bool insideQuotedPart = false;
                StringSegment segment;
                string buffer = _expression.Buffer;
                int offset = _expression.Offset;

                // Walk along the string, keeping track of whether we are in an item list expression.
                // If we hit a semi-colon or the end of the string and we aren't in an item list,
                // add the segment to the list.
                for (; _index < _expression.Length; _index++)
                {
                    switch (buffer[offset + _index])
                    {
                        case ';':
                            if (!insideItemList)
                            {
                                // End of segment, so add it to the list
                                segment = GetExpressionSubstring(segmentStart, _index - segmentStart);
                                if (segment.HasValue)
                                {
                                    _current = segment;
                                    return true;
                                }

                                // Move past this semicolon
                                segmentStart = _index + 1;
                            }

                            break;
                        case '@':
                            // An '@' immediately followed by a '(' is the start of an item list
                            if (_expression.Length > _index + 1 &&
                                buffer[offset + _index + 1] == '(')
                            {
                                // Start of item expression
                                insideItemList = true;
                            }

                            break;
                        case ')':
                            if (insideItemList && !insideQuotedPart)
                            {
                                // End of item expression
                                insideItemList = false;
                            }

                            break;
                        case '\'':
                            if (insideItemList)
                            {
                                // Start or end of quoted expression in item expression
                                insideQuotedPart = !insideQuotedPart;
                            }

                            break;
                    }
                }

                // Reached the end of the string: what's left is another segment
                _current = GetExpressionSubstring(segmentStart, _expression.Length - segmentStart);
                return _current.HasValue;
            }

            public void Reset()
            {
                _current = default;
                _index = 0;
            }

            /// <summary>
            ///  Returns a whitespace-trimmed view over a portion of the expression.
            /// </summary>
            /// <param name="startIndex">Start index of the substring.</param>
            /// <param name="length">Length of the substring.</param>
            /// <returns>
            ///  The trimmed segment, or a null segment if the trimmed substring is empty.
            /// </returns>
            private StringSegment GetExpressionSubstring(int startIndex, int length)
            {
                int endIndex = startIndex + length;
                string buffer = _expression.Buffer;
                int offset = _expression.Offset;

                while (startIndex < endIndex && char.IsWhiteSpace(buffer[offset + startIndex]))
                {
                    startIndex++;
                }
                while (startIndex < endIndex && char.IsWhiteSpace(buffer[offset + endIndex - 1]))
                {
                    endIndex--;
                }
                if (startIndex < endIndex)
                {
                    return _expression.Slice(startIndex, endIndex - startIndex);
                }

                return default;
            }
        }
    }
}
