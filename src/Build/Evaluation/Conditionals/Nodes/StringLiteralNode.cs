// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Node representing a non-expandable string literal in a condition expression.
///  The parser guarantees that numeric and boolean literals are represented by
///  <see cref="NumberLiteralNode"/> and <see cref="BooleanLiteralNode"/> respectively,
///  so this node will never evaluate as numeric or boolean.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal sealed class StringLiteralNode(StringSegment value)
    : ExpressionNode(ExpressionNodeFlags.None)
{
    private readonly StringSegment _value = value;

    /// <summary>
    ///  The string value, lazily materialized from <see cref="_value"/>.
    /// </summary>
    private string? _valueText;

    internal string ValueText => _valueText ??= _value.ToString();

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        // Non-expandable boolean literals are parsed as BooleanLiteralNode.
        result = default;
        return false;
    }

    public override bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result)
    {
        // Non-expandable numeric literals are parsed as NumberLiteralNode.
        result = default;
        return false;
    }

    public override bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version? result)
    {
        // Non-expandable strings can still be valid versions (e.g., "1.2.3")
        // since only simple numeric values are parsed as NumberLiteralNode.
#if NET
        return Version.TryParse(_value.AsSpan(), out result);
#else
        return Version.TryParse(ValueText, out result);
#endif
    }

    public override bool EvaluatesToEmpty(ConditionEvaluator.IConditionEvaluationState state)
        => _value.Length == 0;

    public override bool IsUnexpandedValueEmpty()
        => _value.Length == 0;

    public override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => ValueText;

    public override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => ValueText;

    public override void ResetState()
    {
        // Nothing to reset — this node has no expansion state.
    }

    public override string DebuggerDisplay => $"\"{ValueText}\"";
}
