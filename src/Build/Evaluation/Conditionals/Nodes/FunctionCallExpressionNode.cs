// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.Evaluation;

/// <summary>
/// Evaluates a function expression, such as "Exists('foo')"
/// </summary>
internal sealed class FunctionCallExpressionNode : OperandExpressionNode
{
    private readonly ReadOnlyMemory<char> _functionName;
    private readonly ImmutableArray<ExpressionNode> _arguments;

    internal FunctionCallExpressionNode(ReadOnlyMemory<char> functionName, ImmutableArray<ExpressionNode> arguments)
    {
        _functionName = functionName;
        _arguments = arguments;
    }

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        if (_functionName.Span.Equals("Exists", StringComparison.OrdinalIgnoreCase))
        {
            result = EvaluateExists(_arguments, state);
            return true;
        }

        if (_functionName.Span.Equals("HasTrailingSlash", StringComparison.OrdinalIgnoreCase))
        {
            result = EvaluateHasTrailingSlash(_functionName, _arguments, state);
            return true;
        }

        // We haven't implemented any other "functions"
        ProjectErrorUtilities.ThrowInvalidProject(
            state.ElementLocation,
            "UndefinedFunctionCall",
            state.Condition,
            _functionName.ToString());

        result = false;
        return true;
    }

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

    internal override string? GetExpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    internal override string? GetUnexpandedValue(ConditionEvaluator.IConditionEvaluationState state)
        => null;

    /// <inheritdoc cref="ExpressionNode"/>
    internal override bool IsUnexpandedValueEmpty() => true;

    internal override void ResetState()
    {
        foreach (ExpressionNode argument in _arguments)
        {
            argument.ResetState();
        }
    }

    private static bool EvaluateExists(ImmutableArray<ExpressionNode> arguments, ConditionEvaluator.IConditionEvaluationState state)
    {
        // Check we only have one argument
        ExpressionNode argument = GetSingleArgument(arguments, state);

        try
        {
            // Expand the items and use DefaultIfEmpty in case there is nothing returned
            // Then check if everything is not null (because the list was empty), not
            // already loaded into the cache, and exists
            ImmutableArray<string> fileList = ExpandArgumentAsFileList(argument, state);

            if (fileList.IsEmpty)
            {
                return false;
            }

            foreach (string file in fileList)
            {
                if (file == null || !(state.LoadedProjectsCache?.TryGet(file) != null || FileUtilities.FileOrDirectoryExistsNoThrow(file, state.FileSystem)))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            // Ignore invalid characters or path related exceptions

            // We will ignore the PathTooLong exception caused by GetFullPath because in single proc this code
            // is not executed and the condition is just evaluated to false as File.Exists and Directory.Exists does not throw in this situation.
            // To be consistant with that we will return a false in this case also.
            // DevDiv Bugs: 46035
            return false;
        }
    }

    private static bool EvaluateHasTrailingSlash(
        ReadOnlyMemory<char> functionName,
        ImmutableArray<ExpressionNode> arguments,
        ConditionEvaluator.IConditionEvaluationState state)
    {
        // Check we only have one argument
        ExpressionNode argument = GetSingleArgument(arguments, state);

        // Expand properties and items, and verify the result is an appropriate scalar
        string expandedValue = ExpandArgumentForScalarParameter(functionName, argument, state);

        // Is the last character a backslash?
        if (expandedValue.Length != 0)
        {
            char lastCharacter = expandedValue[^1];

            // Either back or forward slashes satisfy the function: this is useful for URL's
            return lastCharacter == Path.DirectorySeparatorChar || lastCharacter == Path.AltDirectorySeparatorChar || lastCharacter == '\\';
        }

        return false;
    }

    /// <summary>
    /// Expands properties and items in the argument, and verifies that the result is consistent
    /// with a scalar parameter type.
    /// </summary>
    /// <param name="functionName">Function name for errors</param>
    /// <param name="argumentNode">Argument to be expanded</param>
    /// <param name="state"></param>
    /// <param name="isFilePath">True if this is afile name and the path should be normalized</param>
    /// <returns>Scalar result</returns>
    private static string ExpandArgumentForScalarParameter(
        ReadOnlyMemory<char> functionName,
        ExpressionNode argumentNode,
        ConditionEvaluator.IConditionEvaluationState state,
        bool isFilePath = true)
    {
        string? argument = argumentNode.GetUnexpandedValue(state);
        Assumed.NotNull(argument, "Arguments should only be literals, i.e. strings or numbers.");

        // Fix path before expansion
        if (isFilePath)
        {
            argument = FileUtilities.FixFilePath(argument);
        }

        IList<TaskItem> items = state.ExpandIntoTaskItems(argument);

        switch (items)
        {
            case []:
                // Empty argument, that's fine.
                return string.Empty;
            case [TaskItem item]:
                return item.ItemSpec;
        }

        // We only allow a single item to be passed into a scalar parameter.
        ProjectErrorUtilities.ThrowInvalidProject(
            state.ElementLocation,
            "CannotPassMultipleItemsIntoScalarFunction",
            functionName.ToString(),
            argument,
            state.ExpandIntoString(argument));

        return string.Empty;
    }

    private static ImmutableArray<string> ExpandArgumentAsFileList(ExpressionNode argumentNode, ConditionEvaluator.IConditionEvaluationState state, bool isFilePath = true)
    {
        string? argument = argumentNode.GetUnexpandedValue(state);
        Assumed.NotNull(argument, "Arguments should only be literals, i.e. strings or numbers.");

        // Fix path before expansion
        if (isFilePath)
        {
            argument = FileUtilities.FixFilePath(argument);
        }

        IList<TaskItem> expanded = state.ExpandIntoTaskItems(argument);
        int expandedCount = expanded.Count;

        if (expandedCount == 0)
        {
            return [];
        }

        using RefArrayBuilder<string> list = new(initialCapacity: expandedCount);

        for (int i = 0; i < expandedCount; i++)
        {
            TaskItem item = expanded[i];

            if (state.EvaluationDirectory != null && !Path.IsPathRooted(item.ItemSpec))
            {
                list.Add(Path.GetFullPath(Path.Combine(state.EvaluationDirectory, item.ItemSpec)));
            }
            else
            {
                list.Add(item.ItemSpec);
            }
        }

        return list.ToImmutable();
    }

    private static ExpressionNode GetSingleArgument(ImmutableArray<ExpressionNode> arguments, ConditionEvaluator.IConditionEvaluationState state)
    {
        if (arguments.Length == 1)
        {
            return arguments[0];
        }

        ProjectErrorUtilities.ThrowInvalidProject(
            state.ElementLocation,
            "IncorrectNumberOfFunctionArguments",
            state.Condition,
            arguments.Length,
            1);

        return null!;
    }
}
