// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Evaluates a numeric comparison, such as less-than, or greater-or-equal-than
/// Does not update conditioned properties table.
/// </summary>
internal abstract class NumericComparisonExpressionNode(GenericExpressionNode left, GenericExpressionNode right) : BinaryOperatorNode(left, right)
{
    /// <summary>
    /// Compare numbers.
    /// </summary>
    protected abstract bool Compare(double left, double right);

    /// <summary>
    /// Compare Versions. This is only intended to compare version formats like "A.B.C.D" which can otherwise not be compared numerically.
    /// </summary>
    protected abstract bool Compare(Version left, Version right);

    /// <summary>
    /// Compare mixed numbers and Versions.
    /// </summary>
    protected abstract bool Compare(Version left, double right);

    /// <summary>
    /// Compare mixed numbers and Versions.
    /// </summary>
    protected abstract bool Compare(double left, Version right);

    /// <summary>
    /// Evaluate as boolean.
    /// </summary>
    internal override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
    {
        bool isLeftNum = Left.TryNumericEvaluate(state, out double leftNum);

        if (isLeftNum)
        {
            if (Right.TryNumericEvaluate(state, out double rightNum))
            {
                return Compare(leftNum, rightNum);
            }
            else if (Right.TryVersionEvaluate(state, out Version? rightVersion))
            {
                return Compare(leftNum, rightVersion);
            }
        }
        else if (Left.TryVersionEvaluate(state, out Version? leftVersion))
        {
            if (Right.TryVersionEvaluate(state, out Version? rightVersion))
            {
                return Compare(leftVersion, rightVersion);
            }
            else if (Right.TryNumericEvaluate(state, out double rightNum))
            {
                return Compare(leftVersion, rightNum);
            }
        }

        ProjectErrorUtilities.ThrowInvalidProject(
            state.ElementLocation,
            "ComparisonOnNonNumericExpression",
            state.Condition,
            /* helpfully display unexpanded token and expanded result in error message */
            isLeftNum ? Right.GetUnexpandedValue(state) : Left.GetUnexpandedValue(state),
            isLeftNum ? Right.GetExpandedValue(state) : Left.GetExpandedValue(state));

        return false; // Unreachable
    }
}
