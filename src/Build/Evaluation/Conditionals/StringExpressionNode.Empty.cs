// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Evaluation;

internal abstract partial class StringExpressionNode
{
    private sealed class Empty : StringExpressionNode
    {
        public static StringExpressionNode Instance { get; } = new Empty();

        private Empty()
        {
        }

        public override string Value => string.Empty;

        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            result = default;
            return false;
        }

        internal override bool TryNumericEvaluate(ConditionEvaluator.IConditionEvaluationState state, out double result)
        {
            result = default;
            return false;
        }

        internal override bool TryVersionEvaluate(ConditionEvaluator.IConditionEvaluationState state, out Version result)
        {
            result = default;
            return false;
        }

        internal override bool EvaluatesToEmpty(ConditionEvaluator.IConditionEvaluationState state)
            => true;

        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
            => string.Empty;

        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
            => string.Empty;

        internal override bool IsUnexpandedValueEmpty()
            => true;

        internal override void ResetState()
        {
        }
    }
}
