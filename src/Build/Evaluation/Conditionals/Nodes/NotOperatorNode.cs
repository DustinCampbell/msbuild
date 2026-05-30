// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Performs a logical NOT on its child expression.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NotOperatorNode(ExpressionNode expression) : UnaryOperatorNode(expression)
{
    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        if (!Expression.TryEvaluateAsBoolean(state, out bool boolValue))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                state.ElementLocation,
                "ExpressionDoesNotEvaluateToBoolean",
                Expression.GetUnexpandedValue(state),
                Expression.GetExpandedValue(state),
                state.Condition);
        }

        result = !boolValue;
        return true;
    }

    /// <summary>
    ///  Returns unexpanded value with '!' prepended. Useful for error messages.
    /// </summary>
    public override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
    {
        return "!" + Expression.GetUnexpandedValue(state);
    }

    public override bool IsUnexpandedValueEmpty() => false;

    /// <summary>
    ///  Returns expanded value with '!' prepended. Useful for error messages.
    /// </summary>
    public override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
    {
        return "!" + Expression.GetExpandedValue(state);
    }

    public override string DebuggerDisplay
        => $"(not {Expression.DebuggerDisplay})";
}
