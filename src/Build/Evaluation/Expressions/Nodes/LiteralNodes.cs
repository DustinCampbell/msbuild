// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// Represents a string literal: 'foo' or "bar" or `baz`.
/// </summary>
internal sealed class StringLiteralNode(Token token) : ExpressionNode(token.Source)
{
    public SourceSpan Value { get; } = token.Source[1..^1];
}

internal sealed class CompositeStringNode(Token token, ImmutableArray<ExpressionNode> parts) : ExpressionNode(token.Source)
{
    public ImmutableArray<ExpressionNode> Parts => parts;
}

internal sealed class SimpleTextNode(SourceSpan source) : ExpressionNode(source);

internal sealed class EscapedTextNode(StringSegment unescapedText, SourceSpan source) : ExpressionNode(source)
{
    public StringSegment UnescapedText => unescapedText;
}

/// <summary>
/// Represents a numeric literal: 42, 3.14, 0xFF.
/// </summary>
internal sealed class NumericLiteralNode(Token token) : ExpressionNode(token.Source);
