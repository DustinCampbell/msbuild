// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
using static Microsoft.Build.Execution.ProjectItemInstance;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Evaluates the Exists('path') condition function.
/// </summary>
internal sealed class ExistsCallNode(ImmutableArray<ExpressionNode> arguments) : FunctionCallNode(arguments)
{
    protected override string FunctionName => "Exists";

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        // Check we only have one argument
        ExpressionNode argument = GetSingleArgument(state);

        try
        {
            // Expand the items and use DefaultIfEmpty in case there is nothing returned
            // Then check if everything is not null (because the list was empty), not
            // already loaded into the cache, and exists
            ImmutableArray<string> fileList = ExpandArgumentAsFileList(argument, state);

            if (fileList.IsEmpty)
            {
                result = false;
                return true;
            }

            foreach (string file in fileList)
            {
                if (file == null || !(state.LoadedProjectsCache?.TryGet(file) != null || FileUtilities.FileOrDirectoryExistsNoThrow(file, state.FileSystem)))
                {
                    result = false;
                    return true;
                }
            }

            result = true;
            return true;
        }
        catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
        {
            // Ignore invalid characters or path related exceptions

            // We will ignore the PathTooLong exception caused by GetFullPath because in single proc this code
            // is not executed and the condition is just evaluated to false as File.Exists and Directory.Exists does not throw in this situation.
            // To be consistant with that we will return a false in this case also.
            // DevDiv Bugs: 46035
            result = false;
            return true;
        }
    }

    private static ImmutableArray<string> ExpandArgumentAsFileList(ExpressionNode argumentNode, ConditionEvaluator.IConditionEvaluationState state)
    {
        string? argument = argumentNode.GetUnexpandedValue(state);
        Assumed.NotNull(argument, "Arguments should only be literals, i.e. strings or numbers.");

        // Fix path before expansion
        argument = FileUtilities.FixFilePath(argument);

        IList<TaskItem> expanded = state.ExpandIntoTaskItems(argument);
        int expandedCount = expanded.Count;

        if (expandedCount == 0)
        {
            return [];
        }

        string[] array = new string[expandedCount];

        for (int i = 0; i < expandedCount; i++)
        {
            TaskItem item = expanded[i];

            array[i] = state.EvaluationDirectory != null && !Path.IsPathRooted(item.ItemSpec)
                ? Path.GetFullPath(Path.Combine(state.EvaluationDirectory, item.ItemSpec))
                : item.ItemSpec;
        }

        return ImmutableCollectionsMarshal.AsImmutableArray(array);
    }
}
