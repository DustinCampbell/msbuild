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
    internal sealed class HasTrailingSlashCallExpressionNode : FunctionCallExpressionNode
    {
        public HasTrailingSlashCallExpressionNode(string name, ImmutableArray<GenericExpressionNode> arguments)
            : base(name, arguments)
        {
            Debug.Assert(string.Equals(name, "HasTrailingSlash", StringComparison.OrdinalIgnoreCase));
            Debug.Assert(arguments.Length == 1);
        }

        protected override bool BoolEvaluate(ConditionEvaluator.IConditionEvaluationState state)
        {
            // Expand properties and items, and verify the result is an appropriate scalar
            string expandedValue = ExpandArgumentForScalarParameter("HasTrailingSlash", Arguments[0], state);

            // Is the last character a backslash?
            if (expandedValue.Length != 0)
            {
                char lastCharacter = expandedValue[expandedValue.Length - 1];
                // Either back or forward slashes satisfy the function: this is useful for URL's
                return lastCharacter == Path.DirectorySeparatorChar || lastCharacter == Path.AltDirectorySeparatorChar || lastCharacter == '\\';
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Expands properties and items in the argument, and verifies that the result is consistent
        /// with a scalar parameter type.
        /// </summary>
        /// <param name="function">Function name for errors</param>
        /// <param name="argumentNode">Argument to be expanded</param>
        /// <param name="state"></param>
        /// <param name="isFilePath">True if this is afile name and the path should be normalized</param>
        /// <returns>Scalar result</returns>
        private static string ExpandArgumentForScalarParameter(
            string function,
            GenericExpressionNode argumentNode,
            ConditionEvaluator.IConditionEvaluationState state,
            bool isFilePath = true)
        {
            string argument = argumentNode.GetUnexpandedValue(state);

            // Fix path before expansion
            if (isFilePath)
            {
                argument = FrameworkFileUtilities.FixFilePath(argument);
            }

            IList<TaskItem> items = state.ExpandIntoTaskItems(argument);

            if (items.Count == 0)
            {
                // Empty argument, that's fine.
                return string.Empty;
            }
            else if (items.Count == 1)
            {
                return items[0].ItemSpec;
            }

            // We only allow a single item to be passed into a scalar parameter.
            ProjectErrorUtilities.ThrowInvalidProject(
                state.ElementLocation,
                "CannotPassMultipleItemsIntoScalarFunction",
                function,
                argument,
                state.ExpandIntoString(argument));

            return ErrorUtilities.ThrowInternalErrorUnreachable<string>();
        }
    }
}
