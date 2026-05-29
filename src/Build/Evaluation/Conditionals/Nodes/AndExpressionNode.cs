// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Performs logical AND on children
/// Does not update conditioned properties table
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class AndExpressionNode(ExpressionNode leftChild, ExpressionNode rightChild) : BinaryOperatorExpressionNode(leftChild, rightChild)
{
    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        if (!LeftChild.TryEvaluateAsBoolean(state, out bool leftResult))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                 state.ElementLocation,
                 "ExpressionDoesNotEvaluateToBoolean",
                 LeftChild.GetUnexpandedValue(state),
                 LeftChild.GetExpandedValue(state),
                 state.Condition);
        }

        if (!leftResult)
        {
            // Short circuit
            result = false;
            return true;
        }

        if (!RightChild.TryEvaluateAsBoolean(state, out bool rightResult))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                 state.ElementLocation,
                 "ExpressionDoesNotEvaluateToBoolean",
                 RightChild.GetUnexpandedValue(state),
                 RightChild.GetExpandedValue(state),
                 state.Condition);
        }

        result = rightResult;
        return true;
    }

    internal override string DebuggerDisplay
        => $"(and {LeftChild.DebuggerDisplay} {RightChild.DebuggerDisplay})";
}
