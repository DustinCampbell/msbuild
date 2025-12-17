// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
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
}
