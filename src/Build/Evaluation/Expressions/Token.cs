// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expressions;

internal readonly struct Token(TokenKind kind, SourceSpan source, TokenFlags flags = TokenFlags.None)
{
    public TokenKind Kind => kind;
    public SourceSpan Source => source;
    public TokenFlags Flags => flags;

    public StringSegment Text => source.Text;
    public int Start => source.Start;

    public int End => source.Start + source.Length;
    public int Length => source.Length;
}

[Flags]
internal enum TokenFlags : byte
{
    None = 0,

    ContainsEscapeCharacters = 1 << 0,
    ContainsPercent = 1 << 1,
    ContainsDollar = 1 << 2,
    ContainsAtSign = 1 << 3
}
