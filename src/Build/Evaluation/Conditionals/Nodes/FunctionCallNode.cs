// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Base class for condition function call nodes such as Exists() and HasTrailingSlash().
/// </summary>
internal abstract class FunctionCallNode(ImmutableArray<ExpressionNode> arguments, ExpressionNodeFlags flags)
    : ExpressionNode(flags)
{
    private protected readonly ImmutableArray<ExpressionNode> _arguments = arguments;

    /// <summary>
    ///  The name of this function for use in error messages.
    /// </summary>
    protected abstract string FunctionName { get; }

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

    public override string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    public override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    /// <inheritdoc cref="ExpressionNode"/>
    public override bool IsUnexpandedValueEmpty() => true;

    public override void ResetState()
    {
        foreach (ExpressionNode argument in _arguments)
        {
            argument.ResetState();
        }
    }

    protected ExpressionNode GetSingleArgument(ConditionEvaluator.IConditionEvaluationState state)
    {
        if (_arguments is [var singleArgument])
        {
            return singleArgument;
        }

        ProjectErrorUtilities.ThrowInvalidProject(
            state.ElementLocation,
            "IncorrectNumberOfFunctionArguments",
            state.Condition,
            _arguments.Length,
            1);

        return null!;
    }
}
