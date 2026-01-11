// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Compares for inequality
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NotEqualExpressionNode(GenericExpressionNode left, GenericExpressionNode right) : MultipleComparisonNode(left, right)
{
    /// <summary>
    /// Compare numbers
    /// </summary>
    protected override bool Compare(double left, double right)
        => left != right;

    /// <summary>
    /// Compare booleans
    /// </summary>
    protected override bool Compare(bool left, bool right)
        => left != right;

    /// <summary>
    /// Compare strings
    /// </summary>
    protected override bool Compare(string left, string right)
        => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    internal override string GetDebuggerDisplay()
        => $"(!= {Left.GetDebuggerDisplay()} {Right.GetDebuggerDisplay()})";
}
