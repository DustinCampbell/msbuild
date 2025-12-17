// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
