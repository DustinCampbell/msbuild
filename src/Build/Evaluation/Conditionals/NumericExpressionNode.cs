// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Represents a number - evaluates as numeric.
/// </summary>
internal sealed class NumericExpressionNode : OperandExpressionNode
{
    private readonly string _value;

    internal NumericExpressionNode(ReadOnlyMemory<char> value)
    {
        ErrorUtilities.VerifyThrow(!value.IsEmpty, "NumericExpressionNode cannot have empty value");
        _value = value.ToString();
    }

    internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        // Note: A numeric expression cannot be evaluated as a boolean.
        result = default;
        return false;
    }

    internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
        => ConversionUtilities.TryConvertDecimalOrHexToDouble(_value, out result);

    internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, [NotNullWhen(true)] out Version? result)
        => Version.TryParse(_value, out result);

    /// <inheritdoc cref="GenericExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty() => false;

    /// <summary>
    /// Get the unexpanded value.
    /// </summary>
    internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => _value;

    /// <summary>
    /// Get the expanded value.
    /// </summary>
    internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => _value;

    internal override string GetDebuggerDisplay() => $"#\"{_value}\")";
}
