// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Evaluates a numeric comparison, such as less-than, or greater-or-equal-than
    /// Does not update conditioned properties table.
    /// </summary>
    internal abstract class NumericComparisonExpressionNode : BinaryOperatorExpressionNode
    {
        protected NumericComparisonExpressionNode(ExpressionNode leftChild, ExpressionNode rightChild)
            : base(leftChild, rightChild)
        {
        }

        /// <summary>
        /// Compare numbers
        /// </summary>
        protected abstract bool Compare(double left, double right);

        /// <summary>
        /// Compare Versions. This is only intended to compare version formats like "A.B.C.D" which can otherwise not be compared numerically
        /// </summary>
        protected abstract bool Compare(Version left, Version right);

        /// <summary>
        /// Compare mixed numbers and Versions
        /// </summary>
        protected abstract bool Compare(Version left, double right);

        /// <summary>
        /// Compare mixed numbers and Versions
        /// </summary>
        protected abstract bool Compare(double left, Version right);

        /// <summary>
        /// Evaluate as boolean
        /// </summary>
        public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            bool isLeftNum = LeftChild.TryEvaluateAsNumber(state, out double leftNum);
            bool isLeftVersion = LeftChild.TryEvaluateAsVersion(state, out Version leftVersion);
            bool isRightNum = RightChild.TryEvaluateAsNumber(state, out double rightNum);
            bool isRightVersion = RightChild.TryEvaluateAsVersion(state, out Version rightVersion);

            if ((!isLeftNum && !isLeftVersion) || (!isRightNum && !isRightVersion))
            {
                ProjectErrorUtilities.ThrowInvalidProject(
                    state.ElementLocation,
                    "ComparisonOnNonNumericExpression",
                    state.Condition,
                    /* helpfully display unexpanded token and expanded result in error message */
                    isLeftNum ? RightChild.GetUnexpandedValue(state) : LeftChild.GetUnexpandedValue(state),
                    isLeftNum ? RightChild.GetExpandedValue(state) : LeftChild.GetExpandedValue(state));
            }

            result = (isLeftNum, isLeftVersion, isRightNum, isRightVersion) switch
            {
                (true, _, true, _) => Compare(leftNum, rightNum),
                (_, true, _, true) => Compare(leftVersion, rightVersion),
                (true, _, _, true) => Compare(leftNum, rightVersion),
                (_, true, true, _) => Compare(leftVersion, rightNum),

                _ => false
            };
            return true;
        }
    }
}
