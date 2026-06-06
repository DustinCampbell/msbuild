// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.ProjectItemInstance;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Evaluates the HasTrailingSlash('path') condition function.
/// </summary>
internal sealed class HasTrailingSlashCallNode(ImmutableArray<ExpressionNode> arguments) : FunctionCallNode(arguments)
{
    protected override string FunctionName => "HasTrailingSlash";

    public override bool TryEvaluateAsBoolean(ConditionEvaluator.IConditionEvaluationState state, out bool result)
    {
        // Check we only have one argument
        ExpressionNode argument = GetSingleArgument(state);

        // Expand properties and items, and verify the result is an appropriate scalar
        string expandedValue = ExpandArgumentForScalarParameter(argument, state);

        // Is the last character a backslash?
        if (expandedValue.Length != 0)
        {
            char lastCharacter = expandedValue[^1];

            // Either back or forward slashes satisfy the function: this is useful for URL's
            result = lastCharacter == Path.DirectorySeparatorChar || lastCharacter == Path.AltDirectorySeparatorChar || lastCharacter == '\\';
            return true;
        }

        result = false;
        return true;
    }

    /// <summary>
    ///  Expands properties and items in the argument, and verifies that the result is consistent
    ///  with a scalar parameter type.
    /// </summary>
    /// <param name="argumentNode">Argument to be expanded</param>
    /// <param name="state"></param>
    /// <returns>
    ///  Scalar result.
    /// </returns>
    private string ExpandArgumentForScalarParameter(ExpressionNode argumentNode, ConditionEvaluator.IConditionEvaluationState state)
    {
        string? argument = argumentNode.GetUnexpandedValue(state);
        Assumed.NotNull(argument, "Arguments should only be literals, i.e. strings or numbers.");

        // Fix path before expansion
        argument = FileUtilities.FixFilePath(argument);

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
            FunctionName,
            argument,
            state.ExpandIntoString(argument));

        return string.Empty;
    }
}
