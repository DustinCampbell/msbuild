// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.Expansion.FunctionInvocation;

/// <summary>
///  Describes the outcome of binding arguments to a set of candidate overloads.
/// </summary>
internal enum BindingResult : byte
{
    /// <summary>
    ///  An overload was selected successfully.
    /// </summary>
    Success,

    /// <summary>
    ///  No overload accepted every argument.
    /// </summary>
    NoApplicableOverload,

    /// <summary>
    ///  Multiple applicable overloads were equally specific.
    /// </summary>
    Ambiguous,
}
