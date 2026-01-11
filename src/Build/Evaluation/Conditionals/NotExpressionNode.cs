// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Performs logical NOT on left child
/// Does not update conditioned properties table
/// </summary>
internal sealed class NotExpressionNode(GenericExpressionNode left) : UnaryOperatorNode(left)
{
    /// <summary>
    /// Evaluate as boolean.
    /// </summary>
    internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
    {
        if (!Expression.TryBoolEvaluate(state, out bool boolValue))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                state.ElementLocation,
                "ExpressionDoesNotEvaluateToBoolean",
                Expression.GetUnexpandedValue(state),
                Expression.GetExpandedValue(state),
                state.Condition);
        }

        return !boolValue;
    }

    /// <summary>
    /// Returns unexpanded value with '!' prepended. Useful for error messages.
    /// </summary>
    internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => "!" + Expression.GetUnexpandedValue(state);

    /// <inheritdoc cref="GenericExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty() => false;

    /// <summary>
    /// Returns expanded value with '!' prepended. Useful for error messages.
    /// </summary>
    internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => "!" + Expression.GetExpandedValue(state);

    internal override string GetDebuggerDisplay() => $"(not {Expression.GetDebuggerDisplay()})";
}
