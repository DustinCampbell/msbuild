// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using static Microsoft.Build.Execution.ProjectItemInstance;
using static Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.Expansion;

/// <summary>
///  Provides convenience operations shared by all <see cref="IExpander{TProperty, TItem}"/> implementations.
/// </summary>
internal static class IExpanderExtensions
{
    extension<TProperty, TItem>(IExpander<TProperty, TItem> expander)
        where TProperty : class, IProperty
        where TItem : class, IItem
    {
        /// <summary>
        ///  Expands the selected expressions into escaped task items.
        /// </summary>
        /// <param name="expression">The expression to expand.</param>
        /// <param name="options">The kinds of expressions to expand and any expansion behavior modifiers.</param>
        /// <param name="location">The project location associated with the expression.</param>
        /// <returns>
        ///  The expanded task items, or <see langword="null"/> when <see cref="ExpanderOptions.BreakOnNotEmpty"/>
        ///  causes expansion to stop early.
        /// </returns>
        public IList<TaskItem>? ExpandIntoTaskItemsLeaveEscaped(string expression, ExpanderOptions options, IElementLocation location)
            => expander.ExpandIntoItemsLeaveEscaped(expression, (IItemFactory<TItem, TaskItem>)TaskItemFactory.Instance, options, location);
    }
}
