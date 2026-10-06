// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Groups the known overloads of one member.
/// </summary>
/// <param name="overloads">The member's overloads.</param>
internal readonly struct MemberGroup(ImmutableArray<Overload> overloads)
{
    /// <summary>
    ///  Gets the member's overloads.
    /// </summary>
    public ImmutableArray<Overload> Overloads => overloads;
}
