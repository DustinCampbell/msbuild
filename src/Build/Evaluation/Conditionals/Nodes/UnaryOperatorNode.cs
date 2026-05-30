// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Base class for unary operator nodes that have a single child expression.
/// </summary>
internal abstract class UnaryOperatorNode(ExpressionNode expression) : ExpressionNode
{
    public ExpressionNode Expression { get; } = expression;

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

    public override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    public override bool IsUnexpandedValueEmpty()
        => Expression.IsUnexpandedValueEmpty();

    public override void ResetState()
        => Expression.ResetState();
}
