// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Base class for binary operator nodes that have left and right children.
/// </summary>
internal abstract class BinaryOperatorNode(ExpressionNode left, ExpressionNode right) : ExpressionNode
{
    public ExpressionNode Left { get; } = left;

    public ExpressionNode Right { get; } = right;

    public override bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        result = default;
        return false;
    }

    public override bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version? result)
    {
        result = null;
        return false;
    }

    public override string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    public override bool IsUnexpandedValueEmpty()
        => Left.IsUnexpandedValueEmpty() && Right.IsUnexpandedValueEmpty();

    public override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    public override void ResetState()
    {
        Left.ResetState();
        Right.ResetState();
    }
}
