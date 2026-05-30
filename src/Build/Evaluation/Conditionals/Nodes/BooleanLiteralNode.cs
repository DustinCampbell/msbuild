// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Represents a boolean literal keyword (true/false/on/off/yes/no and their negated forms).
///  The boolean value is resolved at parse time, avoiding runtime string-to-bool conversion.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class BooleanLiteralNode(bool value) : ExpressionNode
{
    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        result = value;
        return true;
    }

    public override bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        result = default;
        return false;
    }

    public override bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version? result)
    {
        result = null;
        return false;
    }

    public override bool IsUnexpandedValueEmpty()
        => false;

    public override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => value ? "true" : "false";

    public override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => value ? "true" : "false";

    public override void ResetState()
    {
    }

    public override string DebuggerDisplay
        => value ? "true" : "false";
}
