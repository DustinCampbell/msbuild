// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Performs logical NOT on child
    /// Does not update conditioned properties table
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class NotExpressionNode : UnaryOperatorExpressionNode
    {
        internal NotExpressionNode(ExpressionNode child)
            : base(child)
        {
        }

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            if (!Child.TryEvaluateAsBoolean(state, out bool boolValue))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    state.ElementLocation,
                    "ExpressionDoesNotEvaluateToBoolean",
                    Child.GetUnexpandedValue(state),
                    Child.GetExpandedValue(state),
                    state.Condition);
            }

            result = !boolValue;
            return true;
        }

        /// <summary>
        /// Returns unexpanded value with '!' prepended. Useful for error messages.
        /// </summary>
        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return "!" + Child.GetUnexpandedValue(state);
        }

        /// <inheritdoc cref="ExpressionNode"/>
        internal override bool IsUnexpandedValueEmpty() => false;

        /// <summary>
        /// Returns expanded value with '!' prepended. Useful for error messages.
        /// </summary>
        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return "!" + Child.GetExpandedValue(state);
        }

        internal override string DebuggerDisplay => $"(not {Child.DebuggerDisplay})";
    }
}
