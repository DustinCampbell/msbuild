// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation;

internal abstract class UnaryOperatorNode(GenericExpressionNode expression) : GenericExpressionNode
{
    public GenericExpressionNode Expression => expression;

    internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        result = BoolEvaluate(state);
        return true;
    }

    internal abstract bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state);

    internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        result = default;
        return false;
    }

    internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, [NotNullWhen(true)] out Version? result)
    {
        result = null;
        return false;
    }

    /// <summary>
    /// If any expression nodes cache any state for the duration of evaluation,
    /// now's the time to clean it up
    /// </summary>
    internal override void ResetState()
    {
        Expression.ResetState();
    }

    #region REMOVE_COMPAT_WARNING

    internal override bool DetectAnd()
    {
        // Read the state of the current node
        bool detectedAnd = PossibleAndCollision;

        // Reset the flags on the current node
        PossibleAndCollision = false;

        // Process the node expression
        bool detectAndExpression = Expression.DetectAnd();

        return detectedAnd || detectAndExpression;
    }

    internal override bool DetectOr()
    {
        // Read the state of the current node
        bool detectedOr = PossibleOrCollision;

        // Reset the flags on the current node
        PossibleOrCollision = false;

        // Process the expression
        bool detectAndExpression = Expression.DetectOr();

        return detectedOr || detectAndExpression;
    }

    #endregion
}
