// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Performs logical OR on children
/// Does not update conditioned properties table
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class OrExpressionNode(ExpressionNode left, ExpressionNode right) : BinaryOperatorExpressionNode(left, right)
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

        if (leftResult)
        {
            // Short circuit
            result = true;
            return true;
        }

        if (!Right.TryEvaluateAsBoolean(state, out bool rightBool))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                state.ElementLocation,
                "ExpressionDoesNotEvaluateToBoolean",
                Right.GetUnexpandedValue(state),
                Right.GetExpandedValue(state),
                state.Condition);
        }

        result = rightBool;
        return true;
    }

    internal override string DebuggerDisplay
        => $"(or {Left.DebuggerDisplay} {Right.DebuggerDisplay})";
}
