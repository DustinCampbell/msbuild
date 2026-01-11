// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Performs logical OR on children
/// Does not update conditioned properties table
/// </summary>
internal sealed class OrExpressionNode : BinaryOperatorNode
{
    public OrExpressionNode(GenericExpressionNode left, GenericExpressionNode right)
        : base(left, right)
    {
        PossibleOrCollision = true;
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

        if (leftBool)
        {
            // Short circuit
            return true;
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

    internal override string GetDebuggerDisplay() => $"(or {Left.GetDebuggerDisplay()} {Right.GetDebuggerDisplay()})";

    #region REMOVE_COMPAT_WARNING

    internal override bool PossibleOrCollision { get; set; }

    #endregion
}
