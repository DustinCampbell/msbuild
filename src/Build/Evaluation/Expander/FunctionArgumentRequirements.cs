// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Evaluation.Expander;

/// <summary>
///  Describes the work required before a property-function argument can leave MSBuild's escaped-data state.
/// </summary>
[Flags]
internal enum FunctionArgumentRequirements : byte
{
    /// <summary>
    ///  The source can be consumed directly.
    /// </summary>
    None = 0,

    /// <summary>
    ///  The source contains a property expression that must be expanded.
    /// </summary>
    ExpandProperties = 1,

    /// <summary>
    ///  The source contains an MSBuild <c>%XX</c> escape sequence that must be decoded.
    /// </summary>
    Unescape = 2,
}
