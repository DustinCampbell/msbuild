// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
    internal ref struct ReferencedItemExpressionsEnumerator(StringSegment expression)
    {
        private readonly StringSegment _expression = expression;

        private StringSegment _unprocessed = expression;
        private int _start;
        private ItemExpressionCapture _current;

        public readonly ItemExpressionCapture Current => _current;

        public bool MoveNext()
        {
            StringSegment worker = _unprocessed;
            int end = _start + _unprocessed.Length;

            while (!worker.IsEmpty)
            {
                int start = end - worker.Length;

                if (TryParseReferencedItemExpression(ref worker, start, out var capture))
                {
                    _current = capture;
                    _unprocessed = worker;
                    _start = _expression.Length - _unprocessed.Length;
                    return true;
                }
                else
                {
                    worker = worker[1..];
                }
            }

            _current = default;
            return false;
        }

        private static bool TryParseReferencedItemExpression(ref StringSegment text, int start, out ItemExpressionCapture result)
        {
            result = default;

            if (!text.StartsWith("@("))
            {
                return false;
            }

            int end = start + text.Length;

            var current = text[2..].TrimStart();
            int nameStart = end - current.Length - start;

            if (!TryParseValidName(ref current, out var name))
            {
                return false;
            }

            // Grab the name, but continue to verify it's a well-formed expression
            // before we store it.
            ExpressionSegment itemName = new(name, nameStart);

            current = current.TrimStart();

            bool transformOrFunctionFound = true;
            using RefArrayBuilder<ItemExpressionCapture> transformExpressions = new(initialCapacity: 4);

            // If there's an '->' eat it and the subsequent quoted expression or transform function
            while (transformOrFunctionFound && current.StartsWith("->"))
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

            ExpressionSegment separator = ExpressionSegment.Missing;

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

                int separatorStart = end - current.Length;
                separator = new(current[..closingQuote], separatorStart - start);

                current = current[(closingQuote + 1)..].TrimStart();
            }

            current = current.TrimStart();

            if (current is not [')', ..])
            {
                return false;
            }

            // Create an expression capture that encompasses the entire expression between the @( and the )
            // with the item name and any separator contained within it
            // and each transform expression contained within it (i.e. each ->XYZ)
            int length = text.Length - current.Length + 1;
            result = new(new(text[..length], start), itemName, separator, transformExpressions.ToImmutable());
            text = current[1..];

            return true;
        }
    }
}
