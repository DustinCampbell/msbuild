// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

[Flags]
internal enum LazyItemEvaluatorOptions
{
    None = 0,

    /// <summary>
    ///  Evaluate items for design-time purposes (e.g., IDE scenarios).
    ///  When set, items with <see langword="false"/> conditions are still tracked via AddItemIgnoringCondition.
    /// </summary>
    EvaluateForDesignTime = 1 << 0,

    /// <summary>
    ///  Allow evaluation of elements whose conditions are <see langword="false"/>.
    ///  When combined with <see cref="EvaluateForDesignTime"/>, item groups and items
    ///  with <see langword="false"/> conditions are still processed (though their items are not added to the main item list).
    /// </summary>
    CanEvaluateElementsWithFalseConditions = 1 << 1,

    /// <summary>
    ///  Record each evaluated item element in the evaluator data.
    /// </summary>
    RecordEvaluatedItemElements = 1 << 2,
}
