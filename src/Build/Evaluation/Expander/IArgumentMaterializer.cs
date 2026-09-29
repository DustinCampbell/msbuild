// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Materializes property-function argument values.
/// </summary>
internal interface IArgumentMaterializer
{
    /// <summary>
    ///  Materializes an argument from its unevaluated source text.
    /// </summary>
    /// <param name="argText">The unevaluated argument text.</param>
    /// <param name="argIndex">The zero-based argument index.</param>
    /// <param name="options">The options controlling expansion.</param>
    /// <param name="context">The context in which the argument is expanded.</param>
    /// <returns>
    ///  The materialized argument value.
    /// </returns>
    object? MaterializeArgument(string argText, int argIndex, ExpanderOptions options, ref readonly ExpanderContext context);
}
