// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Collections;
using Microsoft.Build.Text;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Represents a validated MSBuild property function and its invocation chain.
/// </summary>
/// <param name="text">The complete property-function text.</param>
/// <param name="invocations">The ordered invocations comprising the property function.</param>
internal readonly struct PropertyFunction(StringSegment text, OneOrMany<Invocation> invocations)
{
    /// <summary>
    ///  Gets the complete property-function text.
    /// </summary>
    public StringSegment Text { get; } = text;

    /// <summary>
    ///  Gets the ordered invocations comprising the property function.
    /// </summary>
    public readonly OneOrMany<Invocation> Invocations = invocations;

    /// <summary>
    ///  Returns an allocation-free enumerator over the property-function invocations.
    /// </summary>
    public OneOrMany<Invocation>.Enumerator GetEnumerator()
        => Invocations.GetEnumerator();
}
