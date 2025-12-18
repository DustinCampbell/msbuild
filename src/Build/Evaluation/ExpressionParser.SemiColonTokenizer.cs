// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using static Microsoft.NET.StringTools.Strings;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
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
    internal readonly ref struct SemiColonTokenizer(ReadOnlyMemory<char> expression)
    {
        private readonly ReadOnlyMemory<char> _expression = expression;

        public Enumerator GetEnumerator()
            => new(_expression);

        internal ref struct Enumerator(ReadOnlyMemory<char> expression)
        {
            private readonly ReadOnlyMemory<char> _expression = expression;

            private ReadOnlyMemory<char> _worker = expression;
            private string? _current = null;

            public readonly string Current => _current!;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                string? segment;
                bool insideItemList = false;
                bool insideQuotedPart = false;

                // Walk along the string, keeping track of whether we are in an item list expression.
                // If we hit a semi-colon or the end of the string and we aren't in an item list,
                // add the segment to the list.

                ReadOnlySpan<char> span = _worker.Span;
                int index = 0;

                while (index < span.Length)
                {
                    switch (span[index])
                    {
                        case ';' when !insideItemList:
                            // End of segment
                            if (TryGetString(span[..index], out segment))
                            {
                                _worker = _worker[(index + 1)..];
                                _current = segment;
                                return true;
                            }

                            // Slice span after the ;
                            span = span[(index + 1)..];
                            _worker = _worker[(index + 1)..];

                            // Set the index to 0 and continue the loop.
                            // Note: We *don't* fall through to increment the index.
                            index = 0;
                            continue;

                        case '@' when IndexMatches(span, index: index + 1, '('):
                            // An '@' immediately followed by a '(' is the start of an item list
                            insideItemList = true;
                            break;

                        case ')' when insideItemList && !insideQuotedPart:
                            // End of item expression
                            insideItemList = false;

                            break;

                        case '\'' when insideItemList:
                            // Start or end of quoted expression in item expression
                            insideQuotedPart = !insideQuotedPart;
                            break;
                    }

                    index++;
                }

                // Reached the end of the string: what's left is another segment
                if (TryGetString(span, out segment))
                {
                    _current = segment;
                    _worker = ReadOnlyMemory<char>.Empty;
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                _worker = _expression;
                _current = null;
            }

            /// <summary>
            /// Returns a whitespace-trimmed and possibly interned substring of the expression.
            /// </summary>
            /// <returns>Equivalent to _expression.Substring(startIndex, length).Trim() or null if the trimmed substring is empty.</returns>
            private static bool TryGetString(ReadOnlySpan<char> span, [NotNullWhen(true)] out string? result)
            {
                span = span.Trim();
                result = !span.IsEmpty ? WeakIntern(span) : null;

                return result is not null;
            }

            private static bool IndexMatches(ReadOnlySpan<char> span, int index, char ch)
                => index < span.Length && span[index] == ch;
        }
    }
}
