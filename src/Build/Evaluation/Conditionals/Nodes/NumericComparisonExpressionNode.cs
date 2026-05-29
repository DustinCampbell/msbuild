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
            bool isLeftNum = Left.TryEvaluateAsNumber(state, out double leftNum);
            bool isRightNum = Right.TryEvaluateAsNumber(state, out double rightNum);

            // Both sides are numeric — most common case, no need to check versions.
            if (isLeftNum && isRightNum)
            {
                result = Compare(leftNum, rightNum);
                return true;
            }

            bool isLeftVersion = Left.TryEvaluateAsVersion(state, out Version? leftVersion);
            bool isRightVersion = Right.TryEvaluateAsVersion(state, out Version? rightVersion);

            if (isLeftVersion && isRightVersion)
            {
                Assumed.NotNull(leftVersion);
                Assumed.NotNull(rightVersion);

                result = Compare(leftVersion, rightVersion);
                return true;
            }

            if (isLeftNum && isRightVersion)
            {
                Assumed.NotNull(rightVersion);

                result = Compare(leftNum, rightVersion);
                return true;
            }

            if (isLeftVersion && isRightNum)
            {
                Assumed.NotNull(leftVersion);

                result = Compare(leftVersion, rightNum);
                return true;
            }

            ProjectErrorUtilities.ThrowInvalidProject(
                state.ElementLocation,
                "ComparisonOnNonNumericExpression",
                state.Condition,
                /* helpfully display unexpanded token and expanded result in error message */
                isLeftNum || isLeftVersion ? Right.GetUnexpandedValue(state) : Left.GetUnexpandedValue(state),
                isLeftNum || isLeftVersion ? Right.GetExpandedValue(state) : Left.GetExpandedValue(state));

            result = false;
            return true;
        }
    }
}
