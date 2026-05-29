// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Represents a number - evaluates as numeric.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class NumberExpressionNode : ExpressionNode
{
    private readonly ReadOnlyMemory<char> _value;

    private string? _valueText;
    private (bool, double)? _cachedNumericValue;
    private (bool, Version?)? _cachedVersionValue;

    public NumberExpressionNode(ReadOnlyMemory<char> value)
    {
        Assumed.False(value.IsEmpty, "NumericExpressionNode cannot have empty value");
        _value = value;
    }

    private string ValueText => _valueText ??= _value.ToString();

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        result = default;
        return false;
    }

    public override bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        (bool success, double numericValue) = _cachedNumericValue ??= Compute();

        result = numericValue;
        return success;

#if NET
        (bool, double) Compute()
            => ConversionUtilities.TryConvertDecimalOrHexToDouble(_value.Span, out double value)
                ? (true, value)
                : (false, default);
#else
        (bool, double) Compute()
            => ConversionUtilities.TryConvertDecimalOrHexToDouble(ValueText, out double value)
                ? (true, value)
                : (false, default);
#endif
    }

    public override bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version? result)
    {
        (bool success, Version? versionValue) = _cachedVersionValue ??= Compute();

        result = versionValue;
        return success;

#if NET
        (bool, Version?) Compute()
            => Version.TryParse(_value.Span, out Version? value)
                ? (true, value)
                : (false, default);
#else
        (bool, Version?) Compute()
            => Version.TryParse(ValueText, out Version? value)
                ? (true, value)
                : (false, default);
#endif
    }

    /// <inheritdoc cref="ExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty()
        => false;

    /// <summary>
    /// Get the unexpanded value
    /// </summary>
    internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => ValueText;

    /// <summary>
    /// Get the expanded value
    /// </summary>
    internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => ValueText;

    /// <summary>
    /// If any expression nodes cache any state for the duration of evaluation,
    /// now's the time to clean it up
    /// </summary>
    internal override void ResetState()
    {
    }

    internal override string DebuggerDisplay
        => $"#\"{ValueText}\")";
}
