// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
    /// <summary>
    /// Represents one substring for a single successful capture.
    /// </summary>
    internal readonly struct ItemExpressionCapture
    {
        private readonly ExpressionSegment _text;
        private readonly ExpressionSegment _itemType;
        private readonly ExpressionSegment _separator;
        private readonly ExpressionSegment _functionName;
        private readonly ExpressionSegment _functionArguments;
        private readonly ItemExpressionCapture[] _captures;

        public ItemExpressionCapture(ExpressionSegment text)
            : this(text, itemType: ExpressionSegment.Missing, separator: ExpressionSegment.Missing,
                   captures: [], functionName: ExpressionSegment.Missing, functionArguments: ExpressionSegment.Missing)
        {
        }

        public ItemExpressionCapture(ExpressionSegment text, ExpressionSegment itemType, ExpressionSegment separator, ImmutableArray<ItemExpressionCapture> captures)
            : this(text, itemType, separator, captures, functionName: ExpressionSegment.Missing, functionArguments: ExpressionSegment.Missing)
        {
        }

        public ItemExpressionCapture(ExpressionSegment text, ExpressionSegment functionName, ExpressionSegment functionArguments)
            : this(text, itemType: ExpressionSegment.Missing, separator: ExpressionSegment.Missing,
                   captures: [], functionName, functionArguments)
        {
        }

        private ItemExpressionCapture(
            ExpressionSegment text,
            ExpressionSegment itemType,
            ExpressionSegment separator,
            ImmutableArray<ItemExpressionCapture> captures,
            ExpressionSegment functionName,
            ExpressionSegment functionArguments)
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
        public int Length => _text.Text.Length;

        /// <summary>
        /// Gets the captured substring from the input string.
        /// </summary>
        public ExpressionSegment Value => _text;

        /// <summary>
        /// Gets the captured itemtype.
        /// </summary>
        public ExpressionSegment ItemType => _itemType;

        /// <summary>
        /// Gets the captured itemtype.
        /// </summary>
        public ExpressionSegment Separator => _separator;

        /// <summary>
        /// The starting character of the separator.
        /// </summary>
        public int SeparatorStart => _separator.Start;

        /// <summary>
        /// The function name, if any, within this expression.
        /// </summary>
        public ExpressionSegment FunctionName => _functionName;

        /// <summary>
        /// The function arguments, if any, within this expression.
        /// </summary>
        public ExpressionSegment FunctionArguments => _functionArguments;

        /// <summary>
        /// Gets the captured substring from the input string.
        /// </summary>
        public override string ToString()
            => _text.Text.ToString();
    }
}
