// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Base class for nodes that are operators (have children in the parse tree)
/// </summary>
internal abstract class BinaryOperatorExpressionNode(ExpressionNode left, ExpressionNode right) : ExpressionNode
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

    /// <summary>
    /// Value after any item and property expressions are expanded
    /// </summary>
    /// <returns></returns>
    internal override string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    /// <inheritdoc cref="ExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty()
        => Left.IsUnexpandedValueEmpty() && Right.IsUnexpandedValueEmpty();

    /// <summary>
    /// Value before any item and property expressions are expanded
    /// </summary>
    /// <returns></returns>
    internal override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    /// <summary>
    /// If any expression nodes cache any state for the duration of evaluation,
    /// now's the time to clean it up
    /// </summary>
    internal override void ResetState()
    {
        Left.ResetState();
        Right.ResetState();
    }
}
