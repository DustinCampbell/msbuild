// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Evaluation.Conditionals;

internal sealed class BooleanLiteralNode(string text, bool value) : OperandExpressionNode
{
    public string Text => text;
    public bool Value => value;

    internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        result = Value;
        return true;
    }

    internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => Text;

    internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => Text;

    internal override bool IsUnexpandedValueEmpty() => false;

    internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        result = default;
        return false;
    }

    internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, [NotNullWhen(true)] out Version? result)
    {
        result = default;
        return false;
    }

    internal override string GetDebuggerDisplay()
        => Value ? "(true)" : "(false)";
}
