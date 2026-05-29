// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Base class for nodes that are unary operators (have a single child in the parse tree)
    /// </summary>
    internal abstract class UnaryOperatorExpressionNode : ExpressionNode
    {
        internal ExpressionNode Child { get; }

        protected UnaryOperatorExpressionNode(ExpressionNode child)
        {
            Child = child;
        }

        public override bool TryEvaluateAsNumber(ConditionEvaluator.IConditionEvaluationState state, out double result)
        {
            result = default;
            return false;
        }

        public override bool TryEvaluateAsVersion(ConditionEvaluator.IConditionEvaluationState state, out Version result)
        {
            result = default;
            return false;
        }

        internal override string GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return null;
        }

        internal override string GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        {
            return null;
        }

        /// <inheritdoc cref="ExpressionNode"/>
        internal override bool IsUnexpandedValueEmpty()
            => Child?.IsUnexpandedValueEmpty() ?? true;

        internal override void ResetState()
        {
            Child?.ResetState();
        }

        #region REMOVE_COMPAT_WARNING
        internal override bool DetectAnd()
        {
            bool detectedAnd = this.PossibleAndCollision;
            this.PossibleAndCollision = false;
            bool detectAndChild = Child != null ? Child.DetectAnd() : false;
            return detectedAnd || detectAndChild;
        }

        internal override bool DetectOr()
        {
            bool detectedOr = this.PossibleOrCollision;
            this.PossibleOrCollision = false;
            bool detectOrChild = Child != null ? Child.DetectOr() : false;
            return detectedOr || detectOrChild;
        }
        #endregion
    }
}
