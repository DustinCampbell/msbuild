// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Evaluates a function expression, such as "Exists('foo')"
    /// </summary>
    internal abstract class FunctionCallExpressionNode : GenericExpressionNode
    {
        public string Name { get; }
        public ImmutableArray<GenericExpressionNode> Arguments { get; }

        protected FunctionCallExpressionNode(string name, ImmutableArray<GenericExpressionNode> arguments)
        {
            Name = name;
            Arguments = arguments;
        }

        internal override bool TryBoolEvaluate(ConditionEvaluator.IConditionEvaluationState state, out bool result)
        {
            result = BoolEvaluate(state);
            return true;
        }

        protected abstract bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state);

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

        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
            => null;

        internal override bool IsUnexpandedValueEmpty()
            => true;

        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
            => null;

        internal override void ResetState()
        {
        }

        internal override bool DetectAnd() => PossibleAndCollision;
        internal override bool DetectOr() => PossibleOrCollision;
    }
}
