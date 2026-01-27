// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

#nullable disable

namespace Microsoft.Build.Evaluation
{
    internal sealed class ExistsCallExpressionNode : FunctionCallExpressionNode
    {
        public ExistsCallExpressionNode(string name, ImmutableArray<GenericExpressionNode> arguments)
            : base(name, arguments)
        {
            Debug.Assert(string.Equals(name, "Exists", StringComparison.OrdinalIgnoreCase));
            Debug.Assert(arguments.Length == 1);
        }

        protected override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            try
            {
                // Expand the items and use DefaultIfEmpty in case there is nothing returned
                // Then check if everything is not null (because the list was empty), not
                // already loaded into the cache, and exists
                List<string> list = ExpandArgumentAsFileList(Arguments[0], state);
                if (list == null)
                {
                    return false;
                }

                foreach (var item in list)
                {
                    if (item == null || !(state.LoadedProjectsCache?.TryGet(item) != null || FileUtilities.FileOrDirectoryExistsNoThrow(item, state.FileSystem)))
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

        private List<string> ExpandArgumentAsFileList(GenericExpressionNode argumentNode, ConditionEvaluator.IConditionEvaluationState state, bool isFilePath = true)
        {
            string argument = argumentNode.GetUnexpandedValue(state);

            // Fix path before expansion
            if (isFilePath)
            {
                argument = FrameworkFileUtilities.FixFilePath(argument);
            }

            IList<TaskItem> expanded = state.ExpandIntoTaskItems(argument);
            var expandedCount = expanded.Count;

            if (expandedCount == 0)
            {
                return null;
            }

            var list = new List<string>(capacity: expandedCount);
            for (var i = 0; i < expandedCount; i++)
            {
                var item = expanded[i];
                if (state.EvaluationDirectory != null && !Path.IsPathRooted(item.ItemSpec))
                {
                    list.Add(Path.GetFullPath(Path.Combine(state.EvaluationDirectory, item.ItemSpec)));
                }
                else
                {
                    list.Add(item.ItemSpec);
                }
            }

            return list;
        }
    }
}
