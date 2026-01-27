// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;

namespace Microsoft.Build.Evaluation;

internal sealed class UnknownFunctionCallExpressionNode(string name, ImmutableArray<GenericExpressionNode> arguments)
    : FunctionCallExpressionNode(name, arguments)
{
    protected override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        => throw new NotSupportedException();
}
