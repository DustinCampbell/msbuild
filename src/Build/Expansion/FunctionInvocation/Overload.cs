// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Describes the parameters of one member overload.
/// </summary>
/// <param name="parameters">The overload parameters in declaration order.</param>
internal readonly struct Overload(ImmutableArray<Parameter> parameters)
{
    /// <summary>
    ///  Gets the overload parameters in declaration order.
    /// </summary>
    /// <remarks>
    ///  A default parameter array, including the value produced by <c>default(Overload)</c>, represents a
    ///  parameterless overload.
    /// </remarks>
    public ImmutableArray<Parameter> Parameters
        => parameters.IsDefault ? [] : parameters;
}
