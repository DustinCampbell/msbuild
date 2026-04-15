// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework.Utilities;

namespace Microsoft.Build;

#pragma warning disable CS9113 // Parameter is unread.

internal static partial class Assumed
{
    [InterpolatedStringHandler]
    public ref struct UnreachableInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        private StringBuilderHelper _builder = new(literalLength);

        public readonly void AppendLiteral(string value)
            => _builder.AppendLiteral(value);

        public readonly void AppendFormatted<TValue>(TValue value)
            => _builder.AppendFormatted(value);

        public readonly void AppendFormatted<TValue>(TValue value, string format)
            where TValue : IFormattable
            => _builder.AppendFormatted(value, format);

        public string GetFormattedText()
            => _builder.GetFormattedText();
    }
}
