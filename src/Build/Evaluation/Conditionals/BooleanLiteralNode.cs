// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Represents a boolean literal (true, false, on, off, yes, no) in a conditional expression.
    /// </summary>
    internal sealed class BooleanLiteralNode : OperandExpressionNode
    {
        private readonly bool _value;
        private readonly string _text;

        internal BooleanLiteralNode(bool value, string text)
        {
            _value = value;
            _text = text;
        }

        public bool Value => _value;

        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            result = _value;
            return true;
        }

        internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
        {
            result = default;
            return false;
        }

        internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, out Version? result)
        {
            result = default;
            return false;
        }

        internal override bool IsUnexpandedValueEmpty()
            => false;

        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
            => _text;

        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
            => _text;

        internal override void ResetState()
        {
        }

        internal override string DebuggerDisplay => $"#\"{_text}\")";
    }
}
