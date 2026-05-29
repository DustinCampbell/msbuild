// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Performs logical AND on children
    /// Does not update conditioned properties table
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    internal sealed class AndExpressionNode : BinaryOperatorExpressionNode
    {
        internal AndExpressionNode(ExpressionNode leftChild, ExpressionNode rightChild)
            : base(leftChild, rightChild)
        {
        }

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            if (!LeftChild.TryEvaluateAsBoolean(state, out bool leftBool))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                     state.ElementLocation,
                     "ExpressionDoesNotEvaluateToBoolean",
                     LeftChild.GetUnexpandedValue(state),
                     LeftChild.GetExpandedValue(state),
                     state.Condition);
            }

            if (!leftBool)
            {
                // Short circuit
                result = false;
                return true;
            }
            else
            {
                if (!RightChild.TryEvaluateAsBoolean(state, out bool rightBool))
                {
                    ProjectErrorUtilities.ThrowInvalidProject(
                         state.ElementLocation,
                         "ExpressionDoesNotEvaluateToBoolean",
                         RightChild.GetUnexpandedValue(state),
                         RightChild.GetExpandedValue(state),
                         state.Condition);
                }

                result = rightBool;
                return true;
            }
        }

        internal override string DebuggerDisplay => $"(and {LeftChild.DebuggerDisplay} {RightChild.DebuggerDisplay})";

        #region REMOVE_COMPAT_WARNING
        private bool _possibleAndCollision = true;
        internal override bool PossibleAndCollision
        {
            set { _possibleAndCollision = value; }
            get { return _possibleAndCollision; }
        }
        #endregion
    }
}
