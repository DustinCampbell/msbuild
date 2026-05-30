// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Abstract base class for all nodes in a condition expression tree.
/// </summary>
internal abstract class ExpressionNode
{
    public abstract bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result);

    public abstract bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result);

    public abstract bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version? result);

    /// <summary>
    ///  Returns true if this node evaluates to an empty string without performing full expansion.
    /// </summary>
    public virtual bool EvaluatesToEmpty(ConditionEvaluator.IConditionEvaluationState state)
        => false;

    /// <summary>
    ///  Returns the value of this node after expanding item and property expressions.
    /// </summary>
    public abstract string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state);

    /// <summary>
    ///  Returns the value of this node before expanding item and property expressions.
    /// </summary>
    public abstract string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state);

    /// <summary>
    ///  Returns true if the unexpanded value is empty without performing any expansion.
    /// </summary>
    public abstract bool IsUnexpandedValueEmpty();

    /// <summary>
    ///  Resets any cached state from a previous evaluation.
    /// </summary>
    public abstract void ResetState();

    /// <summary>
    ///  Evaluates this node as a boolean, throwing if evaluation fails.
    /// </summary>
    public bool Evaluate(ConditionEvaluator.IConditionEvaluationState state)
    {
        if (!TryEvaluateAsBoolean(state, out bool boolValue))
        {
            ProjectErrorUtilities.ThrowInvalidProject(
                state.ElementLocation,
                "ConditionNotBooleanDetail",
                state.Condition,
                GetExpandedValue(state));
        }

        return boolValue;
    }

    /// <summary>
    ///  Gets a display string for this node for use in the debugger.
    /// </summary>
    public virtual string? DebuggerDisplay { get; }
}
