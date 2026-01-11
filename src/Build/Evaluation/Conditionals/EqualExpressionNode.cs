// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Compares for equality.
/// </summary>
internal sealed class EqualExpressionNode(GenericExpressionNode left, GenericExpressionNode right) : MultipleComparisonNode(left, right)
{
    protected override bool Compare(double left, double right)
        => left == right;

    protected override bool Compare(bool left, bool right)
        => left == right;

    protected override bool Compare(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal override string GetDebuggerDisplay() => $"(== {Left.GetDebuggerDisplay()} {Right.GetDebuggerDisplay()})";
}
