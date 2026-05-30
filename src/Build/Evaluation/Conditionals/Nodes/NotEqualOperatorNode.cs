// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Compares for inequality
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NotEqualOperatorNode(ExpressionNode left, ExpressionNode right) : EqualityComparisonNode(left, right)
{
    protected override bool Compare(double left, double right)
        => left != right;

    protected override bool Compare(bool left, bool right)
        => left != right;

    protected override bool Compare(string left, string right)
        => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public override string DebuggerDisplay
        => $"(!= {Left.DebuggerDisplay} {Right.DebuggerDisplay})";
}
