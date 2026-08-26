// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Represents a validated property-function expression.
/// </summary>
internal readonly struct PropertyFunctionExpression(StringSegment text, OneOrMany<PropertyFunctionInvocation> invocations)
{
    /// <summary>
    ///  Gets the complete parser input.
    /// </summary>
    public StringSegment Text { get; } = text;

    /// <summary>
    ///  Gets the property-function invocations.
    /// </summary>
    public OneOrMany<PropertyFunctionInvocation> Invocations { get; } = invocations;

    /// <summary>
    ///  Returns an allocation-free enumerator over the property-function invocations.
    /// </summary>
    public OneOrMany<PropertyFunctionInvocation>.Enumerator GetEnumerator()
        => Invocations.GetEnumerator();
}
