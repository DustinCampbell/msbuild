// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expressions;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
internal abstract class ExpressionNode(SourceSpan source)
{
    public SourceSpan Source => source;

    public StringSegment Text => source.Text;

    public int Start => source.Start;
    public int End => source.End;
    public int Length => source.Length;
}
