// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
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
    }
}
