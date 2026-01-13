// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

internal static class ExpanderUtilities
{
    /// <summary>
    /// Tests to see if the expression may contain expandable expressions, i.e.
    /// contains $, % or @.
    /// </summary>
    public static bool ExpressionMayContainExpandableExpressions(string expression)
        => expression.AsSpan().IndexOfAny('$', '%', '@') >= 0;

    /// <summary>
    ///  Returns <see langword="true"/> if the expression contains an item vector pattern;
    ///  otherwise, returns <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///  Used to flag use of item expressions where they are illegal.
    /// </remarks>
    public static bool ExpressionContainsItemVector(string expression)
    {
        var transformsEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

        return transformsEnumerator.MoveNext();
    }
}
