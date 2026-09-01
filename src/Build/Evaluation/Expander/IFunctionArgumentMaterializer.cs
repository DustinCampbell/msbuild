// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Materializes property-function arguments when their source segments cannot be consumed directly.
/// </summary>
internal interface IFunctionArgumentMaterializer
{
    /// <summary>
    ///  Materializes one argument.
    /// </summary>
    /// <param name="source">The argument source.</param>
    /// <param name="index">The zero-based argument index.</param>
    /// <param name="requirements">The work required before the argument can be consumed.</param>
    /// <returns>
    ///  The materialized argument.
    /// </returns>
    object? Materialize(StringSegment source, int index, FunctionArgumentRequirements requirements);
}
