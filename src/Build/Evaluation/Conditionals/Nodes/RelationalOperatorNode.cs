// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Evaluates a numeric comparison, such as less-than, or greater-or-equal-than.
///  Does not update conditioned properties table.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class RelationalOperatorNode(RelationalOperationKind kind, ExpressionNode left, ExpressionNode right) : BinaryOperatorNode(left, right)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Compare(double left, double right)
        => kind switch
        {
            RelationalOperationKind.LessThan => left < right,
            RelationalOperationKind.GreaterThan => left > right,
            RelationalOperationKind.LessThanOrEqual => left <= right,
            RelationalOperationKind.GreaterThanOrEqual => left >= right,

            _ => false,
        };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Compare(Version left, Version right)
        => kind switch
        {
            RelationalOperationKind.LessThan => left < right,
            RelationalOperationKind.GreaterThan => left > right,
            RelationalOperationKind.LessThanOrEqual => left <= right,
            RelationalOperationKind.GreaterThanOrEqual => left >= right,

            _ => false,
        };

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

        // Mixed double/Version comparisons: when one side is a number and the other
        // is a Version, we compare the number against the Version's Major component.
        // If the Major differs from the number, compare them as doubles.
        // If they're equal, the Version is always considered "greater" than the plain
        // number (e.g., 6.0.0.0 > 6 is true) because the extra version components
        // make it larger. We encode this by delegating to Compare with sentinel values
        // that place the Version side higher (1.0) and the number side lower (0.0),
        // so the operator kind produces the correct result without needing dedicated
        // mixed-type methods.

        if (isLeftNum && isRightVersion)
        {
            Assumed.NotNull(rightVersion);

            result = leftNum != rightVersion.Major
                ? Compare(leftNum, rightVersion.Major)
                : Compare(0.0, 1.0); // Version (right) is greater
            return true;
        }

        if (isLeftVersion && isRightNum)
        {
            Assumed.NotNull(leftVersion);

            result = leftVersion.Major != rightNum
                ? Compare(leftVersion.Major, rightNum)
                : Compare(1.0, 0.0); // Version (left) is greater
            return true;
        }

        ProjectErrorUtilities.ThrowInvalidProject(
            state.ElementLocation,
            "ComparisonOnNonNumericExpression",
            state.Condition,

            // helpfully display unexpanded token and expanded result in error message
            isLeftNum || isLeftVersion ? Right.GetUnexpandedValue(state) : Left.GetUnexpandedValue(state),
            isLeftNum || isLeftVersion ? Right.GetExpandedValue(state) : Left.GetExpandedValue(state));

        result = false;
        return true;
    }

    public override string DebuggerDisplay
        => kind switch
        {
            RelationalOperationKind.LessThan => $"(< {Left.DebuggerDisplay} {Right.DebuggerDisplay})",
            RelationalOperationKind.GreaterThan => $"(> {Left.DebuggerDisplay} {Right.DebuggerDisplay})",
            RelationalOperationKind.LessThanOrEqual => $"(<= {Left.DebuggerDisplay} {Right.DebuggerDisplay})",
            RelationalOperationKind.GreaterThanOrEqual => $"(>= {Left.DebuggerDisplay} {Right.DebuggerDisplay})",

            _ => $"(?? {Left.DebuggerDisplay} {Right.DebuggerDisplay})",
        };
}
