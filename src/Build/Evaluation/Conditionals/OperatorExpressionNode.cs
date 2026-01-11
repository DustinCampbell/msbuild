// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Base class for nodes that are operators (have children in the parse tree).
/// </summary>
internal abstract class OperatorExpressionNode : GenericExpressionNode
{
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
    /// Value after any item and property expressions are expanded.
    /// </summary>
    internal override string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    /// <summary>
    /// Value before any item and property expressions are expanded.
    /// </summary>
    internal override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;
}
