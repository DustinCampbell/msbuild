// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.ProjectItemInstance;
using static Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.Evaluation;

internal static class ExpanderExtensions
{
    /// <summary>
    /// Expands embedded item metadata, properties, and embedded item lists (in that order) as specified in the provided options
    /// and produces a list of TaskItems.
    /// If the expression is empty, returns an empty list.
    /// If ExpanderOptions.BreakOnNotEmpty was passed, expression was going to be non-empty, and it broke out early, returns null. Otherwise the result can be trusted.
    /// </summary>
    public static IList<TaskItem> ExpandIntoTaskItemsLeaveEscaped<P, I>(
        this IExpander<P, I> expander, string expression, ExpanderOptions options, IElementLocation elementLocation)
        where P : class, IProperty
        where I : class, IItem
        => expander.ExpandIntoItemsLeaveEscaped(
            expression, (IItemFactory<I, TaskItem>)TaskItemFactory.Instance, options, elementLocation);
}
