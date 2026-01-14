// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation;

internal interface IExpander<P, I>
    where P : class, IProperty
    where I : class, IItem
{
    IMetadataTable? Metadata { get; set; }

    PropertiesUseTracker PropertiesUseTracker { get; }

    EvaluationContext? EvaluationContext { get; }

    string ExpandIntoStringAndUnescape(string expression, ExpanderOptions options, IElementLocation elementLocation);

    string ExpandIntoStringLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation);

    object ExpandPropertiesLeaveTypedAndEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation);

    SemiColonTokenizer ExpandIntoStringListLeaveEscaped(string expression, ExpanderOptions options, IElementLocation elementLocation);

    IList<T> ExpandIntoItemsLeaveEscaped<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, IElementLocation elementLocation)
        where T : class, IItem;

    IList<T> ExpandSingleItemVectorExpressionIntoItems<T>(string expression, IItemFactory<I, T> itemFactory, ExpanderOptions options, bool includeNullItems, out bool isTransformExpression, IElementLocation elementLocation)
        where T : class, IItem;

    IList<T> ExpandExpressionCaptureIntoItems<T>(
        ExpressionShredder.ItemExpressionCapture expressionCapture,
        IItemProvider<I> items,
        IItemFactory<I, T> itemFactory,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        IElementLocation elementLocation)
        where T : class, IItem;

    bool ExpandExpressionCapture(
        ExpressionShredder.ItemExpressionCapture expressionCapture,
        IElementLocation elementLocation,
        ExpanderOptions options,
        bool includeNullEntries,
        out bool isTransformExpression,
        out List<KeyValuePair<string, I>> itemsFromCapture);
}
