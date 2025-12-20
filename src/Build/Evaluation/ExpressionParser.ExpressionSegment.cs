// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

internal static partial class ExpressionParser
{
    internal readonly struct ExpressionSegment
    {
        public static readonly ExpressionSegment Missing = new(StringSegment.Empty, -1);

        public bool IsEmpty => Text.IsEmpty;
        public bool IsMissing => Start == -1;

        public StringSegment Text { get; }
        public int Start { get; }

        public ExpressionSegment(StringSegment text, int start)
        {
            Text = text;
            Start = start;
        }

        public override string ToString()
            => Text.ToString();
    }
}
