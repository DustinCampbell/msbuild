// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Collections;

#if !NET
using Microsoft.Build.Utilities;
#endif

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
    internal ref struct ReferencedItemExpressionsEnumerator(ReadOnlyMemory<char> expression)
    {
        private readonly ReadOnlyMemory<char> _expression = expression;

        private ReadOnlyMemory<char> _worker = expression;
        private int _start;
        private ItemExpressionCapture _current;

        public readonly ItemExpressionCapture Current => _current;

        public bool MoveNext()
        {
            ReadOnlyMemory<char> memory = _worker;
            int end = _start + _worker.Length;

            while (!memory.IsEmpty)
            {
                int start = end - memory.Length;

                if (TryParseReferencedItemExpression(ref memory, start, out var capture))
                {
                    _current = capture;
                    _worker = memory;
                    _start = _expression.Length - _worker.Length;
                    return true;
                }
                else
                {
                    memory = memory[1..];
                }
            }

            _current = default;
            return false;
        }

        private static bool TryParseReferencedItemExpression(ref ReadOnlyMemory<char> memory, int start, out ItemExpressionCapture result)
        {
            result = default;

            if (!memory.Span.StartsWith("@("))
            {
                return false;
            }

            int end = start + memory.Length;

            var current = memory[2..].TrimStart();
            int nameStart = end - current.Length;

            if (!TryParseValidName(ref current, out var name))
            {
                return false;
            }

            // Grab the name, but continue to verify it's a well-formed expression
            // before we store it.
            TextToken itemName = new(name, nameStart);

            current = current.TrimStart();

            bool transformOrFunctionFound = true;
            using RefArrayBuilder<ItemExpressionCapture> transformExpressions = new(initialCapacity: 4);

            // If there's an '->' eat it and the subsequent quoted expression or transform function
            while (transformOrFunctionFound && current.Span.StartsWith("->"))
            {
                current = current[2..].TrimStart();

                int transformStart = end - current.Length;

                if (TryParseSingleQuotedExpression(ref current, out var quotedExpression))
                {
                    // Remove quotes.
                    quotedExpression = quotedExpression[1..^1];
                    transformStart++;

                    transformExpressions.Add(new(new(quotedExpression, transformStart)));

                    current = current.TrimStart();
                    continue;
                }

                if (TryParseItemExpressionCapture(ref current, transformStart, out var capture))
                {
                    transformExpressions.Add(capture);

                    current = current.TrimStart();
                    continue;
                }

                transformOrFunctionFound = false;
            }

            if (!transformOrFunctionFound)
            {
                return false;
            }

            current = current.TrimStart();

            TextToken separator = TextToken.Missing;

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

                int separatorStart = end - current.Length;
                separator = new(current[..closingQuote], separatorStart);

                current = current[(closingQuote + 1)..].TrimStart();
            }

            current = current.TrimStart();

            if (current.Span is not [')', ..])
            {
                return false;
            }

            // Create an expression capture that encompasses the entire expression between the @( and the )
            // with the item name and any separator contained within it
            // and each transform expression contained within it (i.e. each ->XYZ)
            int length = memory.Length - current.Length + 1;
            result = new(new(memory[..length], start), itemName, separator, transformExpressions.ToImmutable());
            memory = current[1..];

            return true;
        }
    }
}
