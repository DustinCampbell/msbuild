// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Base class for nodes that are unary operators (have a single child in the parse tree)
/// </summary>
internal abstract class UnaryOperatorExpressionNode(ExpressionNode expression) : ExpressionNode
{
    internal ExpressionNode Expression { get; } = expression;

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

    internal override string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    internal override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    internal override bool IsUnexpandedValueEmpty()
        => Expression.IsUnexpandedValueEmpty();

    internal override void ResetState()
        => Expression.ResetState();
}
