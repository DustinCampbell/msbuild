// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Shared;

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

    internal static ExpressionShredder.ItemExpressionCapture? ExpandSingleItemVectorExpressionIntoExpressionCapture(
        string expression,
        ExpanderOptions options,
        IElementLocation elementLocation)
    {
        if (((options & ExpanderOptions.ExpandItems) == 0) || (expression.Length == 0))
        {
            return null;
        }

        if (expression.IndexOf('@') < 0)
        {
            return null;
        }

        ExpressionShredder.ReferencedItemExpressionsEnumerator matchesEnumerator = ExpressionShredder.GetReferencedItemExpressions(expression);

        if (!matchesEnumerator.MoveNext())
        {
            return null;
        }

        ExpressionShredder.ItemExpressionCapture match = matchesEnumerator.Current;

        // We have a single valid @(itemlist) reference in the given expression.
        // If the passed-in expression contains exactly one item list reference,
        // with nothing else concatenated to the beginning or end, then proceed
        // with itemizing it, otherwise error.
        ProjectErrorUtilities.VerifyThrowInvalidProject(match.Value == expression, elementLocation, "EmbeddedItemVectorCannotBeItemized", expression);
        ErrorUtilities.VerifyThrow(!matchesEnumerator.MoveNext(), "Expected just one item vector");

        return match;
    }
}
