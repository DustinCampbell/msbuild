// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation;

/// <summary>
///  Flags indicating what types a node can potentially evaluate to.
///  Set at construction time based on parser guarantees.
/// </summary>
[Flags]
internal enum ExpressionNodeFlags : byte
{
    None = 0,

    /// <summary>
    ///  The node can potentially evaluate to a numeric value.
    /// </summary>
    CanBeNumeric = 1,

    /// <summary>
    ///  The node can potentially evaluate to a boolean value.
    /// </summary>
    CanBeBoolean = 2,
}
