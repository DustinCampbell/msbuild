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
internal sealed class AndExpressionNode : BinaryOperatorNode
{
    public AndExpressionNode(GenericExpressionNode left, GenericExpressionNode right)
        : base(left, right)
    {
        PossibleAndCollision = true;
    }

    /// <summary>
    /// Evaluate as boolean
    /// </summary>
    internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
    {
        if (!Left.TryBoolEvaluate(state, out bool leftBool))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                 state.ElementLocation,
                 "ExpressionDoesNotEvaluateToBoolean",
                 Left.GetUnexpandedValue(state),
                 Left.GetExpandedValue(state),
                 state.Condition);
        }

        if (!leftBool)
        {
            // Short circuit
            return false;
        }

        if (!Right.TryBoolEvaluate(state, out bool rightBool))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                 state.ElementLocation,
                 "ExpressionDoesNotEvaluateToBoolean",
                 Right.GetUnexpandedValue(state),
                 Right.GetExpandedValue(state),
                 state.Condition);
        }

        return rightBool;
    }

    internal override string DebuggerDisplay
        => $"(and {Left.DebuggerDisplay} {Right.DebuggerDisplay})";

    #region REMOVE_COMPAT_WARNING

    internal override bool PossibleAndCollision { get; set; }

    #endregion
}
