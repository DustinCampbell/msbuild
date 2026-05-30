// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Performs a logical AND on left and right children with short-circuit evaluation.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class AndOperatorNode(ExpressionNode left, ExpressionNode right) : BinaryOperatorNode(left, right)
{
    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        if (!Left.TryEvaluateAsBoolean(state, out bool leftResult))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                 state.ElementLocation,
                 "ExpressionDoesNotEvaluateToBoolean",
                 Left.GetUnexpandedValue(state),
                 Left.GetExpandedValue(state),
                 state.Condition);
        }

        if (!leftResult)
        {
            // Short circuit
            result = false;
            return true;
        }

        if (!Right.TryEvaluateAsBoolean(state, out bool rightResult))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                 state.ElementLocation,
                 "ExpressionDoesNotEvaluateToBoolean",
                 Right.GetUnexpandedValue(state),
                 Right.GetExpandedValue(state),
                 state.Condition);
        }

        result = rightResult;
        return true;
    }

    public override string DebuggerDisplay
        => $"(and {Left.DebuggerDisplay} {Right.DebuggerDisplay})";
}
